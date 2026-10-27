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

        return await IncludeLiveAvailability(inventory);
    }

    public async Task<List<InventoryModel>> HandleAll(CancellationToken cancellationToken = default)
    {
        var inventory = await dbContext.EventInventory.AsNoTracking()
            .OrderBy(item => item.EventId)
            .ThenBy(item => item.SectionId)
            .ThenBy(item => item.InventoryId)
            .ToListAsync(cancellationToken);

        return await IncludeLiveAvailability(inventory);
    }

    private async Task<List<InventoryModel>> IncludeLiveAvailability(List<InventoryModel> inventory)
    {
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
