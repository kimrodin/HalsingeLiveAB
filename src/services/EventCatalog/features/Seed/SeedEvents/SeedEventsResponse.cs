namespace EventCatalog.Features.Seed.SeedEvents;

public sealed record SeedEventsResponse(
    int CreatedCount,
    IReadOnlyList<SeededEventResponse> Events);

public sealed record SeededEventResponse(
    Guid EventId,
    string EventName,
    Guid VenueId,
    string VenueName,
    bool InventoryProvisioned = false);
