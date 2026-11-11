namespace HalsingeLive.Contracts.Messaging;

/// <summary>Snapshot of a paid booking item used to issue tickets.</summary>
public sealed record BookingConfirmed
{
    public required Guid MessageId { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid BookingItemId { get; init; }
    public required Guid EventId { get; init; }
    public required string EventName { get; init; }
    public required DateTimeOffset EventStartsAtUtc { get; init; }
    public required string VenueName { get; init; }
    public required string SectionName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public required string Currency { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
