namespace EventCatalog.Features.Events.GetEvent;

public class Handler(EventCatalogDbContext db)
{
    public async Task<Event?> Handle(Request request)
    {
        return await db.Events
            .FirstOrDefaultAsync(e => e.Id == request.Id);
    }
}