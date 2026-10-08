using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.DeleteVenue;

public class DeleteVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(DeleteVenueRequest request)
    {
        var venue = await dbContext.Venues
            .FirstOrDefaultAsync(v => v.VenueId == request.VenueId);

        if (venue is null)
            return false;

        if (await dbContext.Events.AnyAsync(e => e.VenueId == request.VenueId))
            throw new InvalidOperationException("The venue is used by events and cannot be deleted.");

        dbContext.Venues.Remove(venue);

        await dbContext.SaveChangesAsync();
        return true;
    }
}
