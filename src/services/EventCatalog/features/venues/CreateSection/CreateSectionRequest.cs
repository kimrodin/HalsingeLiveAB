using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateSection;

public record CreateSectionRequest(
    string Name,
    int Capacity,
    SeatingType SeatingType);
