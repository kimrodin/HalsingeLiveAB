using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;

namespace EventCatalog.Features.Events.GetEvent;

public class GetEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<GetEventResponse> Handle(GetEventRequest request)
    {
        return await dbContext.Events
            .Where(e => e.EventId == request.EventId)
            .Select(e => new GetEventResponse(
                e.EventId,
                e.Name,
                e.Description,
                e.StartDate,
                e.EndDate,
                e.OnSaleFrom,
                e.VenueId,
                e.SectionPrices
                    .Select(price => new EventSectionPriceResponse(
                        price.SectionId,
                        price.Price))
                    .ToList()))
            .FirstOrDefaultAsync();
    }
}
