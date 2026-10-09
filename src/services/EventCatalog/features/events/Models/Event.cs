using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Events.Models;

public class Event
{
    public Guid EventId { get; set; }
    public Guid VenueId { get; set; }
    public Venue Venue { get; set; } = null!;
    public string Name { get; set; }
    public List<EventSectionPrice> SectionPrices { get; set; } = [];
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime OnSaleFrom { get; set; }
}
