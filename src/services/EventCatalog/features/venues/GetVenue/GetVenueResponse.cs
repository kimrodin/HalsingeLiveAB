using EventCatalog.Features.Venues.Models;
namespace EventCatalog.Features.Venues.GetVenue;

public record GetVenueResponse(Guid VenueId, string Name, string Address, int Capacity, SeatingType SeatingType);
