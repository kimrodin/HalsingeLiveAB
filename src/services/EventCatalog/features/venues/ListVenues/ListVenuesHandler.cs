using EventCatalog.Data;
using EventCatalog.Features.Venues;
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
                VenueStreetAddress = v.VenueStreetAddress,
                VenuePostalCode = v.VenuePostalCode,
                VenueCity = v.VenueCity,
                VenueCapacity = v.VenueCapacity,
                Sections = v.VenueSections
                    .OrderBy(section => section.Name)
                    .ThenBy(section => section.SectionId)
                    .Select(section => new VenueSectionResponse(
                        section.SectionId,
                        section.Name,
                        section.SectionCapacity,
                        section.SeatingType))
                    .ToList()
            })
            .ToListAsync();

        return new ListVenuesResponse { Venues = venues };
    }
}
