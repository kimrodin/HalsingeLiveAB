using System.Runtime.CompilerServices;
using Booking.Features.Bookings;
using Booking.Features.Bookings.Models;
using Booking.Features.Payments;
using Booking.Features.Statistics;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;
using Inventory = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.IntegrationTests;

public sealed class SalesStatisticsDatabaseTests(BookingDatabaseFixture database) : IClassFixture<BookingDatabaseFixture>
{
    [BookingIntegrationFact]
    public async Task ConfirmedBookingProjection_UsesPostgresAndSwedishPaymentDate()
    {
        await using var db = database.CreateContext();
        var bookingId = Guid.NewGuid();
        var inventoryId = Guid.NewGuid();
        db.EventInventory.Add(new Inventory
        {
            InventoryId = inventoryId, EventId = Guid.NewGuid(), SectionId = Guid.NewGuid(),
            TotalQuantity = 50, AvailableQuantity = 48, Price = 100m
        });
        db.Bookings.Add(new BookingModel
        {
            BookingId = bookingId, CustomerName = "Test", CustomerEmail = "test@example.com",
            Status = BookingStatuses.Confirmed, TotalPrice = 200m, CreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Items = [new BookingItem { BookingItemId = Guid.NewGuid(), InventoryId = inventoryId, Quantity = 2, UnitPrice = 100m }]
        });
        await db.SaveChangesAsync();
        var paidAt = DateTimeOffset.Parse("2026-10-24T22:30:00Z");
        var payments = new PaymentRows(new(Guid.NewGuid(), bookingId, 200m, "SEK", "Succeeded", paidAt, paidAt, paidAt, null));
        var result = await new SalesStatistics(db, payments).Get(new(new(2026, 10, 25), new(2026, 10, 25)), default);
        Assert.Equal(200m, result.Summary.NetRevenue);
        Assert.Equal(2, result.Summary.SoldTickets);
        Assert.Equal(new DateOnly(2026, 10, 25), Assert.Single(result.Daily).Date);
        Assert.Equal(0m, result.PreviousPeriod.Summary.NetRevenue);
    }

    private sealed class PaymentRows(PaymentResult payment) : IStatisticsPayments
    {
        public async IAsyncEnumerable<IReadOnlyList<PaymentResult>> GetAsync(DateTimeOffset from, DateTimeOffset toExclusive,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            Assert.Equal(DateTimeOffset.Parse("2026-10-23T22:00:00Z"), from);
            Assert.Equal(DateTimeOffset.Parse("2026-10-25T23:00:00Z"), toExclusive);
            yield return new[] { payment };
        }
    }
}
