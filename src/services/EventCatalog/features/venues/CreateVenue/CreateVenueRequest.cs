using System.ComponentModel.DataAnnotations;
using EventCatalog.Features.Venues.Models;
namespace EventCatalog.Features.Venues.CreateVenue;

public record CreateVenueRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(500)] string Address,
    [Range(1, int.MaxValue)] int Capacity,
    [EnumDataType(typeof(SeatingType))] SeatingType SeatingType);
