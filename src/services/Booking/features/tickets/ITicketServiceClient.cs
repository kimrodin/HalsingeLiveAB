namespace Booking.Features.Tickets;

public interface ITicketServiceClient
{
    Task IssueTicketsAsync(
        BookingConfirmedMessage message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketServiceTicketResponse>> GetTicketsForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetTicketPdfAsync(
        Guid bookingId,
        Guid ticketId,
        bool download,
        CancellationToken cancellationToken = default);

    Task<TicketServiceStatusChangeResponse> CheckInTicketAsync(
        Guid ticketId,
        Guid eventId,
        CancellationToken cancellationToken = default);

    Task<TicketServiceStatusChangeResponse> CancelTicketAsync(
        Guid ticketId,
        CancellationToken cancellationToken = default);
}

public sealed record TicketServiceTicketResponse(
    Guid TicketId,
    Guid BookingId,
    Guid BookingItemId,
    int TicketNumberInBookingItem,
    Guid EventId,
    string EventName,
    DateTimeOffset EventStartsAt,
    string VenueName,
    string SectionName,
    decimal UnitPrice,
    string Currency,
    string Status,
    DateTimeOffset IssuedAt);

public sealed record TicketServiceStatusChangeResponse(
    Guid TicketId,
    string Status,
    DateTimeOffset ChangedAt);
