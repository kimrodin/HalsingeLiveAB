using EventCatalog.Data;
using Microsoft.EntityFrameworkCore;
using EventCatalog.Features.Events.Models;

namespace EventCatalog.Features.Events.GetEvent;

public class Handler(EventCatalogDbContext db)
{
    public async Task<Event?> Handle(GetEventRequest request)
    {
        return await db.Events
            .FirstOrDefaultAsync(e => e.EventId == request.EventId);
    }
}