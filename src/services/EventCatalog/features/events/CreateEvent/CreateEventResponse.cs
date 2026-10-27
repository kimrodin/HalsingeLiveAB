namespace EventCatalog.Features.Events.CreateEvent;

public record CreateEventResponse(Guid EventId, bool InventoryProvisioned = false);
