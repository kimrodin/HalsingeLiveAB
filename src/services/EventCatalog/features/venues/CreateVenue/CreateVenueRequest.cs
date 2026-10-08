using EventCatalog.Features.Venues.Models;
namespace EventCatalog.Features.Venues.CreateVenue;

public record CreateVenueRequest(
    string Name, 
    string Address, 
    int Capacity, 
    SeatingType SeatingType);
