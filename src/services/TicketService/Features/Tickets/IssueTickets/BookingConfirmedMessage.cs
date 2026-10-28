namespace TicketService.Features.Tickets.IssueTickets;

public sealed record BookingConfirmedMessage
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
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
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public string IdempotencyKey => $"{BookingId}:{BookingItemId}";
}
