namespace EventCatalog.Features.Events.UpdateEvent;

public record UpdateEventRequest(
    Guid Id,
    string Name,
    string Description,
    DateTime StartDate,
    DateTime EndDate);