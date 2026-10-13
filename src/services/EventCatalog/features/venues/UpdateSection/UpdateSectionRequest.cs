using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.UpdateSection;

public record UpdateSectionRequest(
    string Name,
    int Capacity,
    SeatingType SeatingType);
