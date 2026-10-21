namespace Booking.Features.EventInventory.CreateEventInventory;

public record CreateEventInventoryResponse(
    Guid InventoryId,
    Guid EventId,
    Guid SectionId,
    int TotalQuantity,
    decimal Price);
