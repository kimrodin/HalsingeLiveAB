namespace HalsingeLive.Contracts.Messaging;

/// <summary>Snapshot of a paid booking item used to issue tickets.</summary>
public sealed record BookingConfirmedV1
{
    public Guid MessageId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid BookingId { get; init; }
    public Guid BookingItemId { get; init; }
    public Guid EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public DateTimeOffset EventStartsAtUtc { get; init; }
    public string VenueName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = "SEK";
    public DateTimeOffset OccurredAtUtc { get; init; }
}
