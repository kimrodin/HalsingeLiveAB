using EventCatalog.Data;
using EventCatalog.Features.Venues;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.GetVenue;

public class GetVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<GetVenueResponse> Handle(GetVenueRequest request)
    {
        VenueValidation.ValidateId(request.VenueId, nameof(request.VenueId));

        return await dbContext.Venues
            .Where(v => v.VenueId == request.VenueId)
            .Select(v => new GetVenueResponse(
                v.VenueId, v.VenueName, v.VenueStreetAddress,
                v.VenuePostalCode, v.VenueCity,
                v.VenueCapacity,
                v.VenueSections
                    .OrderBy(section => section.Name)
                    .ThenBy(section => section.SectionId)
                    .Select(section => new VenueSectionResponse(
                        section.SectionId,
                        section.Name,
                        section.SectionCapacity,
                        section.SeatingType))
                    .ToList()))
            .FirstOrDefaultAsync();
    }
}
