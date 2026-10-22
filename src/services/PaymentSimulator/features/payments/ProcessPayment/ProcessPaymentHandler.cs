using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.RefundPayment;

namespace PaymentSimulator.Features.Payments.ProcessPayment;

public class ProcessPaymentHandler(PaymentDbContext dbContext, IConfiguration configuration,
    RefundPaymentHandler refundPaymentHandler)
{
    public async Task<ProcessPaymentResponse> Handle(
        ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.BookingId == Guid.Empty)
            throw new ArgumentException("Invalid BookingId");
        if (request.Amount <= 0 || request.Amount != decimal.Round(request.Amount, 2)
            || request.Amount > 9999999999999999.99m)
            throw new ArgumentException("Amount must be positive and have at most two decimal places.");
        var currency = request.Currency?.Trim().ToUpperInvariant();
        if (currency is null || currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency must be a three-letter code.");

        var payment = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.BookingId == request.BookingId, cancellationToken);
        if (payment is null)
        {
            var outcomeText = configuration["PaymentSimulation:Outcome"] ?? "Succeeded";
            var outcome = outcomeText.ToLowerInvariant() switch
            {
                "succeeded" => PaymentStatus.Succeeded,
                "failed" => PaymentStatus.Failed,
                "timeout" or "timedout" => PaymentStatus.TimedOut,
                _ => throw new InvalidOperationException($"Unknown payment outcome: {outcomeText}")
            };
            var delayMs = configuration.GetValue("PaymentSimulation:DelayMs", 500);
            var timeoutMs = configuration.GetValue("PaymentSimulation:TimeoutMs", 5000);
            if (delayMs < 0 || timeoutMs <= 0 || timeoutMs > 30000)
                throw new InvalidOperationException("DelayMs must be non-negative; TimeoutMs must be 1..30000.");
            var now = DateTimeOffset.UtcNow;
            payment = new Payment
            {
                Id = Guid.NewGuid(), BookingId = request.BookingId, Amount = request.Amount,
                Currency = currency, Status = PaymentStatus.Pending, CreatedAt = now,
                CompleteAfter = now.AddMilliseconds(Math.Min(delayMs, timeoutMs)),
                PlannedOutcome = delayMs > timeoutMs ? PaymentStatus.TimedOut : outcome
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
        // Conditional database updates allow multiple workers/HTTP requests without overwriting
        // terminal results or a refund's cancellation fence.
        await dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Pending && p.CompleteAfter <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, p => p.PlannedOutcome)
                .SetProperty(p => p.CompletedAt, now), cancellationToken);
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
        payment.Status, payment.CreatedAt, payment.CompletedAt, payment.RefundedAt);
}

public sealed class PaymentConflictException(string message) : Exception(message);
