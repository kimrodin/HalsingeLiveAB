using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;

namespace EventCatalog.Features.Events.GetEvents;

public class Handler(EventCatalogDbContext dbContext)
{
    public async Task<List<Event>> Handle()
    {
        return await dbContext.Events
            .ToListAsync();
    }
}