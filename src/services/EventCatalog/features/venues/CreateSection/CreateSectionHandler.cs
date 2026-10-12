using EventCatalog.Data;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.CreateSection;

public class CreateSectionHandler(EventCatalogDbContext dbContext)
{
    public async Task<CreateSectionResponse> Handle(
        Guid venueId,
        CreateSectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var venueExists = await dbContext.Venues
            .AnyAsync(venue => venue.VenueId == venueId, cancellationToken);

        if (!venueExists)
            return null;

        var section = new Section
        {
            SectionId = Guid.NewGuid(),
            VenueId = venueId,
            Name = request.Name,
            SectionCapacity = request.Capacity,
            SeatingType = request.SeatingType
        };

        dbContext.Sections.Add(section);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateSectionResponse(
            section.SectionId,
            section.VenueId,
            section.Name,
            section.SectionCapacity,
            section.SeatingType);
    }
}
