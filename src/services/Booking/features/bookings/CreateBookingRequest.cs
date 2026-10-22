namespace Booking.Features.Bookings;

public sealed record CreateBookingRequest(
    Guid InventoryId,
    int Quantity,
    string CustomerName,
    string CustomerEmail);
