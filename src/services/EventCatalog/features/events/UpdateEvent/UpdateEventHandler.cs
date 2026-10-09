using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;

namespace EventCatalog.Features.Events.UpdateEvent;

public class UpdateEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(UpdateEventRequest request)
    {
        var eventItem = await dbContext.Events
            .FirstOrDefaultAsync(e => e.EventId == request.EventId);

        if (eventItem is null)
            return false;

        eventItem.Name = request.Name;
        eventItem.Description = request.Description;
        eventItem.StartDate = request.StartDate;
        eventItem.EndDate = request.EndDate;

        await dbContext.SaveChangesAsync();

        return true;
    }
}