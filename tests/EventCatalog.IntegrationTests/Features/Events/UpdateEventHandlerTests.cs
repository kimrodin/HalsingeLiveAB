using EventCatalog.Features.Events;
using EventCatalog.Features.Events.UpdateEvent;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.IntegrationTests.Features.Events;

public sealed class UpdateEventHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    private static UpdateEventRequest Request(Guid venueId, Guid sectionId) => new(
        "Renamed concert", "New description", new DateTime(2030, 7, 1, 18, 0, 0, DateTimeKind.Utc),
        new DateTime(2030, 7, 1, 20, 0, 0, DateTimeKind.Utc), venueId,
        new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc), [new(sectionId, 250m)]);

    [Fact]
    public async Task Handle_UpdatesDetailsAndPricesAndRemovesOnlyRequestedLinks()
    {
        var venue = await database.CreateVenueAsync();
        var first = await database.CreateSectionAsync(venue.VenueId, 20);
        var removed = await database.CreateSectionAsync(venue.VenueId, 20);
        var added = await database.CreateSectionAsync(venue.VenueId, 20);
        var target = await database.CreateEventAsync(venue.VenueId, first.SectionId);
        var other = await database.CreateEventAsync(venue.VenueId, removed.SectionId);
        await using (var seed = database.CreateContext())
        {
            seed.EventSectionPrice.Add(new() { EventId = target, SectionId = removed.SectionId, Price = 50m });
            await seed.SaveChangesAsync();
        }
        var request = Request(venue.VenueId, first.SectionId) with
        {
            SectionPrices = [new(first.SectionId, 250m), new(added.SectionId, 0m)],
            SectionIdsToRemove = [removed.SectionId]
        };
        await using var db = database.CreateContext();
        Assert.Equal(UpdateEventResult.Updated, await new UpdateEventHandler(db).Handle(target, request));
        await using var verify = database.CreateContext();
        var stored = await verify.Events.Include(e => e.SectionPrices).SingleAsync(e => e.EventId == target);
        Assert.Equal(request.Name, stored.Name);
        Assert.Equal(request.Description, stored.Description);
        Assert.Equal(request.StartDate, stored.StartDate);
        Assert.Equal(request.EndDate, stored.EndDate);
        Assert.Equal(request.OnSaleFrom, stored.OnSaleFrom);
        Assert.Equal(2, stored.SectionPrices.Count);
        Assert.Equal(250m, stored.SectionPrices.Single(p => p.SectionId == first.SectionId).Price);
        Assert.Equal(0m, stored.SectionPrices.Single(p => p.SectionId == added.SectionId).Price);
        Assert.True(await verify.Sections.AnyAsync(s => s.SectionId == removed.SectionId));
        Assert.True(await verify.EventSectionPrice.AnyAsync(p => p.EventId == other && p.SectionId == removed.SectionId));
    }

    [Theory]
    [InlineData("foreign-section")]
    [InlineData("unlinked-removal")]
    [InlineData("update-and-remove")]
    public async Task Handle_InvalidSectionChangesLeavePersistedEventUnchanged(string scenario)
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        var target = await database.CreateEventAsync(venue.VenueId, section.SectionId);
        var request = Request(venue.VenueId, section.SectionId);
        if (scenario == "foreign-section")
        {
            var otherVenue = await database.CreateVenueAsync();
            var otherSection = await database.CreateSectionAsync(otherVenue.VenueId);
            request = request with { SectionPrices = [new(otherSection.SectionId, 1m)] };
        }
        else request = request with
        {
            SectionIdsToRemove = [scenario == "unlinked-removal" ? Guid.NewGuid() : section.SectionId]
        };
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<EventValidationException>(() => new UpdateEventHandler(db).Handle(target, request));
        await using var verify = database.CreateContext();
        var stored = await verify.Events.Include(e => e.SectionPrices).SingleAsync(e => e.EventId == target);
        Assert.Equal("Concert", stored.Name);
        var price = Assert.Single(stored.SectionPrices);
        Assert.Equal(section.SectionId, price.SectionId);
        Assert.Equal(125m, price.Price);
    }
}
