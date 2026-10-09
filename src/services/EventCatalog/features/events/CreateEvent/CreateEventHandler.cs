using EventCatalog.Data;
using EventCatalog.Features.Events.Models;

namespace EventCatalog.Features.Events.CreateEvent;

public class CreateEventHandler(EventCatalogDbContext dbContext)
{
    public async Task<CreateEventResponse> Handle(
        CreateEventRequest request, CancellationToken cancellationToken)
        {
        var newEvent = new Event
            {
                EventId = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description,
                StartDate = request.StartDate!.Value,
                EndDate = request.EndDate!.Value,
                OnSaleFrom = request.OnSaleFrom!.Value
            };

            dbContext.Events.Add(newEvent);

            await dbContext.SaveChangesAsync();

            return new CreateEventResponse(newEvent.EventId);
        }
}
