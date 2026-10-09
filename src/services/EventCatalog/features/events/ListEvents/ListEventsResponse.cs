using EventCatalog.Features.Events.Models;

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
    public DateTime EventDate { get; init; }
    public Guid VenueId { get; init; }

}