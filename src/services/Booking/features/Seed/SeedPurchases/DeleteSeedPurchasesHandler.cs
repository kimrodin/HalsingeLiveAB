using Booking.Data;
using Booking.Features.BookingHolds;
using Booking.Features.Bookings;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Seed.SeedPurchases;

public sealed class DeleteSeedPurchasesHandler(BookingDbContext db, RedisReservationService reservations,
    ISeedPaymentsClient payments, ISeedTicketsClient tickets)
{
    public async Task<DeleteSeedPurchasesResponse> Handle(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The ticket worker runs automatically, even without customer traffic.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(51000000)", ct);
        var bookings = await db.Bookings.Where(row => row.BookingId.ToString().StartsWith("51000000-"))
            .Include(row => row.Items).ThenInclude(item => item.EventInventory).ToArrayAsync(ct);
        var eventIds = Enumerable.Range(1, 40).Select(SeedPurchasesHandler.EventId).ToArray();
        var stock = await db.EventInventory.Where(row => eventIds.Contains(row.EventId)).ToArrayAsync(ct);
        var inventoryIds = stock.Select(row => row.InventoryId).ToArray();
        var bookingIds = bookings.Select(row => row.BookingId).ToArray();
        if (await db.BookingItems.AnyAsync(item => inventoryIds.Contains(item.InventoryId)
            && !bookingIds.Contains(item.BookingId), ct))
            throw new SeedPurchasesConflictException("Seed events have ordinary bookings. These must be kept; seed deletion was cancelled.");
        foreach (var booking in bookings)
        {
            if (booking.Items.Count != 1 || !eventIds.Contains(booking.Items[0].EventInventory.EventId))
                throw new SeedPurchasesConflictException("A seed booking has been changed and cannot be removed automatically.");
            // Older interrupted deletions already restored stock for these rows.
            if (booking.Status == "DeletingSeed") continue;
            if (booking.Status != BookingStatuses.Confirmed)
                throw new SeedPurchasesConflictException("A seed booking has been changed and cannot be removed automatically.");
            var item = booking.Items[0];
            if (item.EventInventory.AvailableQuantity + item.Quantity > item.EventInventory.TotalQuantity)
                throw new SeedPurchasesConflictException("Seed inventory quantities are inconsistent.");
            item.EventInventory.AvailableQuantity += item.Quantity;
        }
        // Remote deletes are idempotent. If either service fails, SQL remains intact
        // and pressing delete again completes the same operation.
        var deletedTickets = await tickets.DeleteTickets(ct);
        var deletedPayments = await payments.DeletePayments(ct);
        await db.TicketIssueOutboxMessages.Where(row => row.BookingId.ToString().StartsWith("51000000-"))
            .ExecuteDeleteAsync(ct);
        db.BookingItems.RemoveRange(bookings.SelectMany(row => row.Items));
        db.Bookings.RemoveRange(bookings);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        // Reload all seed inventory, including on a retry after a cache failure.
        foreach (var inventory in stock)
        {
            await reservations.DeleteInventoryAsync(inventory.EventId, inventory.SectionId);
            await reservations.InitializeInventoryAsync(inventory.EventId, inventory.SectionId, inventory.AvailableQuantity);
        }
        return new(bookings.Length, deletedPayments, deletedTickets);
    }
}

public sealed record DeleteSeedPurchasesResponse(int DeletedBookings, int DeletedPayments, int DeletedTickets);
