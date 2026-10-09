namespace EventCatalog.Features.Events.GetEvent;

public record GetEventResponse(
    Guid EventId,
    string Name,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    DateTime OnSaleFrom,
    Guid VenueId,
    IReadOnlyList<EventSectionPriceResponse> SectionPrices
);

public record EventSectionPriceResponse(
    Guid SectionId,
    decimal Price
);
