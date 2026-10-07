namespace EventCatalog.Features.Events.CreateEvent;

public class Handler(EventCatalogDbContext dbContext)
{
    public async Task<Guid> Handle(
        CreateEventRequest request)
        {
        var newEvent = new Event
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Price = request.Price,
                Description = request.Description,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                OnSaleFrom = request.OnSaleFrom
            };

            dbContext.Events.Add(newEvent);

            await dbContext.SaveChangesAsync();

            return newEvent.Id;
        }
}