using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EventCatalog.Features.Events.UpdateEvent;

public class UpdateEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(UpdateEventRequest request, CancellationToken cancellationToken = default)
    {
        var sectionIds = request.SectionIdsToRemove ?? [];
        if (sectionIds.Any(id => id == Guid.Empty) || sectionIds.Distinct().Count() != sectionIds.Count)
            throw new ValidationException("Section IDs to remove must be non-empty and unique.");

        var eventItem = await dbContext.Events
            .Include(e => e.SectionPrices)
            .FirstOrDefaultAsync(e => e.EventId == request.EventId, cancellationToken);

        if (eventItem is null)
            return false;

        var linkedSectionIds = eventItem.SectionPrices.Select(price => price.SectionId).ToHashSet();
        if (sectionIds.Any(id => !linkedSectionIds.Contains(id)))
            throw new ValidationException("Every section to remove must be linked to this event.");

        // Radera endast detta evenemangs kopplingar. Sektionen, stolarna och andra
        // evenemangs priser finns kvar.
        dbContext.EventSectionPrice.RemoveRange(
            eventItem.SectionPrices.Where(price => sectionIds.Contains(price.SectionId)).ToArray());

        eventItem.Name = request.Name;
        eventItem.Description = request.Description;
        eventItem.StartDate = request.StartDate;
        eventItem.EndDate = request.EndDate;

        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
