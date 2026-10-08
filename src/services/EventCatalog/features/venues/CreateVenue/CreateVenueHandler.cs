using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateVenue;

public class CreateVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<CreateVenueResponse> Handle(CreateVenueRequest request)
    {
        var venue = new Venue
        {
            VenueId = Guid.NewGuid(),
            VenueName = request.Name,
            VenueAddress = request.Address,
            VenueCapacity = request.Capacity,
            SeatingType = request.SeatingType
        };

        dbContext.Venues.Add(venue);
        await dbContext.SaveChangesAsync();

        return new CreateVenueResponse(
            venue.VenueId, venue.VenueName, venue.VenueAddress,
            venue.VenueCapacity, venue.SeatingType);
    }
}
