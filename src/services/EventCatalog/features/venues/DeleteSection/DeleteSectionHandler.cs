using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventCatalog.Features.Venues.DeleteSection;

public class DeleteSectionHandler(EventCatalogDbContext dbContext)
{
    public async Task<DeleteSectionResult> Handle(
        DeleteSectionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validera innan databasen anropas, även när handlern används utan controller.
        if (request.VenueId == Guid.Empty)
            return DeleteSectionResult.InvalidVenueId;

        if (request.SectionId == Guid.Empty)
            return DeleteSectionResult.InvalidSectionId;

        var section = await dbContext.Sections
            .FirstOrDefaultAsync(
                section => section.SectionId == request.SectionId
                    && section.VenueId == request.VenueId,
                cancellationToken);

        if (section is null)
            return DeleteSectionResult.NotFound;

        if (await dbContext.EventSectionPrice.AnyAsync(
            price => price.SectionId == request.SectionId, cancellationToken))
            return DeleteSectionResult.InUse;

        dbContext.Sections.Remove(section);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation,
                ConstraintName: "FK_EventSectionPrice_Sections_SectionId"
            })
        {
            // En ny evenemangskoppling kan ha skapats efter kontrollen ovan.
            dbContext.Entry(section).State = EntityState.Unchanged;
            return DeleteSectionResult.InUse;
        }

        return DeleteSectionResult.Deleted;
    }
}

public enum DeleteSectionResult
{
    Deleted,
    NotFound,
    InUse,
    InvalidVenueId,
    InvalidSectionId
}
