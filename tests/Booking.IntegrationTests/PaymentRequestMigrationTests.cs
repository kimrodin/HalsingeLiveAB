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
        var pending = Guid.NewGuid();
        var settled = Guid.NewGuid();
        var confirmed = Guid.NewGuid();
        var unrequested = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Bookings"
                ("BookingId", "CustomerId", "CustomerName", "CustomerEmail", "Status", "TotalPrice",
                 "CreatedAt", "ExpiresAt", "CancelledAt", "PaymentRequested", "PaymentSettled",
                 "GuestAccessTokenHash")
            VALUES
                ({pending}, NULL, 'Pending', 'pending@example.com', 'Reserved', 125.50, {createdAt}, NULL, NULL, TRUE, FALSE, NULL),
                ({settled}, NULL, 'Settled', 'settled@example.com', 'Reserved', 125.50, {createdAt}, NULL, NULL, TRUE, TRUE, NULL),
                ({confirmed}, NULL, 'Confirmed', 'confirmed@example.com', 'Confirmed', 125.50, {createdAt}, NULL, NULL, TRUE, FALSE, NULL),
                ({unrequested}, NULL, 'Unrequested', 'unrequested@example.com', 'Reserved', 125.50, {createdAt}, NULL, NULL, FALSE, FALSE, NULL)
            """);

        await migrator.MigrateAsync();

        var request = Assert.Single(await db.PaymentRequestOutboxMessages.ToListAsync());
        Assert.Equal(pending, request.BookingId);
        Assert.Equal(125.50m, request.Amount);
        Assert.Equal("SEK", request.Currency);
        Assert.Null(request.DeliveredAtUtc);
        Assert.Equal(4, await db.Bookings.CountAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
