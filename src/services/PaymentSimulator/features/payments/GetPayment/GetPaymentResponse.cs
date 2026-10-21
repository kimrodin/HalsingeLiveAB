using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Features.Payments.GetPayment;

/// <summary>Represents a payment record returned by the API.</summary>
public sealed record GetPaymentResponse(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);