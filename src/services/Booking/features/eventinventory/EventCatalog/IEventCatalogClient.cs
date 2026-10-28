namespace Booking.Features.EventInventory.EventCatalog;

public interface IEventCatalogClient
{
    Task<CatalogEvent> GetEventAsync(Guid eventId, CancellationToken cancellationToken);
}

public sealed record CatalogEvent(IReadOnlyList<CatalogEventSectionPrice> SectionPrices);

public sealed record CatalogEventSectionPrice(Guid SectionId, decimal Price, int SectionCapacity);

public sealed class EventCatalogNotFoundException(Guid eventId)
    : Exception($"Event {eventId} was not found in EventCatalog.");

public sealed class EventCatalogUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class EventCatalogContractException(string message, Exception innerException = null)
    : Exception(message, innerException);
