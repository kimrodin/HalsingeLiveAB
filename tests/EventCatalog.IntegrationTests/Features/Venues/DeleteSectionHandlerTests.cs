using EventCatalog.Features.Venues.DeleteSection;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.IntegrationTests.Features.Venues;

public sealed class DeleteSectionHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    [Fact]
    public async Task Handle_UnusedSectionDeletesItsSeatsButPreservesVenueAndOtherSections()
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        var other = await database.CreateSectionAsync(venue.VenueId);
        var seatId = Guid.NewGuid();
        await using (var seed = database.CreateContext())
        {
            seed.Seats.Add(new Seat { SeatId = seatId, SectionId = section.SectionId, Row = "A", Number = 1 });
            await seed.SaveChangesAsync();
        }
        await using var db = database.CreateContext();
        Assert.Equal(DeleteSectionResult.Deleted,
            await new DeleteSectionHandler(db).Handle(new(venue.VenueId, section.SectionId)));
        await using var verify = database.CreateContext();
        Assert.False(await verify.Sections.AnyAsync(s => s.SectionId == section.SectionId));
        Assert.False(await verify.Seats.AnyAsync(s => s.SeatId == seatId));
        Assert.True(await verify.Venues.AnyAsync(v => v.VenueId == venue.VenueId));
        Assert.True(await verify.Sections.AnyAsync(s => s.SectionId == other.SectionId));
    }

    [Fact]
    public async Task Handle_SectionUsedByEventCannotBeDeleted()
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        var eventId = await database.CreateEventAsync(venue.VenueId, section.SectionId);
        await using var db = database.CreateContext();
        Assert.Equal(DeleteSectionResult.InUse,
            await new DeleteSectionHandler(db).Handle(new(venue.VenueId, section.SectionId)));
        await using var verify = database.CreateContext();
        Assert.True(await verify.Sections.AnyAsync(s => s.SectionId == section.SectionId));
        Assert.True(await verify.EventSectionPrice.AnyAsync(p => p.EventId == eventId && p.SectionId == section.SectionId));
    }

    [Fact]
    public async Task Handle_SectionFromAnotherVenueReturnsNotFoundAndIsPreserved()
    {
        var venue = await database.CreateVenueAsync();
        var other = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        await using var db = database.CreateContext();
        Assert.Equal(DeleteSectionResult.NotFound,
            await new DeleteSectionHandler(db).Handle(new(other.VenueId, section.SectionId)));
        await using var verify = database.CreateContext();
        Assert.True(await verify.Sections.AnyAsync(s => s.SectionId == section.SectionId));
    }

}
