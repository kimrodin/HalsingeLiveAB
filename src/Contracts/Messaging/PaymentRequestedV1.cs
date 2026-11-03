namespace HalsingeLive.Contracts.Messaging;

/// <summary>Requests payment processing using BookingId as the idempotency key.</summary>
public sealed record PaymentRequestedV1
{
    public required Guid MessageId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid BookingId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
