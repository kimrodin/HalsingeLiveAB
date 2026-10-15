using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.ListEvents;

public class ListEventsHandler(EventCatalogDbContext dbContext)
{
    public async Task<ListEventsResponse> Handle(
        ListEventsRequest request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);

        var query = dbContext.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EventName))
        {
            var eventName = $"%{request.EventName.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.Name, eventName));
        }

        if (request.VenueId.HasValue)
            query = query.Where(e => e.VenueId == request.VenueId.Value);

        if (request.StartAfter.HasValue)
            query = query.Where(e => e.StartDate >= request.StartAfter.Value);

        if (request.StartsBefore.HasValue)
            query = query.Where(e => e.StartDate <= request.StartsBefore.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var events = await query
            .OrderBy(e => e.StartDate)
            .ThenBy(e => e.EventId)
            .Skip(skip)
            .Take(pageSize)
            .Select(eventItem => new EventListItemResponseDTO
            {
                EventId = eventItem.EventId,
                EventName = eventItem.Name,
                EventDescription = eventItem.Description,
                StartDate = eventItem.StartDate,
                EndDate = eventItem.EndDate,
                OnSaleFrom = eventItem.OnSaleFrom,
                VenueId = eventItem.VenueId,
                SectionPrices = eventItem.SectionPrices
                    .Select(price => new SectionPriceResponseDTO
                    {
                        SectionId = price.SectionId,
                        Price = price.Price
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new ListEventsResponse
        {
            Events = events,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
