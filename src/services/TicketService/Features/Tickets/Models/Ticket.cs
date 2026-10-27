namespace TicketService.Features.Tickets.Models;

public sealed class Ticket
{
    public Guid TicketId { get; set; }

    public Guid BookingId { get; set; }
    public Guid BookingItemId { get; set; }
    public int TicketNumberInBookingItem { get; set; }

    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTimeOffset EventStartsAt { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "SEK";

    public TicketStatus Status { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? CheckedInAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
