using EventCatalog.Data;
using EventCatalog.Features.Events;
using Microsoft.EntityFrameworkCore;


namespace EventCatalog.Features.Events.DeleteEvent;

public class DeleteEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(DeleteEventRequest request)
    {
        EventValidation.ValidateId(request.EventId, nameof(request.EventId));

        var eventItem = await dbContext.Events
            .FirstOrDefaultAsync(e => e.EventId == request.EventId);

        if (eventItem is null)
            return false;

        dbContext.Events.Remove(eventItem);

        await dbContext.SaveChangesAsync();

        return true;
    }
}
