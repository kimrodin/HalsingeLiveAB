namespace HalsingeLive.Contracts.Messaging;

/// <summary>Reports a terminal payment failure for a booking.</summary>
public sealed record PaymentFailed
{
    public required Guid MessageId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid BookingId { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    /// <summary>Terminal outcome code: Failed or TimedOut.</summary>
    public required string FailureCode { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
