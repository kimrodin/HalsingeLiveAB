using EventCatalog.Features.Venues.DeleteVenue;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.IntegrationTests.Features.Venues;

public sealed class DeleteVenueHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    [Fact]
    public async Task Handle_UnusedVenueCascadesSectionsAndSeatsButPreservesOtherVenues()
    {
        var venue = await database.CreateVenueAsync();
        var other = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        var seatId = Guid.NewGuid();
        await using (var seed = database.CreateContext())
        {
            seed.Seats.Add(new Seat { SeatId = seatId, SectionId = section.SectionId, Row = "A", Number = 1 });
            await seed.SaveChangesAsync();
        }
        await using var db = database.CreateContext();
        Assert.Equal(DeleteVenueResult.Deleted, await new DeleteVenueHandler(db).Handle(new(venue.VenueId)));
        await using var verify = database.CreateContext();
        Assert.False(await verify.Venues.AnyAsync(v => v.VenueId == venue.VenueId));
        Assert.False(await verify.Sections.AnyAsync(s => s.SectionId == section.SectionId));
        Assert.False(await verify.Seats.AnyAsync(s => s.SeatId == seatId));
        Assert.True(await verify.Venues.AnyAsync(v => v.VenueId == other.VenueId));
    }

    [Fact]
    public async Task Handle_VenueUsedByEventCannotBeDeleted()
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        var eventId = await database.CreateEventAsync(venue.VenueId, section.SectionId);
        await using var db = database.CreateContext();
        Assert.Equal(DeleteVenueResult.InUse, await new DeleteVenueHandler(db).Handle(new(venue.VenueId)));
        await using var verify = database.CreateContext();
        Assert.True(await verify.Venues.AnyAsync(v => v.VenueId == venue.VenueId));
        Assert.True(await verify.Events.AnyAsync(e => e.EventId == eventId));
        Assert.True(await verify.Sections.AnyAsync(s => s.SectionId == section.SectionId));
    }

}
