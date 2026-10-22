using Booking.Data;
using Booking.Features.BookingHolds;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Seed;

public sealed class DeleteSeedInventoryHandler(
    BookingDbContext dbContext,
    RedisReservationService reservations)
{
    public async Task<DeleteSeedInventoryResponse> Handle(CancellationToken cancellationToken = default)
    {
        var eventIds = SeedInventoryData.Definitions.Select(item => item.EventId).Distinct().ToArray();
        var seededPairs = SeedInventoryData.Definitions
            .Select(item => (item.EventId, item.SectionId))
            .ToHashSet();

        var inventoryRows = await dbContext.EventInventory
            .Where(item => eventIds.Contains(item.EventId))
            .ToListAsync(cancellationToken);
        var seededRows = inventoryRows
            .Where(item => seededPairs.Contains((item.EventId, item.SectionId)))
            .ToArray();
        var inventoryIds = seededRows.Select(item => item.InventoryId).ToArray();

        if (inventoryIds.Length > 0)
        {
            var hasBookings = await dbContext.BookingItems
                .AnyAsync(item => inventoryIds.Contains(item.InventoryId), cancellationToken);
            if (hasBookings)
                throw new SeedInventoryDeleteConflictException(
                    "Seed inventory is referenced by bookings. Delete or clear those bookings before deleting the seed inventory.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var inventory in seededRows)
            await reservations.DeleteInventoryAsync(inventory.EventId, inventory.SectionId);

        dbContext.EventInventory.RemoveRange(seededRows);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DeleteSeedInventoryResponse(seededRows.Length);
    }
}

public sealed record DeleteSeedInventoryResponse(int DeletedCount);

public sealed class SeedInventoryDeleteConflictException(string message) : InvalidOperationException(message);
