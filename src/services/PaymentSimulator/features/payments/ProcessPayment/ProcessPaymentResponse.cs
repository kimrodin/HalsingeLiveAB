using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Features.Payments.ProcessPayment;

/// <summary>Represents the result of processing a simulated payment.</summary>
public sealed record ProcessPaymentResponse(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? RefundedAt = null
);
