using EventCatalog.Features.Venues.Models;
namespace EventCatalog.Features.Venues.CreateVenue;

public record CreateVenueResponse(
    Guid VenueId, 
    string Name, 
    string StreetAddress,
    string PostalCode,
    string City,
    int Capacity);
