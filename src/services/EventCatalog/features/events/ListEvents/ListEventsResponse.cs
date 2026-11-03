namespace EventCatalog.Features.Events.ListEvents;

public class ListEventsResponse
{
    public IReadOnlyList<EventListItemResponseDTO> Events { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public class EventListItemResponseDTO
{
    public string ImageUrl { get; init; }
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
