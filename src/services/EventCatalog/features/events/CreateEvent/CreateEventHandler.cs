using EventCatalog.Data;
using EventCatalog.Features.Events.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.CreateEvent;

public class CreateEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<CreateEventResponse> Handle(
        CreateEventRequest request, CancellationToken cancellationToken)
    {
        var venueId = request.VenueId!.Value;
        var requestedSectionPrices = request.SectionPrices!;
        var sectionIds = requestedSectionPrices
            .Select(sectionPrice => sectionPrice.SectionId!.Value)
            .ToArray();

        if (sectionIds.Any(sectionId => sectionId == Guid.Empty)
            || sectionIds.Distinct().Count() != sectionIds.Length)
        {
            throw new ArgumentException("Section IDs must be non-empty and unique.", nameof(request));
        }

        var venueExists = await dbContext.Venues
            .AnyAsync(venue => venue.VenueId == venueId, cancellationToken);

        if (!venueExists)
            return null;

        var matchingSectionCount = await dbContext.Sections
            .CountAsync(
                section => section.VenueId == venueId && sectionIds.Contains(section.SectionId),
                cancellationToken);

        if (matchingSectionCount != sectionIds.Length)
            return null;

        var eventId = Guid.NewGuid();
        var newEvent = new Event
        {
            EventId = eventId,
            VenueId = venueId,
            Name = request.Name,
            Description = request.Description,
            StartDate = request.StartDate!.Value,
            EndDate = request.EndDate!.Value,
            OnSaleFrom = request.OnSaleFrom!.Value,
            SectionPrices = requestedSectionPrices
                .Select(sectionPrice => new EventSectionPrice
                {
                    EventId = eventId,
                    SectionId = sectionPrice.SectionId!.Value,
                    Price = sectionPrice.Price!.Value
                })
                .ToList()
        };

        dbContext.Events.Add(newEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateEventResponse(newEvent.EventId);
    }
}
