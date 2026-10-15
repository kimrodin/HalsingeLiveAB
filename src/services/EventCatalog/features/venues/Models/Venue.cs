using System.ComponentModel.DataAnnotations;
using EventCatalog.Features.Events.Models;

namespace EventCatalog.Features.Venues.Models;

public sealed class Venue
{
    // TODO: Konfigurera Id som primärnyckel och kopplingen till sektioner i katalogens databas.
    // TODO: Bestäm maxlängder och obligatoriska fält för namn och adress.
    // TODO: Definiera regler för positiv kapacitet och hur den stäms av mot numrerade stolar.
    // TODO: Bestäm hur lokaler och platskartor skyddas eller versionshanteras när evenemang använder dem.
    [Key]
    public Guid VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string VenueStreetAddress { get; set; } = string.Empty;
    public string VenuePostalCode { get; set; } = string.Empty;
    public string VenueCity { get; set; } = string.Empty;
    public int VenueCapacity { get; set; }
    public List<Section> VenueSections { get; set; } = new List<Section>();
    public List<VenueSeatingMap> SeatingMaps { get; set; } = new List<VenueSeatingMap>();
    public List<Event> Events { get; set; } = new List<Event>();
}
