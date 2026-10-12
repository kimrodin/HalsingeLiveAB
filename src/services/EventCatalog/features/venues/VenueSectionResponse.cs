using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues;

public record VenueSectionResponse(
    Guid SectionId,
    string Name,
    int Capacity,
    SeatingType SeatingType);
