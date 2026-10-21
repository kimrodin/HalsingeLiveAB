namespace Booking.Features.EventInventory.CreateEventInventory;

public record CreateEventInventoryRequest(Guid SectionId, int TotalQuantity, decimal Price);
