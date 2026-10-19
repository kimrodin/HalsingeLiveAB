namespace Booking.Features.EventInventory.CreateEventInventory;

public record CreateEventInventoryResponse(
    Guid InventoryId,
    Guid EventId,
    string TicketType,
    int TotalQuantity,
    decimal Price);
