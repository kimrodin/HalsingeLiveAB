#nullable enable
using Microsoft.EntityFrameworkCore;
using EventCatalog.Data;

namespace EventCatalog.Features.Venues.SeatingMaps;

public sealed class GetVenueSeatingMapHandler(EventCatalogDbContext db, IVenueSeatingMapStorage storage)
{
    public async Task<VenueSeatingMapDownload?> Handle(Guid venueId, Guid mapId, CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateId(venueId, nameof(venueId));
        VenueValidation.ValidateId(mapId, nameof(mapId));
        var map = await db.VenueSeatingMaps.AsNoTracking()
            .SingleOrDefaultAsync(m => m.VenueId == venueId && m.VenueSeatingMapId == mapId, cancellationToken);
        if (map is null) return null;
        var content = await storage.OpenReadAsync(map.StorageKey, cancellationToken);
        return new(content, map.FileName, map.ContentType);
    }
}
