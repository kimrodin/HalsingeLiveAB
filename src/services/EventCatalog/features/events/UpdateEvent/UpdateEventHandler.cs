using EventCatalog.Data;
using EventCatalog.Features.Events;
using EventCatalog.Features.Events.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.UpdateEvent;

public class UpdateEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<UpdateEventResult> Handle(Guid eventId, UpdateEventRequest request, CancellationToken cancellationToken = default)
    {
        EventValidation.ValidateId(eventId, nameof(eventId));
        EventValidation.ValidateUpdateEvent(request);

        var eventItem = await dbContext.Events
            .Include(e => e.SectionPrices)
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

        if (eventItem is null)
            return UpdateEventResult.EventNotFound;

        var requestedPrices = request.SectionPrices;
        var sectionIds = requestedPrices
            .Select(p => p.SectionId!.Value)
            .ToArray();

        var removeIds = request.SectionIdsToRemove ?? Array.Empty<Guid>();
        var linkedSectionIds = eventItem.SectionPrices.Select(price => price.SectionId).ToHashSet();
        EventValidation.ValidateRemovedSectionPrices(removeIds, linkedSectionIds);

        EventValidation.ValidateNoSectionPriceIsUpdatedAndRemoved(sectionIds, removeIds);

        var remainingSectionIds = linkedSectionIds
            .Except(removeIds)
            .Concat(sectionIds)
            .Distinct()
            .ToArray();
        var matchingSectionCount = await dbContext.Sections
            .CountAsync(section => section.VenueId == request.VenueId
                && remainingSectionIds.Contains(section.SectionId), cancellationToken);

        EventValidation.ValidateSectionPricesBelongToVenue(
            matchingSectionCount == remainingSectionIds.Length);

        // Radera endast detta evenemangs kopplingar. Sektionen, stolarna och andra
        // evenemangs priser finns kvar.

        eventItem.Name = request.Name;
        eventItem.Description = request.Description;
        eventItem.StartDate = request.StartDate;
        eventItem.EndDate = request.EndDate;
        eventItem.VenueId = request.VenueId;
        eventItem.OnSaleFrom = request.OnSaleFrom;

        var existingPrices = eventItem.SectionPrices.ToDictionary(p => p.SectionId);

        foreach (var existingPrice in eventItem.SectionPrices
                     .Where(p => removeIds.Contains(p.SectionId)))
        {
            dbContext.EventSectionPrice.Remove(existingPrice);
        }

        foreach (var requestedPrice in requestedPrices)
        {
            var sectionId = requestedPrice.SectionId!.Value;
            if (existingPrices.TryGetValue(sectionId, out var existingPrice))
            {
                existingPrice.Price = requestedPrice.Price!.Value;
            }
            else
            {
                eventItem.SectionPrices.Add(new EventSectionPrice
                {
                    EventId = eventItem.EventId,
                    SectionId = sectionId,
                    Price = requestedPrice.Price!.Value
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return UpdateEventResult.Updated;
    }

}
