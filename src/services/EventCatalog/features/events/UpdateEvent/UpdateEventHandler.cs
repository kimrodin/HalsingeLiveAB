using EventCatalog.Data;
using EventCatalog.Features.Events.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.UpdateEvent;

public class UpdateEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<UpdateEventResult> Handle(Guid eventId, UpdateEventRequest request)
    {
        var eventItem = await dbContext.Events
            .Include(e => e.SectionPrices)
            .FirstOrDefaultAsync(e => e.EventId == eventId);

        if (eventItem is null)
            return UpdateEventResult.EventNotFound;

        if (request.SectionPrices is null || request.SectionPrices.Count == 0)
            return UpdateEventResult.InvalidSectionPrices;

        var requestedPrices = request.SectionPrices;
        if (requestedPrices.Any(p => p is null
                || p.SectionId is null
                || p.SectionId == Guid.Empty
                || p.Price is null
                || p.Price < 0))
        {
            return UpdateEventResult.InvalidSectionPrices;
        }

        var sectionIds = requestedPrices
            .Select(p => p.SectionId!.Value)
            .ToArray();

        if (sectionIds.Distinct().Count() != sectionIds.Length)
            return UpdateEventResult.InvalidSectionPrices;

        var matchingSectionCount = await dbContext.Sections
            .CountAsync(section => section.VenueId == request.VenueId
                && sectionIds.Contains(section.SectionId));

        if (matchingSectionCount != sectionIds.Length)
            return UpdateEventResult.InvalidSectionPrices;

        eventItem.Name = request.Name;
        eventItem.Description = request.Description;
        eventItem.StartDate = request.StartDate;
        eventItem.EndDate = request.EndDate;
        eventItem.VenueId = request.VenueId;
        eventItem.OnSaleFrom = request.OnSaleFrom;

        var existingPrices = eventItem.SectionPrices.ToDictionary(p => p.SectionId);
        var requestedSectionIdSet = sectionIds.ToHashSet();

        foreach (var existingPrice in eventItem.SectionPrices
                     .Where(p => !requestedSectionIdSet.Contains(p.SectionId)))
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

        await dbContext.SaveChangesAsync();

        return UpdateEventResult.Updated;
    }
}
