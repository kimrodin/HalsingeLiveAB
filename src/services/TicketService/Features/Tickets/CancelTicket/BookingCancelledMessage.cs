namespace TicketService.Features.Tickets.CancelTicket;

public sealed record BookingCancelledMessage
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid BookingId { get; init; }
    public Guid? BookingItemId { get; init; }
    public Guid EventId { get; init; }
    public string Reason { get; init; } = "CustomerCancelled";
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
