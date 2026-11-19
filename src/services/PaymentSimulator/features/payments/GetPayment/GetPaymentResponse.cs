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
    DateTimeOffset? CompletedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? RefundedAt = null);

/// <summary>Summarizes a persisted payment-result delivery for privacy exports.</summary>
public sealed record PaymentResultOutboxResponse(
    Guid MessageId,
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Outcome,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    int AttemptCount);
