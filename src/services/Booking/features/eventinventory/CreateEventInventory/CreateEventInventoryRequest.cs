namespace Booking.Features.EventInventory.CreateEventInventory;

public record CreateEventInventoryRequest(string TicketType, int TotalQuantity, decimal Price);
