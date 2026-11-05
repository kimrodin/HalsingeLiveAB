using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Booking.IntegrationTests;

public sealed class PaymentRequestMigrationTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    [BookingIntegrationFact]
    public async Task Migration_backfills_only_unsettled_intents_and_preserves_existing_bookings()
    {
        await using var db = fixture.CreateContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103125909_PaymentResultInbox");
        var inventory = await fixture.CreateInventoryAsync();
        var pending = await fixture.ReserveAsync(inventory, null!);
        var settled = await fixture.ReserveAsync(inventory, null!);
        var confirmed = await fixture.ReserveAsync(inventory, null!);
        var unrequested = await fixture.ReserveAsync(inventory, null!);
        await db.Bookings.Where(row => row.BookingId != unrequested)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PaymentRequested, true));
        await db.Bookings.Where(row => row.BookingId == settled)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PaymentSettled, true));
        await db.Bookings.Where(row => row.BookingId == confirmed)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "Confirmed"));

        await migrator.MigrateAsync();

        var request = Assert.Single(await db.PaymentRequestOutboxMessages.ToListAsync());
        Assert.Equal(pending, request.BookingId);
        Assert.Equal(inventory.Price, request.Amount);
        Assert.Equal("SEK", request.Currency);
        Assert.Null(request.DeliveredAtUtc);
        Assert.Equal(4, await db.Bookings.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
