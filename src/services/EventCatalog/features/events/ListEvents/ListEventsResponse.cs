namespace EventCatalog.Features.Events.ListEvents;

public class ListEventsResponse
{
    // En tom lista returneras när det inte finns några lokaler.
    public IReadOnlyList<EventListItemResponseDTO> Events { get; init; } = [];


    // TODO: Om paginering införs: lägg till sidnummer, sidstorlek och totalt antal lokaler.
}

public class EventListItemResponseDTO
{
    public Guid EventId { get; init; }
    public required string EventName { get; init; }
    public required string EventDescription { get; init; }
    public IReadOnlyList<SectionPriceResponseDTO> SectionPrices { get; init; } = [];
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public DateTime OnSaleFrom { get; init; }
    public Guid VenueId { get; init; }

}

public class SectionPriceResponseDTO
{
    public Guid SectionId { get; init; }
    public decimal Price { get; init; }
}
