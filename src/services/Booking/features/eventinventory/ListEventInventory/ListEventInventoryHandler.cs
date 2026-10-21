using Booking.Data;
using InventoryModel = Booking.Features.EventInventory.Models.EventInventory;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.EventInventory.ListEventInventory;

public class ListEventInventoryHandler(BookingDbContext dbContext)
{
    public Task<List<InventoryModel>> Handle(
        Guid eventId,
        CancellationToken cancellationToken = default) =>
        dbContext.EventInventory.AsNoTracking()
            .Where(item => item.EventId == eventId)
            .OrderBy(item => item.SectionId)
            .ThenBy(item => item.InventoryId)
            .ToListAsync(cancellationToken);
}
