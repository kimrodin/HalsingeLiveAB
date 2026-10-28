using Microsoft.EntityFrameworkCore;

namespace EventCatalog.IntegrationTests.Features.Venues;

public sealed class CreateVenueHandlerTests(EventCatalogDatabaseFixture database)
    : IClassFixture<EventCatalogDatabaseFixture>
{
    [Fact]
    public async Task Handle_PersistsVenueWithAddressAndCapacity()
    {
        var created = await database.CreateVenueAsync(250);
        await using var verify = database.CreateContext();
        var stored = await verify.Venues.SingleAsync(v => v.VenueId == created.VenueId);
        Assert.NotEqual(Guid.Empty, stored.VenueId);
        Assert.Equal("Arena", stored.VenueName);
        Assert.Equal("Main Street 1", stored.VenueStreetAddress);
        Assert.Equal("12345", stored.VenuePostalCode);
        Assert.Equal("Hudiksvall", stored.VenueCity);
        Assert.Equal(250, stored.VenueCapacity);
    }
}
