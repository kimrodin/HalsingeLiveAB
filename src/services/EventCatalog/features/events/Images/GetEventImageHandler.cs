#nullable enable
using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.Images;

public sealed class GetEventImageHandler(EventCatalogDbContext db, IEventImageStorage storage)
{
    public async Task<EventImageDownload?> Handle(Guid eventId, Guid imageId, CancellationToken cancellationToken)
    {
        EventValidation.ValidateId(eventId, nameof(eventId));
        EventValidation.ValidateId(imageId, nameof(imageId));
        var image = await db.EventImages.AsNoTracking().SingleOrDefaultAsync(
            i => i.EventId == eventId && i.EventImageId == imageId, cancellationToken);
        if (image is null) return null;
        try
        {
            return new(await storage.OpenReadAsync(image.StorageKey, cancellationToken), image.FileName, image.ContentType);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}
