using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.UpdateVenue;

public record UpdateVenueRequest(
    Guid VenueId,
    string Name,
    string StreetAddress,
    string PostalCode,
    string City,
    int Capacity,
    IReadOnlyList<UpdateVenueSectionRequest> Sections);

public record UpdateVenueSectionRequest(Guid? SectionId, string Name, int Capacity, SeatingType SeatingType);
