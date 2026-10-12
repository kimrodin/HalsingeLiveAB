using System.ComponentModel.DataAnnotations;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateSection;

public record CreateSectionRequest(
    [Required(ErrorMessage = "Please enter a section name.")]
    [StringLength(100, ErrorMessage = "The section name can be at most 100 characters long.")]
    string Name,

    [Range(1, int.MaxValue, ErrorMessage = "The section capacity must be greater than zero.")]
    int Capacity,

    [EnumDataType(typeof(SeatingType), ErrorMessage = "Invalid seating type.")]
    SeatingType SeatingType);
