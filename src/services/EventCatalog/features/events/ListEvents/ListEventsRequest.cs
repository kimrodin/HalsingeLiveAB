namespace EventCatalog.Features.Events.ListEvents;

public record ListEventsRequest(
    string EventName = null,
    Guid? VenueId = null,
    DateTime? StartAfter = null,
    DateTime? StartsBefore = null,
    int Page = 1,
    int PageSize = 10);
