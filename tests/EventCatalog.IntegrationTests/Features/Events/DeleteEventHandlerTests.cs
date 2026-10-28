using EventCatalog.Features.Events.DeleteEvent;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.IntegrationTests.Features.Events;

public sealed class DeleteEventHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    [Fact]
    public async Task Handle_DeletesOnlyTargetEventAndItsPrices()
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        var target = await database.CreateEventAsync(venue.VenueId, section.SectionId);
        var other = await database.CreateEventAsync(venue.VenueId, section.SectionId);
        await using var db = database.CreateContext();
        Assert.True(await new DeleteEventHandler(db).Handle(new(target)));
        await using var verify = database.CreateContext();
        Assert.False(await verify.Events.AnyAsync(e => e.EventId == target));
        Assert.False(await verify.EventSectionPrice.AnyAsync(p => p.EventId == target));
        Assert.True(await verify.Events.AnyAsync(e => e.EventId == other));
        Assert.True(await verify.EventSectionPrice.AnyAsync(p => p.EventId == other));
        Assert.True(await verify.Sections.AnyAsync(s => s.SectionId == section.SectionId));
        Assert.True(await verify.Venues.AnyAsync(v => v.VenueId == venue.VenueId));
    }

}
