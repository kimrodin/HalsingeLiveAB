using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.UpdateVenue;

public class UpdateVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(UpdateVenueRequest request)
    {
        var venue = await dbContext.Venues
            .FirstOrDefaultAsync(v => v.VenueId == request.VenueId);

        if (venue is null)
            return false;

        venue.VenueName = request.Name;
        venue.VenueAddress = request.Address;
        venue.VenueCapacity = request.Capacity;
        venue.SeatingType = request.SeatingType;

        await dbContext.SaveChangesAsync();
        return true;
    }
}
