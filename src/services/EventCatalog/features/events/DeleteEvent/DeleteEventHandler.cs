namespace EventCatalog.Features.Events.DeleteEvent;

public class Handler(EventCatalogDbContext dbContext)
{
    public async Task<bool> Handle(DeleteEventRequest request)
    {
        var eventItem = await dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == request.Id);

        if (eventItem is null)
            return false;

        dbContext.Events.Remove(eventItem);

        await dbContext.SaveChangesAsync();

        return true;
    }
}