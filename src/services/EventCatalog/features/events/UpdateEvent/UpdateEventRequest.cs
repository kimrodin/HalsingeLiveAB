using System.ComponentModel.DataAnnotations;

namespace EventCatalog.Features.Events.UpdateEvent;

public record UpdateEventRequest(
    Guid EventId,
    [Required, StringLength(200)] string Name,
    [Required, StringLength(2000)] string Description,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    [Required, MinLength(1)] List<UpdateEventSectionPriceRequest> SectionPrices);

public record UpdateEventSectionPriceRequest(
    [Required] Guid? SectionId,
    [Required, Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? Price);
