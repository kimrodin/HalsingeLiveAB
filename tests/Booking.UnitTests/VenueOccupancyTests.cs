using System.Net;
using System.Net.Http.Json;
using Booking.Data;
using Booking.Features.Bookings.Occupancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Inventory = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.UnitTests;

public sealed class VenueOccupancyTests
{
    [Fact]
    public async Task Get_ExcludesDeletedEventsFromVenueTotals()
    {
        await using var db = CreateDb();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        AddInventory(db, firstId, 100, 80);
        AddInventory(db, secondId, 50, 20);
        AddInventory(db, deletedId, 1000, 0);
        await db.SaveChangesAsync();
        var from = new DateTime(2026, 10, 30);
        var to = from.AddDays(1);
        using var client = CreateClient(async (request, ct) =>
        {
            var sent = await request.Content!.ReadFromJsonAsync<CatalogRequest>(cancellationToken: ct);
            Assert.NotNull(sent);
            Assert.Equal(new[] { firstId, secondId, deletedId }.Order(), sent.EventIds.Order());
            Assert.Equal(new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc), sent.From);
            Assert.Equal(new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc), sent.To);
            return CatalogResponse(
                [new(firstId, venueId, "Venue", StartDate: from), new(secondId, venueId, "Venue", StartDate: from)], [deletedId]);
        });

        var result = await new VenueOccupancyController(new VenueOccupancyReport(db, client))
            .GetVenueOccupancy(from, to, default);

        var rows = Assert.IsAssignableFrom<IReadOnlyList<VenueOccupancyRow>>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal(new VenueOccupancyRow(venueId, "Venue", 150, 50, 33.33m), Assert.Single(rows));
    }

    [Fact]
    public async Task Get_ReturnsEmptyReportWhenAllEventsWereDeleted()
    {
        await using var db = CreateDb();
        var deletedId = Guid.NewGuid();
        AddInventory(db, deletedId, 100, 80);
        await db.SaveChangesAsync();
        using var client = CreateClient((_, _) => Task.FromResult(CatalogResponse([], [deletedId])));

        var rows = await new VenueOccupancyReport(db, client).Get(null, null, default);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Get_CatalogTimeoutReturnsBadGateway()
    {
        await using var db = CreateDb();
        AddInventory(db, Guid.NewGuid(), 100, 80);
        await db.SaveChangesAsync();
        using var client = CreateClient(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return CatalogResponse([], []);
        });
        client.Timeout = TimeSpan.FromMilliseconds(50);

        var result = await new VenueOccupancyController(new VenueOccupancyReport(db, client))
            .GetVenueOccupancy(null, null, default);

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, response.StatusCode);
        Assert.Equal(502, Assert.IsType<ProblemDetails>(response.Value).Status);
    }

    [Fact]
    public async Task Get_ClientCancellationPropagates()
    {
        await using var db = CreateDb();
        AddInventory(db, Guid.NewGuid(), 100, 80);
        await db.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        using var client = CreateClient(async (_, ct) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return CatalogResponse([], []);
        });
        var controller = new VenueOccupancyController(new VenueOccupancyReport(db, client));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            controller.GetVenueOccupancy(null, null, cancellation.Token));
    }

    [Fact]
    public async Task GetEvents_UsesSwedishCalendarDayAndKeepsSameNamedEventsSeparate()
    {
        const string day = "2026-10-25";
        const string start = "2026-10-24T22:00:00Z";
        const string end = "2026-10-25T23:00:00Z";
        await using var db = CreateDb();
        var startUtc = DateTime.Parse(start, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var endUtc = DateTime.Parse(end, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var venueId = Guid.NewGuid();
        var dates = new[] { startUtc.AddTicks(-1), startUtc, endUtc.AddTicks(-1), endUtc };
        var events = dates.Select(timestamp => new CatalogEvent(Guid.NewGuid(), venueId, "Venue",
            "Same name", timestamp)).ToArray();
        foreach (var item in events) AddInventory(db, item.EventId, 100, 75);
        await db.SaveChangesAsync();
        using var client = CreateClient(async (request, ct) =>
        {
            var sent = await request.Content!.ReadFromJsonAsync<CatalogRequest>(cancellationToken: ct);
            Assert.NotNull(sent);
            Assert.Equal(startUtc.Date, sent.From);
            Assert.Equal(endUtc.AddTicks(-1).Date, sent.To);
            // Model the catalog's inclusive UTC-day filtering.
            var matching = events.Where(item => item.StartDate >= sent.From
                && item.StartDate < sent.To!.Value.AddDays(1)).ToArray();
            return CatalogResponse(matching, []);
        });
        var date = DateTime.Parse(day);
        var report = new VenueOccupancyReport(db, client);

        var response = await new EventOccupancyController(report).GetEventOccupancy(date, date, default);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<EventOccupancyRow>>(
            Assert.IsType<OkObjectResult>(response).Value);

        Assert.Equal(new[] { events[1].EventId, events[2].EventId }, rows.Select(row => row.EventId));
        Assert.All(rows, row => { Assert.Equal("Same name", row.EventName); Assert.Equal(25m, row.OccupancyPercent); });
        var venue = Assert.Single(await report.Get(date, date, default));
        Assert.Equal(200, venue.Capacity);
        Assert.Equal(50, venue.Sold);
    }

    private static BookingDbContext CreateDb() => new(new DbContextOptionsBuilder<BookingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void AddInventory(BookingDbContext db, Guid eventId, int capacity, int available) =>
        db.EventInventory.Add(new Inventory
        {
            InventoryId = Guid.NewGuid(), EventId = eventId, SectionId = Guid.NewGuid(),
            TotalQuantity = capacity, AvailableQuantity = available, Price = 100m
        });

    private static HttpResponseMessage CatalogResponse(CatalogEvent[] events, Guid[] missingIds) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { Events = events, MissingEventIds = missingIds })
    };

    private static HttpClient CreateClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) =>
        new(new StubHandler(handle)) { BaseAddress = new Uri("http://eventcatalog/") };

    private sealed record CatalogRequest(Guid[] EventIds, DateTime? From, DateTime? To);
    private sealed record CatalogEvent(Guid EventId, Guid VenueId, string VenueName,
        string Name = "Concert", DateTime StartDate = default);
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handle(request, cancellationToken);
    }
}
