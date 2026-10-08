namespace EventCatalog.Features.Venues.Models;

public class Seat
{
    // TODO: Konfigurera Id som primärnyckel och SectionId som främmande nyckel.
    // TODO: Kräv en angiven rad och ett positivt stolnummer.
    // TODO: Lägg en unik databasregel på SectionId, Row och Number för att undvika dubbla stolar i platskartan.
    // TODO: Bevara id när stolen används av ett evenemang; reservationer och försäljning ägs av bokningstjänsten.

    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }
}
