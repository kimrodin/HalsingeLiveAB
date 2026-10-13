using EventCatalog.Data;
using EventCatalog.Features.Venues.Models;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Venues;

public static class VenueCapacityRules
{
    // Kräver en aktiv transaktion. Alla ändringar som kan öka sektionernas
    // kapacitet eller minska lokalens kapacitet måste ta samma lås först.
    public static Task<Venue> LockVenueAsync(
        EventCatalogDbContext dbContext, Guid venueId, CancellationToken cancellationToken)
    {
        return dbContext.Venues
            .FromSqlInterpolated($"SELECT * FROM \"Venues\" WHERE \"VenueId\" = {venueId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public static Task<long> GetSectionCapacityAsync(
        EventCatalogDbContext dbContext, Guid venueId, CancellationToken cancellationToken)
    {
        // Summera som long så att stora kapaciteter inte orsakar heltalsöverflöde.
        return dbContext.Sections
            .Where(section => section.VenueId == venueId)
            .SumAsync(section => (long)section.SectionCapacity, cancellationToken);
    }
}
