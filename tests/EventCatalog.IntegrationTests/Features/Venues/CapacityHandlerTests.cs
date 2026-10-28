using EventCatalog.Features.Venues;
using EventCatalog.Features.Venues.CreateSection;
using EventCatalog.Features.Venues.Models;
using EventCatalog.Features.Venues.UpdateSection;
using EventCatalog.Features.Venues.UpdateVenue;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.IntegrationTests.Features.Venues;

public sealed class CapacityHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    [Theory]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public async Task CreateSection_AcceptsExactRemainingCapacityButRejectsOneAbove(int capacity, bool accepted)
    {
        var venue = await database.CreateVenueAsync();
        await database.CreateSectionAsync(venue.VenueId, 40);
        await using var db = database.CreateContext();
        var handler = new CreateSectionHandler(db);
        if (accepted)
        {
            var created = await handler.Handle(venue.VenueId, new("Balcony", capacity, SeatingType.AssignedSeating));
            Assert.Equal(capacity, created.Capacity);
        }
        else
            await Assert.ThrowsAsync<VenueCapacityConflictException>(() =>
                handler.Handle(venue.VenueId, new("Balcony", capacity, SeatingType.AssignedSeating)));

        await using var verify = database.CreateContext();
        Assert.Equal(accepted ? 100 : 40,
            await verify.Sections.Where(s => s.VenueId == venue.VenueId).SumAsync(s => s.SectionCapacity));
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(59, false)]
    public async Task UpdateVenue_AcceptsSectionSumButRejectsCapacityBelowIt(int capacity, bool accepted)
    {
        var venue = await database.CreateVenueAsync();
        await database.CreateSectionAsync(venue.VenueId, 40);
        await database.CreateSectionAsync(venue.VenueId, 20);
        await using var db = database.CreateContext();
        var handler = new UpdateVenueHandler(db);
        var request = new UpdateVenueRequest(venue.VenueId, "Renamed", "New Street", "99999", "New City", capacity);
        if (accepted) Assert.True(await handler.Handle(request));
        else await Assert.ThrowsAsync<VenueCapacityConflictException>(() => handler.Handle(request));

        await using var verify = database.CreateContext();
        var stored = await verify.Venues.SingleAsync(v => v.VenueId == venue.VenueId);
        Assert.Equal(accepted ? capacity : 100, stored.VenueCapacity);
        Assert.Equal(accepted ? "Renamed" : "Arena", stored.VenueName);
        Assert.Equal(accepted ? "New Street" : "Main Street 1", stored.VenueStreetAddress);
        Assert.Equal(accepted ? "99999" : "12345", stored.VenuePostalCode);
        Assert.Equal(accepted ? "New City" : "Hudiksvall", stored.VenueCity);
    }

    [Theory]
    [InlineData(60, UpdateSectionResult.Updated)]
    [InlineData(61, UpdateSectionResult.CapacityExceeded)]
    public async Task UpdateSection_CountsOtherSectionsWithoutCountingItselfTwice(int capacity, UpdateSectionResult expected)
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId, 20);
        await database.CreateSectionAsync(venue.VenueId, 40);
        await using var db = database.CreateContext();
        Assert.Equal(expected, await new UpdateSectionHandler(db).Handle(venue.VenueId, section.SectionId,
            new("Renamed", capacity, SeatingType.GeneralAdmission)));
        await using var verify = database.CreateContext();
        var stored = await verify.Sections.SingleAsync(s => s.SectionId == section.SectionId);
        Assert.Equal(expected == UpdateSectionResult.Updated ? capacity : 20, stored.SectionCapacity);
        Assert.Equal(expected == UpdateSectionResult.Updated ? "Renamed" : "Floor", stored.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateSection_UsedSectionAllowsRenamingButRejectsCapacityChanges(bool changeCapacity)
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        await database.CreateEventAsync(venue.VenueId, section.SectionId);
        await using var db = database.CreateContext();
        Assert.Equal(changeCapacity ? UpdateSectionResult.InUse : UpdateSectionResult.Updated,
            await new UpdateSectionHandler(db).Handle(venue.VenueId, section.SectionId,
                new("Renamed", changeCapacity ? 41 : 40, SeatingType.GeneralAdmission)));
        await using var verify = database.CreateContext();
        var stored = await verify.Sections.SingleAsync(s => s.SectionId == section.SectionId);
        Assert.Equal(40, stored.SectionCapacity);
        Assert.Equal(changeCapacity ? "Floor" : "Renamed", stored.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateSection_ExistingSeatsPreventCapacityBelowSeatCountAndSeatingTypeChanges(bool changeSeating)
    {
        var venue = await database.CreateVenueAsync();
        var section = await database.CreateSectionAsync(venue.VenueId);
        await using (var seed = database.CreateContext())
        {
            seed.Seats.AddRange(
                new Seat { SeatId = Guid.NewGuid(), SectionId = section.SectionId, Row = "A", Number = 1 },
                new Seat { SeatId = Guid.NewGuid(), SectionId = section.SectionId, Row = "A", Number = 2 });
            await seed.SaveChangesAsync();
        }
        await using var db = database.CreateContext();
        Assert.Equal(UpdateSectionResult.SeatConflict, await new UpdateSectionHandler(db).Handle(
            venue.VenueId, section.SectionId, new("Renamed", changeSeating ? 40 : 1,
                changeSeating ? SeatingType.AssignedSeating : SeatingType.GeneralAdmission)));
        await using var verify = database.CreateContext();
        var stored = await verify.Sections.SingleAsync(s => s.SectionId == section.SectionId);
        Assert.Equal(40, stored.SectionCapacity);
        Assert.Equal(SeatingType.GeneralAdmission, stored.SeatingType);
        Assert.Equal("Floor", stored.Name);
        Assert.Equal(2, await verify.Seats.CountAsync(s => s.SectionId == section.SectionId));
    }
}
