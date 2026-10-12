using System.ComponentModel.DataAnnotations;

namespace EventCatalog.Features.Events.CreateEvent;

public record CreateEventRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(2000)] string Description,
    [Required] Guid? VenueId,
    [Required] DateTime? StartDate,
    [Required] DateTime? EndDate,
    [Required] DateTime? OnSaleFrom,
    [Required, MinLength(1)] List<CreateEventSectionPriceRequest> SectionPrices);

public record CreateEventSectionPriceRequest(
    [Required] Guid? SectionId,
    [Required, Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? Price);
