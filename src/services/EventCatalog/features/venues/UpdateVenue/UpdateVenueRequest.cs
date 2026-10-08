using EventCatalog.Features.Venues.Models;
namespace EventCatalog.Features.Venues.UpdateVenue;

public record UpdateVenueRequest(Guid VenueId, string Name, string Address, int Capacity, SeatingType SeatingType);
