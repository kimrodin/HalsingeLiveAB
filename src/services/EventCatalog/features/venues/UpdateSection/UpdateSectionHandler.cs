using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues.UpdateSection;

public class UpdateSectionHandler(EventCatalogDbContext dbContext)
{
    public async Task<UpdateSectionResult> Handle(
        Guid venueId,
        Guid sectionId,
        UpdateSectionRequest request,
        CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateId(venueId, "VenueId");
        VenueValidation.ValidateId(sectionId, "SectionId");
        VenueValidation.ValidateSection(request.Name, request.Capacity, request.SeatingType);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var venue = await VenueCapacityRules.LockVenueAsync(dbContext, venueId, cancellationToken);
        if (venue is null)
            return UpdateSectionResult.NotFound;

        // Lås även sektionen. Nya evenemangskopplingar måste då vänta tills
        // kontrollen av användning och uppdateringen har slutförts.
        var section = await dbContext.Sections
            .FromSqlInterpolated($"SELECT * FROM \"Sections\" WHERE \"SectionId\" = {sectionId} FOR UPDATE")
            .SingleOrDefaultAsync(section => section.VenueId == venueId, cancellationToken);
        if (section is null)
            return UpdateSectionResult.NotFound;

        var capacityChanged = section.SectionCapacity != request.Capacity;
        var seatingChanged = section.SeatingType != request.SeatingType;

        if ((capacityChanged || seatingChanged) && await dbContext.EventSectionPrice
            .AnyAsync(price => price.SectionId == sectionId, cancellationToken))
            return UpdateSectionResult.InUse;

        if (capacityChanged || seatingChanged)
        {
            var seatCount = await dbContext.Seats.CountAsync(seat => seat.SectionId == sectionId, cancellationToken);
            if (request.Capacity < seatCount || (seatingChanged && seatCount > 0))
                return UpdateSectionResult.SeatConflict;
        }

        var totalCapacity = await VenueCapacityRules.GetSectionCapacityAsync(dbContext, venueId, cancellationToken);
        if (totalCapacity - section.SectionCapacity + request.Capacity > venue.VenueCapacity)
            return UpdateSectionResult.CapacityExceeded;

        section.Name = request.Name;
        section.SectionCapacity = request.Capacity;
        section.SeatingType = request.SeatingType;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UpdateSectionResult.Updated;
    }
}

public enum UpdateSectionResult
{
    Updated,
    NotFound,
    InUse,
    CapacityExceeded,
    SeatConflict
}
