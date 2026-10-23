using EventCatalog.Data;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Seed.SeedVenues;

public sealed class SeedVenuesHandler(EventCatalogDbContext dbContext)
{
    private const string PlaceholderAddress = "Ej angiven";
    private const string PlaceholderPostalCode = "000 00";
    private const string PlaceholderCity = "Ej angiven";

    private static readonly VenueSeedDefinition[] Definitions =
    [
        new(1, "Kulturhuset (stora salen)", 600, "Storgatan 12", "821 32", "Hudiksvall",
        [
            new(1, "Sektion 1", 200, SeatingType.AssignedSeating),
            new(2, "Sektion 2", 200, SeatingType.AssignedSeating),
            new(3, "Sektion 3", 200, SeatingType.AssignedSeating)
        ]),
        new(2, "Studion", 150, "Långgatan 5", "851 27", "Hudiksvall",
        [
            new(4, "Ståplats", 150, SeatingType.GeneralAdmission)
        ]),
        new(3, "Sommarfestivalens område", 2000, PlaceholderAddress, PlaceholderPostalCode, PlaceholderCity,
        [
            new(5, "Ståplats", 2000, SeatingType.GeneralAdmission)
        ])
    ];

    public static IReadOnlyList<string> SeedVenueNames =>
        Definitions.Select(definition => definition.Name).ToArray();

    public static IReadOnlyList<(string VenueName, string SectionName)> SeedVenueSections =>
        Definitions.SelectMany(definition => definition.Sections
            .Select(section => (definition.Name, section.Name)))
            .ToArray();

    public async Task<SeedVenuesResponse> Handle(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var response = new List<SeededVenueResponse>(Definitions.Length);

        foreach (var definition in Definitions)
        {
            var venueId = SeedGuid("30000000-0000-0000-0000", definition.SeedOrdinal);
            var venue = await dbContext.Venues
                .Include(item => item.VenueSections)
                .SingleOrDefaultAsync(item => item.VenueName == definition.Name, cancellationToken);

            if (venue is null)
            {
                venue = new Venue
                {
                    VenueId = venueId,
                    VenueName = definition.Name,
                    VenueStreetAddress = definition.StreetAddress,
                    VenuePostalCode = definition.PostalCode,
                    VenueCity = definition.City,
                    VenueCapacity = definition.Capacity
                };
                dbContext.Venues.Add(venue);
            }
            else if (venue.VenueId != venueId || venue.VenueCapacity != definition.Capacity)
            {
                throw new VenueSeedConflictException(
                    $"Venue '{definition.Name}' conflicts with the deterministic seed. Delete and reseed the existing seed data.");
            }

            foreach (var sectionDefinition in definition.Sections)
            {
                var sectionId = SeedGuid("20000000-0000-0000-0000", sectionDefinition.SeedOrdinal);
                var existingSection = venue.VenueSections
                    .SingleOrDefault(section => section.Name == sectionDefinition.Name);

                if (existingSection is not null)
                {
                    if (existingSection.SectionId != sectionId
                        || existingSection.SectionCapacity != sectionDefinition.Capacity
                        || existingSection.SeatingType != sectionDefinition.SeatingType)
                    {
                        throw new VenueSeedConflictException(
                            $"Section '{sectionDefinition.Name}' in venue '{definition.Name}' conflicts with the deterministic seed. Delete and reseed the existing seed data.");
                    }

                    continue;
                }

                var currentSectionCapacity = venue.VenueSections.Sum(section => (long)section.SectionCapacity);
                if (currentSectionCapacity + sectionDefinition.Capacity > venue.VenueCapacity)
                {
                    throw new VenueSeedConflictException(
                        $"Adding section '{sectionDefinition.Name}' would exceed venue '{definition.Name}' capacity.");
                }

                var section = new Section
                {
                    SectionId = sectionId,
                    VenueId = venue.VenueId,
                    Name = sectionDefinition.Name,
                    SectionCapacity = sectionDefinition.Capacity,
                    SeatingType = sectionDefinition.SeatingType
                };

                venue.VenueSections.Add(section);
                dbContext.Sections.Add(section);
            }

            response.Add(new SeededVenueResponse(
                venue.VenueId,
                venue.VenueName,
                venue.VenueCapacity,
                venue.VenueSections
                    .Where(section => definition.Sections.Any(seedSection => seedSection.Name == section.Name))
                    .Select(section => new SeededSectionResponse(
                        section.SectionId,
                        section.Name,
                        section.SectionCapacity,
                        section.SeatingType))
                    .OrderBy(section => section.Name)
                    .ToList()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SeedVenuesResponse(response);
    }

    private sealed record VenueSeedDefinition(int SeedOrdinal, string Name, int Capacity, string StreetAddress, string PostalCode, string City, SectionSeedDefinition[] Sections);
    private sealed record SectionSeedDefinition(int SeedOrdinal, string Name, int Capacity, SeatingType SeatingType);

    private static Guid SeedGuid(string prefix, int ordinal) => Guid.Parse($"{prefix}-{ordinal:D12}");
}

public sealed class VenueSeedConflictException(string message) : Exception(message);
