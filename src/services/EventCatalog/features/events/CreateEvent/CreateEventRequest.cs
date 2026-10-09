using System.ComponentModel.DataAnnotations;

namespace EventCatalog.Features.Events.CreateEvent;

public record CreateEventRequest(
    [property: Required, StringLength(200)] string Name,
    [property: Required, StringLength(2000)] string Description,
    [property: Required] DateTime? StartDate,
    [property: Required] DateTime? EndDate,
    [property: Required] DateTime? OnSaleFrom);
