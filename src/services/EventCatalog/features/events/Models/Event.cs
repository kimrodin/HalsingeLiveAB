namespace EventCatalog.Features.Events.Models;

using EventCatalog.Features.Venues.Models;

public class Event
{
    public Guid EventId { get; set; }
    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime OnSaleFrom { get; set; }
}
