namespace EventCatalog.Features.Venues.UpdateVenue;

public record UpdateVenueRequest(
    Guid VenueId,
    string Name,
    string StreetAddress,
    string PostalCode,
    string City,
    int Capacity);
