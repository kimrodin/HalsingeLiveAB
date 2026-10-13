using System.ComponentModel.DataAnnotations;

namespace EventCatalog.Features.Events.UpdateEvent;

public record UpdateEventRequest(
    Guid EventId,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(500)] string Description,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    // Utelämnat eller tomt: behåll kopplingarna. Id:n här tas bara bort från detta evenemang.
    IReadOnlyList<Guid> SectionIdsToRemove = null);
