using Booking.Data;
using Booking.Features.Bookings;
using Microsoft.EntityFrameworkCore;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.Features.EventInventory.CreateEventInventory;

public class CreateEventInventoryHandler(BookingDbContext dbContext)
{
    public async Task<CreateEventInventoryResponse> Handle(
        Guid eventId,
        CreateEventInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        BookingValidation.ValidateCreateInventory(eventId, request);

        var alreadyExists = await dbContext.EventInventory.AnyAsync(
            inventory => inventory.EventId == eventId && inventory.SectionId == request.SectionId,
            cancellationToken);
        if (alreadyExists)
            throw new InventoryConflictException("Inventory already exists for this event and section.");

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

public sealed class InventoryConflictException(string message) : Exception(message);
