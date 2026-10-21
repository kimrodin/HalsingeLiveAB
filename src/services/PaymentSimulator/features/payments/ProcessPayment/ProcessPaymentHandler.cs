using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Features.Payments.ProcessPayment;

public class ProcessPaymentHandler(
    PaymentDbContext dbContext,
    IConfiguration configuration)
{
    public async Task<ProcessPaymentResponse> Handle(
        ProcessPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BookingId == Guid.Empty)
            throw new ArgumentException("Invalid BookingId");

        if (request.Amount <= 0)
            throw new ArgumentException("Amount must be positive");

        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Currency is required");

        var currency = request.Currency.Trim().ToUpperInvariant();
        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a three-letter code.");

        // Prevent duplicate payments for the same booking
        var existingPayment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.BookingId == request.BookingId,
                cancellationToken);

        if (existingPayment is not null)
        {
            return ToResponse(existingPayment);
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = request.BookingId,
            Amount = request.Amount,
            Currency = currency,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Simulate payment processing
        var outcome = configuration["PaymentSimulation:Outcome"]
            ?? "Succeeded";

        var delayMs = configuration.GetValue<int>(
            "PaymentSimulation:DelayMs", 500);

        await Task.Delay(delayMs, cancellationToken);

        switch (outcome.ToLowerInvariant())
        {
            case "succeeded":
                payment.Status = PaymentStatus.Succeeded;
                break;

            case "failed":
                payment.Status = PaymentStatus.Failed;
                break;

            case "timeout":
                payment.Status = PaymentStatus.TimedOut;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown payment outcome: {outcome}");
        }

        payment.CompletedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(payment);
    }

    private static ProcessPaymentResponse ToResponse(Payment payment)
    {
        return new ProcessPaymentResponse(
            payment.Id,
            payment.BookingId,
            payment.Amount,
            payment.Currency,
            payment.Status,
            payment.CreatedAt,
            payment.CompletedAt
        );
    }
}