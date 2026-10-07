namespace EventCatalog.Features.Events.CreateEvent;

public record CreateEventRequest(
    string Name,
    decimal Price,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    DateTime OnSaleFrom
    );
