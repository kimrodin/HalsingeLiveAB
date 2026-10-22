using Booking.Data;
using Booking.Features.BookingHolds;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Seed;

public sealed class ClearBookingDatabaseHandler(
    BookingDbContext dbContext,
    RedisReservationService reservations)
{
    public async Task<ClearBookingDatabaseResponse> Handle(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var deletedItemCount = await dbContext.BookingItems.ExecuteDeleteAsync(cancellationToken);
        var deletedBookingCount = await dbContext.Bookings.ExecuteDeleteAsync(cancellationToken);
        var deletedInventoryCount = await dbContext.EventInventory.ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await reservations.ClearAllInventoryAsync();

        return new ClearBookingDatabaseResponse(
            deletedBookingCount,
            deletedItemCount,
            deletedInventoryCount);
    }
}

public sealed record ClearBookingDatabaseResponse(
    int DeletedBookings,
    int DeletedBookingItems,
    int DeletedInventories);
