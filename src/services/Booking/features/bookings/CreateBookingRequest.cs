namespace Booking.Features.Bookings;

public sealed record CreateBookingRequest(
    Guid EventId,
    Guid SectionId,
    int Quantity,
    string CustomerId);
