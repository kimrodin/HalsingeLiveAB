namespace EventCatalog.Features.Events.UpdateEvent;

public record UpdateEventRequest(
    string Name,
    string Description,
    DateTime StartDate,
    DateTime EndDate,
    Guid VenueId,
    DateTime OnSaleFrom,
    List<UpdateEventSectionPriceRequest> SectionPrices,
    // Utelämnat eller tomt: behåll kopplingarna. Id:n här tas bara bort från detta evenemang.
    IReadOnlyList<Guid> SectionIdsToRemove = null);

public record UpdateEventSectionPriceRequest(
    Guid? SectionId,
    decimal? Price);
