using Booking.Features.Bookings;
using Booking.Features.EventCatalog;
using Booking.Features.Seed.SeedPurchases;
using Microsoft.EntityFrameworkCore;
using Inventory = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.IntegrationTests;

public sealed class SeedPurchasesTests(BookingDatabaseFixture database) : IClassFixture<BookingDatabaseFixture>
{
    [BookingIntegrationFact]
    public async Task Delete_RemovesSeedPurchasesRestoresStockAndAllowsReseeding_KeepingOrdinaryData()
    {
        var stock = await CreateInventory(4);
        var payments = new Payments();
        var tickets = new Tickets();
        try
        {
            var first = await Seed(stock, payments);
            Assert.True(first.CreatedBookings > 0);
            Assert.Equal(0, (await Seed(stock, payments)).CreatedBookings);
            await using (var db = database.CreateContext())
            {
                foreach (var row in await db.Bookings.Include(row => row.Items).ToArrayAsync())
                {
                    Assert.Equal(BookingStatuses.Confirmed, row.Status);
                    Assert.True(row.PaymentSettled);
                    tickets.Rows[row.BookingId] = row.Items.Sum(item => item.Quantity);
                }
                Assert.Equal(first.TotalTickets, tickets.Rows.Values.Sum());
                var ordinaryId = Guid.NewGuid();
                db.Bookings.Add(new Booking.Features.Bookings.Models.Booking
                {
                    BookingId = ordinaryId, CustomerName = "Ordinary customer", CustomerEmail = "ordinary@example.test",
                    Status = BookingStatuses.Confirmed, TotalPrice = 125m, CreatedAt = DateTime.UtcNow,
                });
                payments.Rows[ordinaryId] = new(ordinaryId, 125m, DateTimeOffset.UtcNow);
                tickets.Rows[ordinaryId] = 1;
                await db.SaveChangesAsync();
            }
            var deleted = await Delete(payments, tickets);
            Assert.True(deleted.DeletedBookings > 0);
            Assert.True(deleted.DeletedTickets > 0);
            Assert.Equal(deleted.DeletedBookings, deleted.DeletedPayments);
            await using (var verify = database.CreateContext())
            {
                Assert.DoesNotContain("51000000-", Assert.Single(await verify.Bookings.ToArrayAsync()).BookingId.ToString());
                Assert.Empty(await verify.BookingItems.ToArrayAsync());
                Assert.Empty(await verify.TicketIssueOutboxMessages.ToArrayAsync());
                Assert.Equal(100, (await verify.EventInventory.SingleAsync()).AvailableQuantity);
                Assert.Equal(100, await database.Reservations.GetAvailableQuantityAsync(stock.EventId, stock.SectionId, 100));
                Assert.Single(payments.Rows);
                Assert.Single(tickets.Rows);
            }
            Assert.Equal(new DeleteSeedPurchasesResponse(0, 0, 0), await Delete(payments, tickets));
            var reseeded = await Seed(stock, payments);
            Assert.Equal(deleted.DeletedBookings, reseeded.CreatedBookings);
            Assert.Equal(100 - reseeded.TotalTickets,
                await database.Reservations.GetAvailableQuantityAsync(stock.EventId, stock.SectionId, 100));
        }
        finally { await CleanRedis(stock); }
    }

    private async Task<SeedPurchasesResponse> Seed(Inventory stock, Payments payments)
    {
        await using var db = database.CreateContext();
        return await new SeedPurchasesHandler(db, database.Reservations, new Snapshot(), payments).Handle([stock.EventId], default);
    }

    private async Task<DeleteSeedPurchasesResponse> Delete(Payments payments, Tickets tickets)
    {
        await using var db = database.CreateContext();
        return await new DeleteSeedPurchasesHandler(db, database.Reservations, payments, tickets).Handle(default);
    }

    private sealed class Tickets : ISeedTicketsClient
    {
        public Dictionary<Guid, int> Rows { get; } = [];
        public Task<int> DeleteTickets(CancellationToken ct)
        {
            var ids = Rows.Keys.Where(id => id.ToString().StartsWith("51000000-")).ToArray();
            var count = ids.Sum(id => Rows[id]);
            foreach (var id in ids) Rows.Remove(id);
            return Task.FromResult(count);
        }
    }

    private async Task<Inventory> CreateInventory(int ordinal)
    {
        var stock = new Inventory
        {
            InventoryId = Guid.NewGuid(), EventId = SeedPurchasesHandler.EventId(ordinal),
            SectionId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            TotalQuantity = 100, AvailableQuantity = 100, Price = 125m,
        };
        await using var db = database.CreateContext();
        db.EventInventory.Add(stock);
        await db.SaveChangesAsync();
        return stock;
    }

    private async Task CleanRedis(Inventory stock)
    {
        await database.Reservations.DeleteInventoryAsync(stock.EventId, stock.SectionId);
        await using var db = database.CreateContext();
        await db.TicketIssueOutboxMessages.ExecuteDeleteAsync();
        await db.BookingItems.ExecuteDeleteAsync();
        await db.Bookings.ExecuteDeleteAsync();
        await db.EventInventory.ExecuteDeleteAsync();
    }

    private sealed class Snapshot : IEventSnapshotProvider
    {
        public Task<EventSnapshot> GetSnapshotAsync(Guid eventId, Guid sectionId, CancellationToken ct = default) =>
            Task.FromResult(new EventSnapshot(eventId, "Seed concert", new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero), "Venue", "Section"));
    }
    private sealed class Payments : ISeedPaymentsClient
    {
        public Task<int> DeletePayments(CancellationToken ct)
        {
            var ids = Rows.Keys.Where(id => id.ToString().StartsWith("51000000-")).ToArray();
            var deleted = ids.Length;
            foreach (var id in ids) Rows.Remove(id);
            return Task.FromResult(deleted);
        }
        public Dictionary<Guid, SeedPayment> Rows { get; } = [];
        public Task EnsurePayments(IReadOnlyList<SeedPayment> rows, CancellationToken ct)
        {
            foreach (var row in rows) Rows[row.BookingId] = row;
            return Task.CompletedTask;
        }
    }
}
