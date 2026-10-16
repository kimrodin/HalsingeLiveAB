using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Seed.SeedVenues;

public sealed record SeedVenuesResponse(
    IReadOnlyList<SeededVenueResponse> Venues);

public sealed record SeededVenueResponse(
    Guid VenueId,
    string VenueName,
    int VenueCapacity,
    IReadOnlyList<SeededSectionResponse> Sections);

public sealed record SeededSectionResponse(
    Guid SectionId,
    string Name,
    int Capacity,
    SeatingType SeatingType);
