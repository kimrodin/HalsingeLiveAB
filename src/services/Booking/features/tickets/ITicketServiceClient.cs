using HalsingeLive.Contracts.Messaging;

namespace Booking.Features.Tickets;

public interface ITicketServiceClient
{
    Task IssueTicketsAsync(
        BookingConfirmedV1 message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TicketServiceTicketResponse>> GetTicketsForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<byte[]> GetTicketPdfAsync(
        Guid bookingId,
        Guid ticketId,
        bool download,
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
