using System.ComponentModel.DataAnnotations;

namespace EventCatalog.Features.Events.CreateEvent;

public record CreateEventRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(2000)] string Description,
    [Required] Guid? VenueId,
    [Required] DateTime? StartDate,
    [Required] DateTime? EndDate,
    [Required] DateTime? OnSaleFrom);
