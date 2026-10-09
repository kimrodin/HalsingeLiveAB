namespace EventCatalog.Features.Events.Models;

public class EventSectionPrice
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public decimal Price { get; set; }
}
