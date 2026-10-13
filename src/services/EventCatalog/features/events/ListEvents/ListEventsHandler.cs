using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.ListEvents;

public class ListEventsHandler(EventCatalogDbContext dbContext)
{
    public async Task<ListEventsResponse> Handle()
    {
        var events = await dbContext.Events
            .AsNoTracking()
            .Include(e => e.SectionPrices)
            .OrderBy(e => e.StartDate)
            .ThenBy(e => e.EventId)
            .ToListAsync();

        return new ListEventsResponse
        {
            Events = events.Select(eventItem => new EventListItemResponseDTO
            {
                EventId = eventItem.EventId,
                EventName = eventItem.Name,
                EventDescription = eventItem.Description,
                EventDate = eventItem.StartDate,
                VenueId = eventItem.VenueId,
                SectionPrices = eventItem.SectionPrices
                    .Select(price => new SectionPriceResponseDTO
                    {
                        SectionId = price.SectionId,
                        Price = price.Price
                    })
                    .ToList()
            }).ToList()
        };
    }
}
