namespace EventCatalog.Features.Events.UpdateEvent;

public record UpdateEventRequest(
    Guid EventId,
    string Name,
    decimal Price,
    string Description,
    DateTime StartDate,
    DateTime EndDate);