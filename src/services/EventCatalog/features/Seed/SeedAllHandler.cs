using EventCatalog.Features.Seed.SeedEvents;
using EventCatalog.Features.Seed.SeedVenues;

namespace EventCatalog.Features.Seed;

public sealed class SeedAllHandler(
    SeedVenuesHandler seedVenuesHandler,
    SeedEventsHandler seedEventsHandler)
{
    public async Task<SeedAllResponse> Handle(CancellationToken cancellationToken)
    {
        var venues = await seedVenuesHandler.Handle(cancellationToken);
        var events = await seedEventsHandler.Handle(cancellationToken);
        return new SeedAllResponse(venues, events);
    }
}
