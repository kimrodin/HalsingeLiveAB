using Booking.Data;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.Features.EventInventory.CreateEventInventory;

public class CreateEventInventoryHandler(BookingDbContext dbContext)
{
    public async Task<CreateEventInventoryResponse> Handle(
        Guid eventId,
        CreateEventInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId must be a non-empty UUID.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(request.TicketType))
            throw new ArgumentException("TicketType is required.", nameof(request));
        if (request.TicketType.Length > 255)
            throw new ArgumentException("TicketType cannot exceed 255 characters.", nameof(request));
        if (request.TotalQuantity < 0)
            throw new ArgumentException("TotalQuantity cannot be negative.", nameof(request));
        if (request.Price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(request));

        var inventory = new InventoryModel
        {
            InventoryId = Guid.NewGuid(),
            EventId = eventId,
            TicketType = request.TicketType.Trim(),
            TotalQuantity = request.TotalQuantity,
            Price = request.Price
        };

        dbContext.EventInventory.Add(inventory);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateEventInventoryResponse(
            inventory.InventoryId,
            inventory.EventId,
            inventory.TicketType,
            inventory.TotalQuantity,
            inventory.Price);
    }
}
