using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.CreateVenue;

public class CreateVenueRequest
{
    public required string Name { get; init; }
    public required string Address { get; init; }
    public int Capacity { get; init; }
    public SeatingType SeatingType { get; init; }

    // TODO: Definiera indata för sektioner och stolar om platskartan ska skapas samtidigt.
    // TODO: Använd separata DTO:er för platskartan så att databasmodeller inte blir API-kontrakt.
    // TODO: Beskriv vilka fält som är obligatoriska och ett exempel för fri respektive numrerad placering.
}
