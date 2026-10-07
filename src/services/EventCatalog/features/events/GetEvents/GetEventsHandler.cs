namespace EventCatalog.Features.Events.GetEvents;

public class Handler(EventCatalogDbContext dbContext)
{
    public async Task<List<Event>> Handle()
    {
        return await dbContext.Events
            .ToListAsync();
    }
}