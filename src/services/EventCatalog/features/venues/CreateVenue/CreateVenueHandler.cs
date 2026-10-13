using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateVenue;

public class CreateVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<CreateVenueResponse> Handle(CreateVenueRequest request, CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateVenue(request.Name, request.Address, request.Capacity);

        var venue = new Venue
        {
            VenueId = Guid.NewGuid(),
            VenueName = request.Name,
            VenueAddress = request.Address,
            VenueCapacity = request.Capacity,
        };

        dbContext.Venues.Add(venue);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateVenueResponse(
            venue.VenueId, venue.VenueName, venue.VenueAddress,
            venue.VenueCapacity);
    }
}
