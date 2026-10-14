using Microsoft.EntityFrameworkCore;
using EventCatalog.Data;
using EventCatalog.Features.Venues.Models;

namespace EventCatalog.Features.Venues.SeatingMaps;

public sealed class ListVenueSeatingMapsHandler(EventCatalogDbContext db)
{
    public async Task<ListVenueSeatingMapsResult> Handle(Guid venueId, CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateId(venueId, nameof(venueId));
        var exists = await db.Venues.AnyAsync(v => v.VenueId == venueId, cancellationToken);
        if (!exists) return new(false, []);

        var maps = await db.VenueSeatingMaps.AsNoTracking()
            .Where(m => m.VenueId == venueId)
            .OrderBy(m => m.Name)
            .Select(m => new VenueSeatingMapResponse(m.VenueSeatingMapId, m.VenueId, m.Name,
                m.FileName, m.ContentType, string.Empty))
            .ToListAsync(cancellationToken);
        return new(true, maps);
    }
}

public sealed record ListVenueSeatingMapsResult(bool VenueFound, IReadOnlyList<VenueSeatingMapResponse> Maps);
