using EventCatalog.Features.Seed.SeedEvents;
using EventCatalog.Features.Seed.SeedVenues;

namespace EventCatalog.Features.Seed;

public sealed record SeedAllResponse(
    SeedVenuesResponse Venues,
    SeedEventsResponse Events);

public sealed record DeleteSeedDataResponse(
    int DeletedEvents,
    int DeletedSections,
    int DeletedVenues);
