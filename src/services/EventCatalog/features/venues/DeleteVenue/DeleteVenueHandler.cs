using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.DeleteVenue;

public class DeleteVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<DeleteVenueResult> Handle(DeleteVenueRequest request)
    {
        VenueValidation.ValidateId(request.VenueId, nameof(request.VenueId));

        var venue = await dbContext.Venues
            .FirstOrDefaultAsync(v => v.VenueId == request.VenueId);

        if (venue is null)
            return DeleteVenueResult.NotFound;

        if (await dbContext.Events.AnyAsync(e => e.VenueId == request.VenueId))
            return DeleteVenueResult.InUse;

        dbContext.Venues.Remove(venue);

        await dbContext.SaveChangesAsync();
        return DeleteVenueResult.Deleted;
    }
}

public enum DeleteVenueResult
{
    Deleted,
    NotFound,
    InUse
}
