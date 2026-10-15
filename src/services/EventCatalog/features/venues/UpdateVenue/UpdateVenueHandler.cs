using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.UpdateVenue;

public class UpdateVenueHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(UpdateVenueRequest request, CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateId(request.VenueId, nameof(request.VenueId));
        VenueValidation.ValidateVenue(request.Name, request.StreetAddress, request.PostalCode, request.City, request.Capacity);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var venue = await VenueCapacityRules.LockVenueAsync(dbContext, request.VenueId, cancellationToken);

        if (venue is null)
            return false;

        var sectionCapacity = await VenueCapacityRules.GetSectionCapacityAsync(dbContext, request.VenueId, cancellationToken);
        if (request.Capacity < sectionCapacity)
            throw new VenueCapacityConflictException(
                "The venue capacity cannot be less than the combined section capacity.");

        venue.VenueName = request.Name;
        venue.VenueStreetAddress = request.StreetAddress;
        venue.VenuePostalCode = request.PostalCode;
        venue.VenueCity = request.City;
        venue.VenueCapacity = request.Capacity;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
