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
            .Include(e => e.Image)
            .Include(e => e.Venue)
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
                .ToList(),
            eventItem.Image is null ? null : $"/DownloadEventImage/{eventItem.EventId}/{eventItem.Image.EventImageId}",
            eventItem.Venue.VenueName);
    }

    public async Task<GetEventsResponse> HandleMany(
        IReadOnlyCollection<Guid> eventIds, DateTime? from, DateTime? to,
        CancellationToken cancellationToken)
    {
        var existingIds = await dbContext.Events.AsNoTracking()
            .Where(e => eventIds.Contains(e.EventId))
            .Select(e => e.EventId)
            .ToArrayAsync(cancellationToken);
        var query = dbContext.Events.AsNoTracking().Where(e => eventIds.Contains(e.EventId));
        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            query = query.Where(e => e.StartDate >= fromUtc);
        }
        if (to.HasValue)
        {
            var toUtcExclusive = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(e => e.StartDate < toUtcExclusive);
        }

        var events = await query
            .Include(e => e.Image)
            .Include(e => e.Venue)
            .Include(e => e.SectionPrices).ThenInclude(price => price.Section)
            .ToListAsync(cancellationToken);

        var responses = events.Select(eventItem => new GetEventResponse(
            eventItem.EventId, eventItem.Name, eventItem.Description, eventItem.StartDate,
            eventItem.EndDate, eventItem.OnSaleFrom, eventItem.VenueId,
            eventItem.SectionPrices.Select(price => new EventSectionPriceResponse(
                price.SectionId, price.Price, price.Section.SectionCapacity)).ToArray(),
            eventItem.Image is null ? null : $"/DownloadEventImage/{eventItem.EventId}/{eventItem.Image.EventImageId}",
            eventItem.Venue.VenueName)).ToArray();
        var missingIds = eventIds.Except(existingIds).ToArray();
        return new GetEventsResponse(responses, missingIds);
    }
}

public sealed record GetEventsResponse(
    IReadOnlyList<GetEventResponse> Events,
    IReadOnlyList<Guid> MissingEventIds);
