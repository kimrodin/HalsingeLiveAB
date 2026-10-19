using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateVenue;

public class CreateVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<CreateVenueResponse> Handle(CreateVenueRequest request, CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateVenue(request.Name, request.StreetAddress, request.PostalCode, request.City, request.Capacity);

        var venue = new Venue
        {
            VenueId = Guid.NewGuid(),
            VenueName = request.Name,
            VenueStreetAddress = request.StreetAddress,
            VenuePostalCode = request.PostalCode,
            VenueCity = request.City,
            VenueCapacity = request.Capacity,
        };

        dbContext.Venues.Add(venue);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateVenueResponse(
            venue.VenueId, venue.VenueName, venue.VenueStreetAddress,
            venue.VenuePostalCode, venue.VenueCity,
            venue.VenueCapacity);
    }
}
