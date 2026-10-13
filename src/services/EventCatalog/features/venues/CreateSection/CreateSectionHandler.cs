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
        VenueValidation.ValidateId(venueId, nameof(venueId));
        VenueValidation.ValidateSection(request.Name, request.Capacity, request.SeatingType);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var venue = await VenueCapacityRules.LockVenueAsync(dbContext, venueId, cancellationToken);

        if (venue is null)
            return null;

        var sectionCapacity = await VenueCapacityRules.GetSectionCapacityAsync(dbContext, venueId, cancellationToken);
        if (sectionCapacity + request.Capacity > venue.VenueCapacity)
            throw new VenueCapacityConflictException(
                "The combined section capacity cannot exceed the venue capacity.");

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
        await transaction.CommitAsync(cancellationToken);

        return new CreateSectionResponse(
            section.SectionId,
            section.VenueId,
            section.Name,
            section.SectionCapacity,
            section.SeatingType);
    }
}
