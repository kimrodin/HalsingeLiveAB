using EventCatalog.Features.Events.CreateEvent;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.IntegrationTests.Features.Events;

public sealed class CreateEventHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    [Fact]
    public async Task Handle_PersistsEventAndSectionPrice()
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        var request = EventCatalogDatabaseFixture.EventRequest(venue.VenueId, section.SectionId);
        await using var db = database.CreateContext();
        var created = await new CreateEventHandler(db).Handle(request, CancellationToken.None);
        await using var verify = database.CreateContext();
        var stored = await verify.Events.Include(e => e.SectionPrices).SingleAsync(e => e.EventId == created.EventId);
        Assert.Equal(request.Name, stored.Name);
        Assert.Equal(request.Description, stored.Description);
        Assert.Equal(request.VenueId, stored.VenueId);
        Assert.Equal(request.StartDate, stored.StartDate);
        Assert.Equal(request.EndDate, stored.EndDate);
        Assert.Equal(request.OnSaleFrom, stored.OnSaleFrom);
        var price = Assert.Single(stored.SectionPrices);
        Assert.Equal(section.SectionId, price.SectionId);
        Assert.Equal(125m, price.Price);
    }

    [Theory]
    [InlineData("missing-venue")]
    [InlineData("foreign-section")]
    public async Task Handle_MissingOrMismatchedReferencesDoNotCreateEvent(string scenario)
    {
        var venue = await database.CreateVenueAsync();
        var sectionVenue = scenario == "foreign-section" ? await database.CreateVenueAsync() : venue;
        var section = await database.CreateSectionAsync(sectionVenue.VenueId);
        var request = EventCatalogDatabaseFixture.EventRequest(
            scenario == "missing-venue" ? Guid.NewGuid() : venue.VenueId,
            section.SectionId);
        await using var db = database.CreateContext();
        var count = await db.Events.CountAsync();
        Assert.Null(await new CreateEventHandler(db).Handle(request, CancellationToken.None));
        await using var verify = database.CreateContext();
        Assert.Equal(count, await verify.Events.CountAsync());
    }
}
