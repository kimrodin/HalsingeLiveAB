using EventCatalog.Data;
using EventCatalog.Features.Events.Models;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Seed.SeedEvents;

public sealed class SeedEventsHandler(EventCatalogDbContext dbContext)
{
    private static readonly EventSeedDefinition[] Definitions =
    [
        new("Vinterkonsert i stora salen", "En vinterkonsert med artister från Hälsingland.", "Kulturhuset (stora salen)", Utc(2027, 1, 16, 19), [495m, 395m, 295m]),
        new("Standup på Kulturhuset", "En kväll med standup och gästande komiker.", "Kulturhuset (stora salen)", Utc(2027, 2, 20, 19), [350m, 300m, 250m]),
        new("Vårkonsert i stora salen", "Vårkonsert med lokala musiker och gästartister.", "Kulturhuset (stora salen)", Utc(2027, 3, 27, 19), [450m, 375m, 325m]),
        new("Danskväll i stora salen", "Liveband och dans i Kulturhusets stora sal.", "Kulturhuset (stora salen)", Utc(2027, 4, 24, 20), [395m, 345m, 295m]),
        new("Akustisk kväll på Studion", "En intim konsert med akustiska framträdanden.", "Studion", Utc(2027, 2, 6, 19), [225m]),
        new("Indie Live på Studion", "En kväll med nya indieband från regionen.", "Studion", Utc(2027, 4, 10, 20), [275m]),
        new("Sommarklubb på Studion", "Sommarkväll med DJ och dans.", "Studion", Utc(2027, 6, 5, 21), [250m]),
        new("Hälsingesommar Festival", "En tvådagars musikfestival för hela familjen.", "Sommarfestivalens område", Utc(2027, 7, 9, 12), [495m], DurationHours: 36),
        new("Rock under bar himmel", "En utomhuskonsert med svenska rockband.", "Sommarfestivalens område", Utc(2027, 8, 14, 17), [595m]),
        new("Avslutningsfestivalen", "Vi avslutar sommaren med musik och mat.", "Sommarfestivalens område", Utc(2027, 9, 4, 14), [395m])
    ];

    public static IReadOnlyList<(string EventName, string VenueName)> SeedEventVenueNames =>
        Definitions.Select(definition => (definition.Name, definition.VenueName)).ToArray();

    public async Task<SeedEventsResponse> Handle(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var venueNames = Definitions.Select(definition => definition.VenueName).Distinct().ToArray();
        var venues = await dbContext.Venues
            .Include(venue => venue.VenueSections)
            .Where(venue => venueNames.Contains(venue.VenueName))
            .ToDictionaryAsync(venue => venue.VenueName, cancellationToken);

        var results = new List<SeededEventResponse>(Definitions.Length);
        var createdCount = 0;

        foreach (var definition in Definitions)
        {
            if (!venues.TryGetValue(definition.VenueName, out var venue))
            {
                throw new EventSeedConflictException(
                    $"Venue '{definition.VenueName}' was not found. Seed venues before seeding events.");
            }

            var sections = venue.VenueSections.OrderBy(section => section.Name).ToArray();
            if (sections.Length != definition.Prices.Length)
            {
                throw new EventSeedConflictException(
                    $"Venue '{definition.VenueName}' has {sections.Length} sections, but {definition.Prices.Length} section prices were configured for event '{definition.Name}'.");
            }

            var existingEvent = await dbContext.Events
                .Include(eventItem => eventItem.SectionPrices)
                .SingleOrDefaultAsync(eventItem => eventItem.Name == definition.Name, cancellationToken);

            if (existingEvent is not null)
            {
                EnsureExistingEventMatches(existingEvent, venue, sections, definition);
                results.Add(new SeededEventResponse(
                    existingEvent.EventId,
                    existingEvent.Name,
                    venue.VenueId,
                    venue.VenueName));
                continue;
            }

            var eventId = Guid.NewGuid();
            var eventItem = new Event
            {
                EventId = eventId,
                VenueId = venue.VenueId,
                Name = definition.Name,
                Description = definition.Description,
                StartDate = definition.StartDate,
                EndDate = definition.StartDate.AddHours(definition.DurationHours),
                OnSaleFrom = definition.StartDate.AddMonths(-2),
                SectionPrices = sections.Select((section, index) => new EventSectionPrice
                {
                    EventId = eventId,
                    SectionId = section.SectionId,
                    Price = definition.Prices[index]
                }).ToList()
            };

            dbContext.Events.Add(eventItem);
            createdCount++;
            results.Add(new SeededEventResponse(eventId, eventItem.Name, venue.VenueId, venue.VenueName));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SeedEventsResponse(createdCount, results);
    }

    private static void EnsureExistingEventMatches(
        Event eventItem,
        Venue venue,
        IReadOnlyList<Section> sections,
        EventSeedDefinition definition)
    {
        var expectedEndDate = definition.StartDate.AddHours(definition.DurationHours);
        var expectedPrices = sections
            .Select((section, index) => (section.SectionId, Price: definition.Prices[index]))
            .ToDictionary(item => item.SectionId, item => item.Price);

        var pricesMatch = eventItem.SectionPrices.Count == expectedPrices.Count
            && eventItem.SectionPrices.All(price =>
                expectedPrices.TryGetValue(price.SectionId, out var expectedPrice)
                && price.Price == expectedPrice);

        if (eventItem.VenueId != venue.VenueId
            || eventItem.Description != definition.Description
            || eventItem.StartDate != definition.StartDate
            || eventItem.EndDate != expectedEndDate
            || eventItem.OnSaleFrom != definition.StartDate.AddMonths(-2)
            || !pricesMatch)
        {
            throw new EventSeedConflictException(
                $"Event '{definition.Name}' already exists with different values.");
        }
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private sealed record EventSeedDefinition(
        string Name,
        string Description,
        string VenueName,
        DateTime StartDate,
        decimal[] Prices,
        int DurationHours = 3);
}

public sealed class EventSeedConflictException(string message) : Exception(message);
