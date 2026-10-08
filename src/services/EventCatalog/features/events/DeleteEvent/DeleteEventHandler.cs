using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;


namespace EventCatalog.Features.Events.DeleteEvent;

public class Handler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(DeleteEventRequest request)
    {
        var eventItem = await dbContext.Events
            .FirstOrDefaultAsync(e => e.EventId == request.EventId);

        if (eventItem is null)
            return false;

        dbContext.Events.Remove(eventItem);

        await dbContext.SaveChangesAsync();

        return true;
    }
}
