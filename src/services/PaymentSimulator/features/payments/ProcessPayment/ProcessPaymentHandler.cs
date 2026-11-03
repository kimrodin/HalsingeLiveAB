using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.RefundPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging;

namespace PaymentSimulator.Features.Payments.ProcessPayment;

public class ProcessPaymentHandler(PaymentDbContext dbContext, SimulationSettingsService settingsService,
    RefundPaymentHandler refundPaymentHandler)
{
    public async Task<ProcessPaymentResponse> Handle(
        ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        var currency = PaymentValidation.ValidateAndNormalizeCurrency(request);

        var payment = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.BookingId == request.BookingId, cancellationToken);
        if (payment is null)
        {
            var (outcome, delayMs, timeoutMs) = await settingsService.Get(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            payment = new Payment
            {
                Id = Guid.NewGuid(), BookingId = request.BookingId, Amount = request.Amount,
                Currency = currency, Status = PaymentStatus.Pending, CreatedAt = now,
                CompleteAfter = now.AddMilliseconds(Math.Min(delayMs, timeoutMs)),
                PlannedOutcome = delayMs > timeoutMs ? PaymentStatus.TimedOut : PaymentValidation.ResolveOutcome(outcome)
            };
            dbContext.Payments.Add(payment);
            try
            {
                // Once committed, the worker owns completion even if this request disconnects.
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                dbContext.ChangeTracker.Clear();
                payment = await dbContext.Payments.AsNoTracking()
                    .SingleAsync(p => p.BookingId == request.BookingId, cancellationToken);
            }
        }
        if (payment.Amount != request.Amount || payment.Currency != currency)
            throw new PaymentConflictException("A payment with different details already exists for this booking.");

        if (payment.Status == PaymentStatus.Pending)
        {
            var waitUntil = payment.CompleteAfter < DateTimeOffset.UtcNow.AddSeconds(30)
                ? payment.CompleteAfter : DateTimeOffset.UtcNow.AddSeconds(30);
            // Timers may wake slightly early; recheck the persisted deadline before completion.
            while (waitUntil > DateTimeOffset.UtcNow)
            {
                var remaining = waitUntil - DateTimeOffset.UtcNow;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1,
                    Math.Ceiling(remaining.TotalMilliseconds))), cancellationToken);
            }
            await CompleteDuePayments(cancellationToken);
            payment = await dbContext.Payments.AsNoTracking()
                .SingleAsync(p => p.Id == payment.Id, cancellationToken);
        }
        return ToResponse(payment);
    }

    public async Task CompleteDuePayments(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        // One statement commits the terminal outcome and its event atomically. PostgreSQL
        // rechecks the Pending predicate after competing completions or cancellation fences.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            WITH completed AS (
                UPDATE "Payments"
                SET "Status" = "PlannedOutcome", "CompletedAt" = {now},
                    "PaidAt" = CASE WHEN "PlannedOutcome" = 'Succeeded' THEN {now} ELSE "PaidAt" END
                WHERE "Status" = 'Pending' AND "CompleteAfter" <= {now}
                RETURNING *
            )
            INSERT INTO "PaymentResultOutboxMessages"
                ("MessageId", "PaymentId", "BookingId", "Amount", "Currency", "Outcome",
                 "PaidAtUtc", "OccurredAtUtc", "NextAttemptAtUtc", "AttemptCount")
            SELECT gen_random_uuid(), "Id", "BookingId", "Amount", "Currency", "Status",
                   "PaidAt", "CompletedAt", "CompletedAt", 0 FROM completed
            ON CONFLICT ("PaymentId") DO NOTHING
            """, cancellationToken);
    }

    public async Task<ProcessPaymentResponse> Cancel(ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.BookingId == Guid.Empty || request.Amount <= 0
            || request.Amount != decimal.Round(request.Amount, 2)
            || request.Amount > 9999999999999999.99m
            || request.Currency?.Trim().ToUpperInvariant() != "SEK")
            throw new ArgumentException("Invalid cancellation request.");
        var payment = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.BookingId == request.BookingId, cancellationToken);
        if (payment is null)
        {
            var now = DateTimeOffset.UtcNow;
            payment = new Payment
            {
                Id = Guid.NewGuid(), BookingId = request.BookingId, Amount = request.Amount,
                Currency = "SEK", CreatedAt = now, CompleteAfter = now, CompletedAt = now,
                Status = PaymentStatus.TimedOut, PlannedOutcome = PaymentStatus.TimedOut,
                RefundedAt = now
            };
            dbContext.Payments.Add(payment);
            dbContext.PaymentResultOutboxMessages.Add(PaymentResultOutboxMessage.FromPayment(payment));
            try { await dbContext.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                dbContext.ChangeTracker.Clear();
                payment = await dbContext.Payments.AsNoTracking()
                    .SingleAsync(p => p.BookingId == request.BookingId, cancellationToken);
            }
        }
        if (payment.Amount != request.Amount || payment.Currency != "SEK")
            throw new PaymentConflictException("A payment with different details already exists for this booking.");
        return (await refundPaymentHandler.Handle(payment.Id, cancellationToken))!;
    }

    public static ProcessPaymentResponse ToResponse(Payment payment) => new(
        payment.Id, payment.BookingId, payment.Amount, payment.Currency,
        payment.Status, payment.CreatedAt, payment.CompletedAt, payment.PaidAt, payment.RefundedAt);
}

public sealed class PaymentConflictException(string message) : Exception(message);
