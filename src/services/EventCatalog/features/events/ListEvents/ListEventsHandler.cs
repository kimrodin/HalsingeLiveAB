using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;

namespace EventCatalog.Features.Events.ListEvents;

public class ListEventsHandler(EventCatalogDbContext dbContext)
{
    public async Task<List<Event>> Handle()
    {
        return await dbContext.Events
            .ToListAsync();
    }
}