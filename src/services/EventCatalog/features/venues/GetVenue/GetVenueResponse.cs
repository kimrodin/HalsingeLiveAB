using EventCatalog.Features.Venues;
namespace EventCatalog.Features.Venues.GetVenue;

public record GetVenueResponse(
    Guid VenueId,
    string Name,
    string Address,
    int Capacity,
    IReadOnlyList<VenueSectionResponse> Sections);
