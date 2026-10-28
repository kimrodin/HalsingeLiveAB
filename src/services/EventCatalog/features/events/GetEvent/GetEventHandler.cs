using EventCatalog.Data;
using EventCatalog.Features.Events;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.GetEvent;

public class GetEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<GetEventResponse> Handle(GetEventRequest request)
    {
        EventValidation.ValidateId(request.EventId, nameof(request.EventId));

        var eventItem = await dbContext.Events
            .AsNoTracking()
            .Include(e => e.SectionPrices)
                .ThenInclude(sectionPrice => sectionPrice.Section)
            .Where(e => e.EventId == request.EventId)
            .FirstOrDefaultAsync();

        if (eventItem is null)
            return null;

        return new GetEventResponse(
            eventItem.EventId,
            eventItem.Name,
            eventItem.Description,
            eventItem.StartDate,
            eventItem.EndDate,
            eventItem.OnSaleFrom,
            eventItem.VenueId,
            eventItem.SectionPrices
                .Select(price => new EventSectionPriceResponse(
                    price.SectionId,
                    price.Price,
                    price.Section.SectionCapacity))
                .ToList());
    }
}
