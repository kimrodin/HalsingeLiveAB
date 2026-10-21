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
        if (request.SectionId == Guid.Empty)
            throw new ArgumentException("SectionId must be a non-empty UUID.", nameof(request));
        if (request.TotalQuantity < 0)
            throw new ArgumentException("TotalQuantity cannot be negative.", nameof(request));
        if (request.Price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(request));

        var inventory = new InventoryModel
        {
            InventoryId = Guid.NewGuid(),
            EventId = eventId,
            SectionId = request.SectionId,
            TotalQuantity = request.TotalQuantity,
            AvailableQuantity = request.TotalQuantity,
            Price = request.Price
        };

        dbContext.EventInventory.Add(inventory);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateEventInventoryResponse(
            inventory.InventoryId,
            inventory.EventId,
            inventory.SectionId,
            inventory.TotalQuantity,
            inventory.AvailableQuantity,
            inventory.Price);
    }
}
