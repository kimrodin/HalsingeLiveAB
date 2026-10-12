using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.DeleteSection;

public class DeleteSectionHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(
        DeleteSectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var section = await dbContext.Sections
            .FirstOrDefaultAsync(
                section => section.SectionId == request.SectionId
                    && section.VenueId == request.VenueId,
                cancellationToken);

        if (section is null)
            return false;

        dbContext.Sections.Remove(section);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
