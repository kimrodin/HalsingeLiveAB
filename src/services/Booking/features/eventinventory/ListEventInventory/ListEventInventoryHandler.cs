using Booking.Data;
using Booking.Features.BookingHolds;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.EventInventory.ListEventInventory;

public class ListEventInventoryHandler(
    BookingDbContext dbContext,
    RedisReservationService reservations)
{
    public async Task<List<InventoryModel>> Handle(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var inventory = await dbContext.EventInventory.AsNoTracking()
            .Where(item => item.EventId == eventId)
            .OrderBy(item => item.SectionId)
            .ThenBy(item => item.InventoryId)
            .ToListAsync(cancellationToken);

        foreach (var item in inventory)
        {
            item.AvailableQuantity = await reservations.GetAvailableQuantityAsync(
                item.EventId,
                item.SectionId,
                item.AvailableQuantity);
        }

        return inventory;
    }
}
