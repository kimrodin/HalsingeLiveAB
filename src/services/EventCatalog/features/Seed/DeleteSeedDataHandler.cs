using EventCatalog.Data;
using EventCatalog.Features.Seed.SeedEvents;
using EventCatalog.Features.Seed.SeedVenues;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Seed;

public sealed class DeleteSeedDataHandler(EventCatalogDbContext dbContext)
{
    public async Task<DeleteSeedDataResponse> Handle(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var venueNames = SeedVenuesHandler.SeedVenueNames.ToArray();
        var seedVenueSections = SeedVenuesHandler.SeedVenueSections.ToHashSet();
        var seedEvents = SeedEventsHandler.SeedEventVenueNames
            .ToDictionary(item => item.EventName, item => item.VenueName);
        var seedEventNames = seedEvents.Keys.ToArray();

        var venues = await dbContext.Venues
            .Include(venue => venue.VenueSections)
            .Where(venue => venueNames.Contains(venue.VenueName))
            .ToListAsync(cancellationToken);
        var venueIds = venues.Select(venue => venue.VenueId).ToArray();
        var sectionIds = venues.SelectMany(venue => venue.VenueSections)
            .Select(section => section.SectionId)
            .ToArray();

        var events = await dbContext.Events
            .Include(eventItem => eventItem.SectionPrices)
            .Where(eventItem => seedEventNames.Contains(eventItem.Name))
            .ToListAsync(cancellationToken);
        var eventIds = events.Select(eventItem => eventItem.EventId).ToArray();

        foreach (var eventItem in events)
        {
            var expectedVenueName = seedEvents[eventItem.Name];
            if (!venues.Any(venue => venue.VenueId == eventItem.VenueId && venue.VenueName == expectedVenueName))
            {
                throw new SeedDataDeleteConflictException(
                    $"Event '{eventItem.Name}' is not linked to its expected seeded venue.");
            }
        }

        var unrelatedEvents = await dbContext.Events
            .Where(eventItem => venueIds.Contains(eventItem.VenueId)
                && !eventIds.Contains(eventItem.EventId))
            .Select(eventItem => eventItem.Name)
            .ToListAsync(cancellationToken);
        if (unrelatedEvents.Count > 0)
        {
            throw new SeedDataDeleteConflictException(
                $"Seeded venues are used by other events ({string.Join(", ", unrelatedEvents)}); remove or move those events first.");
        }

        var unexpectedSections = venues
            .SelectMany(venue => venue.VenueSections
                .Where(section => !seedVenueSections.Contains((venue.VenueName, section.Name)))
                .Select(section => $"{venue.VenueName}/{section.Name}"))
            .ToArray();
        if (unexpectedSections.Length > 0)
        {
            throw new SeedDataDeleteConflictException(
                $"Seeded venues contain non-seed sections ({string.Join(", ", unexpectedSections)}); remove or move those sections first.");
        }

        var hasSeatingMaps = await dbContext.VenueSeatingMaps
            .AnyAsync(map => venueIds.Contains(map.VenueId), cancellationToken);
        if (hasSeatingMaps)
        {
            throw new SeedDataDeleteConflictException(
                "A seeded venue has seating maps. Remove the maps before deleting the seeded venues.");
        }

        var unrelatedSectionPrices = await dbContext.EventSectionPrice
            .AnyAsync(price => sectionIds.Contains(price.SectionId)
                && !eventIds.Contains(price.EventId), cancellationToken);
        if (unrelatedSectionPrices)
        {
            throw new SeedDataDeleteConflictException(
                "A seeded section is linked to a non-seed event; remove that link before deleting seed data.");
        }

        var seededSectionEntities = venues.SelectMany(venue => venue.VenueSections).ToArray();
        var deletedEventCount = events.Count;
        var deletedSectionCount = seededSectionEntities.Length;
        var deletedVenueCount = venues.Count;

        dbContext.EventSectionPrice.RemoveRange(events.SelectMany(eventItem => eventItem.SectionPrices));
        dbContext.Events.RemoveRange(events);
        dbContext.Sections.RemoveRange(seededSectionEntities);
        dbContext.Venues.RemoveRange(venues);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DeleteSeedDataResponse(deletedEventCount, deletedSectionCount, deletedVenueCount);
    }
}

public sealed class SeedDataDeleteConflictException(string message) : Exception(message);
