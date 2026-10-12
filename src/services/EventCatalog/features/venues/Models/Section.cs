using EventCatalog.Features.Events.Models;

namespace EventCatalog.Features.Venues.Models;

public class Section
{
    // TODO: Konfigurera Id som primärnyckel och VenueId som främmande nyckel.
    // TODO: Bestäm om sektionsnamn ska vara unika inom lokalen och hur sektionerna sorteras.
    // TODO: Konfigurera relationen till stolar och reglerna för borttagning.

    public Guid SectionId { get; set; }
    public Guid VenueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SectionCapacity { get; set; }
    public SeatingType SeatingType { get; set; }
    public List<Seat> Seats { get; set; } = new List<Seat>();
    public List<EventSectionPrice> EventPrices { get; set; } = new List<EventSectionPrice>();
}
