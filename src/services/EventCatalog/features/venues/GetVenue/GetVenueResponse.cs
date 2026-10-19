using EventCatalog.Features.Venues;
namespace EventCatalog.Features.Venues.GetVenue;

public record GetVenueResponse(
    Guid VenueId,
    string Name,
    string StreetAddress,
    string PostalCode,
    string City,
    int Capacity,
    IReadOnlyList<VenueSectionResponse> Sections);
