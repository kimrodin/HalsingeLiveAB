using Microsoft.EntityFrameworkCore;
using EventCatalog.Data;

namespace EventCatalog.Features.Venues.SeatingMaps;

public sealed class DeleteVenueSeatingMapHandler(EventCatalogDbContext db, IVenueSeatingMapStorage storage)
{
    public async Task<bool> Handle(Guid venueId, Guid mapId, CancellationToken cancellationToken = default)
    {
        VenueValidation.ValidateId(venueId, nameof(venueId));
        VenueValidation.ValidateId(mapId, nameof(mapId));
        var map = await db.VenueSeatingMaps
            .SingleOrDefaultAsync(m => m.VenueId == venueId && m.VenueSeatingMapId == mapId, cancellationToken);
        if (map is null) return false;
        db.VenueSeatingMaps.Remove(map);
        await db.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(map.StorageKey, cancellationToken);
        return true;
    }
}
