using EventCatalog.Data;
using EventCatalog.Features.Events;
using Microsoft.EntityFrameworkCore;


namespace EventCatalog.Features.Events.DeleteEvent;

public class DeleteEventHandler(EventCatalogDbContext dbContext,
    EventCatalog.Features.Events.Images.IEventImageStorage storage = null,
    ILogger<DeleteEventHandler> logger = null)
{
    public async Task<bool> Handle(DeleteEventRequest request)
    {
        EventValidation.ValidateId(request.EventId, nameof(request.EventId));

        var eventItem = await dbContext.Events
            .Include(e => e.Image)
            .FirstOrDefaultAsync(e => e.EventId == request.EventId);

        if (eventItem is null)
            return false;

        dbContext.Events.Remove(eventItem);

        await dbContext.SaveChangesAsync();
        if (eventItem.Image is not null && storage is not null)
        {
            try { await storage.DeleteAsync(eventItem.Image.StorageKey, CancellationToken.None); }
            catch (Exception ex) { logger?.LogWarning(ex, "Could not clean up image for deleted event {EventId}", eventItem.EventId); }
        }

        return true;
    }
}
