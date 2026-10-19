using Booking.Data;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.EventInventory.GetEventInventory;

public class GetEventInventoryHandler(BookingDbContext dbContext)
{
    public Task<InventoryModel> Handle(
        Guid inventoryId,
        CancellationToken cancellationToken = default) =>
        dbContext.EventInventory.AsNoTracking()
            .FirstOrDefaultAsync(item => item.InventoryId == inventoryId, cancellationToken)!;
}
