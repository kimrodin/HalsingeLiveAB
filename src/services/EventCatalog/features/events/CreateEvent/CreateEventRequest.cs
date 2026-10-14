namespace EventCatalog.Features.Events.CreateEvent;

public record CreateEventRequest(
    string Name,
    string Description,
    Guid? VenueId,
    DateTime? StartDate,
    DateTime? EndDate,
    DateTime? OnSaleFrom,
    List<CreateEventSectionPriceRequest> SectionPrices);

public record CreateEventSectionPriceRequest(
    Guid? SectionId,
    decimal? Price);
