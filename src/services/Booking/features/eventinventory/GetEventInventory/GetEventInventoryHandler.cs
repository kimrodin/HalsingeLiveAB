using Booking.Data;
using Booking.Features.BookingHolds;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.EventInventory.GetEventInventory;

public class GetEventInventoryHandler(
    BookingDbContext dbContext,
    RedisReservationService reservations)
{
    public async Task<InventoryModel> Handle(
        Guid inventoryId,
        CancellationToken cancellationToken = default)
    {
        var inventory = await dbContext.EventInventory.AsNoTracking()
            .FirstOrDefaultAsync(item => item.InventoryId == inventoryId, cancellationToken);

        if (inventory is not null)
        {
            inventory.AvailableQuantity = await reservations.GetAvailableQuantityAsync(
                inventory.EventId,
                inventory.SectionId,
                inventory.AvailableQuantity);
        }

        return inventory!;
    }
}
