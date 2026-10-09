namespace EventCatalog.Features.Events.GetEvent;

public record GetEventResponse(
    Guid EventId,
    string Name,
    string Description,
    decimal Price,
    DateTime StartDate,
    DateTime EndDate,
    DateTime OnSaleFrom,
    Guid VenueId
);