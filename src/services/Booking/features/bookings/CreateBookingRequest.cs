namespace Booking.Features.Bookings;

public sealed record CreateBookingRequest(
    Guid EventId,
    string TicketType,
    int Quantity,
    string CustomerId);
