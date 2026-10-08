using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.ListVenues;

public class ListVenuesHandler(EventCatalogDbContext dbContext)
{
    public async Task<ListVenuesResponse> Handle()
    {
        var venues = await dbContext.Venues
            .OrderBy(v => v.VenueName)
            .ThenBy(v => v.VenueId)
            .Select(v => new VenueListItemResponseDTO
            {
                VenueId = v.VenueId,
                VenueName = v.VenueName,
                VenueAddress = v.VenueAddress,
                VenueCapacity = v.VenueCapacity,
                SeatingType = v.SeatingType
            })
            .ToListAsync();

        return new ListVenuesResponse { Venues = venues };
    }
}
