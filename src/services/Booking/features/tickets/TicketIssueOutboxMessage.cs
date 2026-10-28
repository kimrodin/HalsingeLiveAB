namespace Booking.Features.Tickets;

public sealed class TicketIssueOutboxMessage
{
    public Guid MessageId { get; set; }
    public Guid BookingId { get; set; }
    public Guid BookingItemId { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTimeOffset EventStartsAtUtc { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "SEK";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string LastError { get; set; } = string.Empty;
}
