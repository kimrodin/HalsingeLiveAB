using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateSection;

public record CreateSectionResponse(
    Guid SectionId,
    Guid VenueId,
    string Name,
    int Capacity,
    SeatingType SeatingType);
