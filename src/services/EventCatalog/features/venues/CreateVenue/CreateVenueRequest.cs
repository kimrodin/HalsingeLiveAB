using System.ComponentModel.DataAnnotations;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateVenue;

public record CreateVenueRequest(
    [Required(ErrorMessage = "Please enter a name.")]
    [StringLength(200, ErrorMessage = "The name can be at most 200 characters long.")]
    string Name,

    [Required(ErrorMessage = "Please enter an address.")]
    [StringLength(500, ErrorMessage = "The address can be at most 500 characters long.")]
    string Address,

    [Range(1, int.MaxValue,
        ErrorMessage = "The capacity must be greater than zero.")]
    int Capacity,

    [EnumDataType(typeof(SeatingType),
        ErrorMessage = "Invalid seating type.")]
    SeatingType SeatingType);