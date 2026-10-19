namespace EventCatalog.Features.Venues.CreateVenue;

public record CreateVenueRequest(
    string Name,
    string StreetAddress,
    string PostalCode,
    string City,
    int Capacity);
