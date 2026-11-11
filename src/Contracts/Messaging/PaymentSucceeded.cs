namespace HalsingeLive.Contracts.Messaging;

/// <summary>Reports a successfully completed payment for a booking.</summary>
public sealed record PaymentSucceeded
{
    public required Guid MessageId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid BookingId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset PaidAtUtc { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
