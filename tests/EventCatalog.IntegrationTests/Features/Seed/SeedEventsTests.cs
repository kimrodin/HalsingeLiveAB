using EventCatalog.Features.Events.CreateEvent;
using EventCatalog.Features.Seed.SeedEvents;
using EventCatalog.Features.Seed.SeedVenues;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventCatalog.IntegrationTests.Features.Seed;

public sealed class SeedEventsTests(EventCatalogDatabaseFixture database) : IClassFixture<EventCatalogDatabaseFixture>
{
    [Fact]
    public async Task Seed_PreservesOriginalIdsAddsEveryMonthOf2026AndCanBeRerun()
    {
        var client = new InventoryClient();
        await using (var db = database.CreateContext())
        {
            await new SeedVenuesHandler(db).Handle(default);
            var seeded = await new SeedEventsHandler(db, client, NullLogger<SeedEventsHandler>.Instance).Handle(default);
            Assert.Equal(40, seeded.CreatedCount);
            Assert.All(seeded.Events, item => Assert.True(item.InventoryProvisioned));
        }
        await using (var db = database.CreateContext())
        {
            var again = await new SeedEventsHandler(db, client, NullLogger<SeedEventsHandler>.Instance).Handle(default);
            Assert.Equal(0, again.CreatedCount);
            var events = await db.Events.ToArrayAsync();
            Assert.Equal(40, events.Length);
            Assert.Equal(30, events.Count(item => item.StartDate.Year == 2026));
            Assert.Equal(Enumerable.Range(1, 12), events.Where(item => item.StartDate.Year == 2026)
                .Select(item => item.StartDate.Month).Distinct().Order());
            Assert.Equal(new DateTime(2027, 1, 16, 19, 0, 0, DateTimeKind.Utc),
                events.Single(item => item.EventId == Guid.Parse("10000000-0000-0000-0000-000000000001")).StartDate);
        }
    }
    private sealed class InventoryClient : IBookingInventoryClient
    {
        public Task EnsureInventoryCreatedAsync(Guid eventId, CancellationToken ct) => Task.CompletedTask;
    }
}
