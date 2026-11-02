using System.Runtime.CompilerServices;
using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.Bookings.Models;
using Booking.Features.Payments;
using Booking.Features.Statistics;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;

namespace Booking.UnitTests;

public sealed class SalesStatisticsTests
{
    [Fact]
    public async Task AggregatesPaidDatesRefundsZeroDaysAndPreviousPeriod()
    {
        await using var db = Db();
        var gateway = new Payments();
        Add(db, gateway, 250m, 2, "2026-10-01T22:30:00Z"); // Swedish Oct 2, booking created earlier.
        Add(db, gateway, 200m, 3, "2026-10-02T12:00:00Z", refunded: "2026-11-01T12:00:00Z");
        Add(db, gateway, 100m, 1, "2026-09-29T12:00:00Z"); // Preceding equal period.
        Add(db, gateway, 900m, 9, "2026-10-04T22:00:00Z"); // Swedish Oct 5, excluded.
        Add(db, gateway, 900m, 9, "2026-10-02T12:00:00Z", status: BookingStatuses.Reserved);
        await db.SaveChangesAsync();

        var result = await new SalesStatistics(db, gateway).Get(new(new(2026, 10, 2), new(2026, 10, 4)), default);
        Assert.Equal(new SalesSummary(450m, 200m, 250m, 2, 3), result.Summary);
        Assert.Equal(new SalesSummary(100m, 0m, 100m, 1, 0), result.PreviousPeriod.Summary);
        Assert.Equal(new DateOnly(2026, 9, 29), result.PreviousPeriod.From);
        Assert.Equal(150m, result.Changes.NetRevenuePercent);
        Assert.Equal(100m, result.Changes.SoldTicketsPercent);
        Assert.Equal(3, result.Daily.Length);
        Assert.Equal(250m, result.Daily[0].NetRevenue);
        Assert.Equal(0m, result.Daily[1].NetRevenue);
        Assert.Equal(0, result.Daily[1].SoldTickets);
        Assert.Equal("SEK", result.Currency);
        Assert.Equal("Europe/Stockholm", result.TimeZone);
    }

    [Theory]
    [InlineData(3, 29, 23)]
    [InlineData(10, 25, 25)]
    public void SwedishCalendarDays_RespectDaylightSavingTime(int month, int day, int hours)
    {
        var date = new DateOnly(2026, month, day);
        var period = new SalesPeriod(date, date);
        Assert.Equal(hours, (period.EndUtc - period.StartUtc).TotalHours);
    }

    private static BookingDbContext Db() => new(new DbContextOptionsBuilder<BookingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Guid Add(BookingDbContext db, Payments payments, decimal amount, int quantity, string paid,
        string? refunded = null, string status = BookingStatuses.Confirmed)
    {
        var id = Guid.NewGuid();
        db.Bookings.Add(new BookingModel
        {
            BookingId = id, Status = status, TotalPrice = amount, CreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Items = [new BookingItem { BookingItemId = Guid.NewGuid(), Quantity = quantity, UnitPrice = amount / quantity }]
        });
        payments.Rows[id] = new(Guid.NewGuid(), id, amount, "SEK", "Succeeded", DateTimeOffset.Parse(paid),
            DateTimeOffset.Parse(paid), DateTimeOffset.Parse(paid), refunded is null ? null : DateTimeOffset.Parse(refunded));
        return id;
    }
    private sealed class Payments : IStatisticsPayments
    {
        public Dictionary<Guid, PaymentResult> Rows { get; } = [];
        public async IAsyncEnumerable<IReadOnlyList<PaymentResult>> GetAsync(DateTimeOffset from, DateTimeOffset toExclusive,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield return Rows.Values.Where(p => p.PaidAt >= from && p.PaidAt < toExclusive).ToArray();
        }
    }
}
