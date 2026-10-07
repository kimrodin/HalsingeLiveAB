using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.ListVenues;

public class ListVenuesResponse
{
    // En tom lista returneras när det inte finns några lokaler.
    public IReadOnlyList<VenueListItemResponseDTO> Venues { get; init; } = [];

    // TODO: Om paginering införs: lägg till sidnummer, sidstorlek och totalt antal lokaler.
}

// En översikt per lokal. Hela platskartan hör till GetVenueResponse.
public class VenueListItemResponseDTO
{
    public Guid VenueId { get; init; }
    public required string VenueName { get; init; }
    public required string VenueAddress { get; init; }
    public int VenueCapacity { get; init; }
    public SeatingType SeatingType { get; init; }
}
