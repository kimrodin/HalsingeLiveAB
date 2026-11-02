using System.Runtime.CompilerServices;
using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.Bookings.Models;
using Booking.Features.Payments;
using Booking.Features.Statistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task EmptyPeriod_HasZeroValuesAndNoMisleadingPercentage()
    {
        await using var db = Db();
        var result = await new SalesStatistics(db, new Payments()).Get(new(new(2026, 10, 1), new(2026, 10, 2)), default);
        Assert.Equal(new SalesSummary(0, 0, 0, 0, 0), result.Summary);
        Assert.Null(result.Changes.NetRevenuePercent);
        Assert.Null(result.Changes.SoldTicketsPercent);
        Assert.Equal(2, result.Daily.Length);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("failed")]
    [InlineData("refund-before-paid")]
    public async Task InconsistentConfirmedPayment_DoesNotReturnPartialStatistics(string scenario)
    {
        await using var db = Db();
        var gateway = new Payments();
        var id = Add(db, gateway, 100m, 1, "2026-10-02T12:00:00Z");
        await db.SaveChangesAsync();
        var payment = gateway.Rows[id];
        gateway.Rows[id] = scenario switch
        {
            "amount" => payment with { Amount = 99m },
            "currency" => payment with { Currency = "EUR" },
            "refund-before-paid" => payment with { RefundedAt = payment.PaidAt!.Value.AddHours(-1) },
            _ => payment with { Status = "Failed" }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => new SalesStatistics(db, gateway)
            .Get(new(new(2026, 10, 1), new(2026, 10, 31)), default));
    }

    [Fact]
    public async Task BatchesPaymentsAt500AndIncludesFinalPartialBatch()
    {
        await using var db = Db();
        var gateway = new Payments();
        for (var i = 0; i < 501; i++) Add(db, gateway, 10m, 1, "2026-10-02T12:00:00Z");
        await db.SaveChangesAsync();
        gateway.AfterFirstBatch = async () =>
        {
            Add(db, gateway, 10m, 1, "2026-10-02T12:00:00Z");
            await db.SaveChangesAsync();
        };
        var result = await new SalesStatistics(db, gateway).Get(new(new(2026, 10, 1), new(2026, 10, 31)), default);
        Assert.Equal(502, await db.Bookings.CountAsync());
        Assert.Equal(501, result.Summary.SoldTickets);
        Assert.Equal(new[] { 500, 1 }, gateway.BatchSizes);
    }

    [Fact]
    public async Task OldIncorrectPayments_DoNotEnterTheReportOrBookingLookups()
    {
        await using var db = Db();
        var gateway = new Payments();
        for (var i = 0; i < 501; i++)
        {
            var old = Add(db, gateway, 100m, 1, "2026-01-02T12:00:00Z");
            gateway.Rows[old] = gateway.Rows[old] with { Amount = 99m };
        }
        var current = Add(db, gateway, 100m, 1, "2026-11-01T12:00:00Z");
        await db.SaveChangesAsync();
        var result = await new SalesStatistics(db, gateway).Get(new(new(2026, 11, 1), new(2026, 11, 1)), default);
        Assert.Equal(100m, result.Summary.NetRevenue);
        Assert.Equal(1, result.Summary.SoldTickets);
        Assert.Equal(current, Assert.Single(gateway.ReturnedIds));
        Assert.Equal(1, Assert.Single(gateway.BatchSizes));
    }

    [Fact]
    public void DefaultsAndValidation_AreCalendarBasedAndBounded()
    {
        var period = SalesPeriod.Create(null, null, DateTimeOffset.Parse("2026-10-31T23:30:00Z"));
        Assert.Equal(new DateOnly(2026, 11, 1), period.To);
        Assert.Equal(30, period.Days);
        Assert.Throws<ArgumentException>(() => SalesPeriod.Create(new(2026, 10, 2), new(2026, 10, 1), default));
        Assert.Throws<ArgumentException>(() => SalesPeriod.Create(new(2025, 1, 1), new(2026, 1, 2), default));
    }

    [Fact]
    public async Task Controller_RequiresAdminAndReturns502ForPaymentFailure()
    {
        var method = typeof(SalesStatisticsController).GetMethod(nameof(SalesStatisticsController.GetSalesStatistics))!;
        Assert.Equal("AdminOnly", Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Policy);
        await using var db = Db();
        var gateway = new Payments { Failure = new HttpRequestException("Unavailable") };
        Add(db, gateway, 100, 1, "2026-10-02T12:00:00Z");
        await db.SaveChangesAsync();
        var controller = new SalesStatisticsController(new(db, gateway), TimeProvider.System, NullLogger<SalesStatisticsController>.Instance);
        var result = await controller.GetSalesStatistics(new(2026, 10, 1), new(2026, 10, 31), default);
        Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
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
        public Dictionary<Guid, PaymentResult> Rows { get; } = new();
        public List<int> BatchSizes { get; } = [];
        public List<Guid> ReturnedIds { get; } = [];
        public Exception? Failure { get; init; }
        public Func<Task>? AfterFirstBatch { get; set; }
        public async IAsyncEnumerable<IReadOnlyList<PaymentResult>> GetAsync(DateTimeOffset from, DateTimeOffset toExclusive,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            var rows = Rows.Values.Where(p => p.PaidAt >= from && p.PaidAt < toExclusive).ToArray();
            foreach (var batch in rows.Chunk(500))
            {
                BatchSizes.Add(batch.Length);
                ReturnedIds.AddRange(batch.Select(p => p.BookingId));
                yield return batch;
                if (BatchSizes.Count == 1 && AfterFirstBatch is not null) await AfterFirstBatch();
            }
        }
    }
}
