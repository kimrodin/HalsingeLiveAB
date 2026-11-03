using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.Images;

public sealed class DeleteEventImageHandler(EventCatalogDbContext db, IEventImageStorage storage,
    ILogger<DeleteEventImageHandler> logger)
{
    public async Task<bool> Handle(Guid eventId, CancellationToken cancellationToken)
    {
        EventValidation.ValidateId(eventId, nameof(eventId));
        var image = await db.EventImages.SingleOrDefaultAsync(i => i.EventId == eventId, cancellationToken);
        if (image is null) return false;
        db.EventImages.Remove(image);
        await db.SaveChangesAsync(cancellationToken);
        try { await storage.DeleteAsync(image.StorageKey, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not clean up event image {StorageKey}", image.StorageKey); }
        return true;
    }
}
