using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.GetVenue;

public class GetVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<GetVenueResponse> Handle(GetVenueRequest request)
    {
        return await dbContext.Venues
            .Where(v => v.VenueId == request.VenueId)
            .Select(v => new GetVenueResponse(
                v.VenueId, v.VenueName, v.VenueAddress,
                v.VenueCapacity, v.SeatingType))
            .FirstOrDefaultAsync();
    }
}
