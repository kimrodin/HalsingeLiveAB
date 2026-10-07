namespace EventCatalog.Features.Events.Models;

public class Event
{
    public Guid Id { get; set; }
    public Guid VenueId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime OnSaleFrom { get; set; }
}