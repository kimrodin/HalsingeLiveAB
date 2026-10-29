using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.EventInventory.CreateEventInventory;
using Booking.Features.EventInventory.EventCatalog;
using Microsoft.EntityFrameworkCore;

namespace Booking.UnitTests;

public sealed class CreateEventInventoryFromCatalogHandlerTests
{
    [Fact]
    public async Task Handle_RejectsUnsupportedCatalogPricesAsContractErrors()
    {
        foreach (var price in new[] { -1m, 1.001m, 10_000_000_000m })
        {
            await using var db = CreateDbContext();
            var client = new StubEventCatalogClient(new CatalogEvent(
                [new CatalogEventSectionPrice(Guid.NewGuid(), price, 10)]));
            var handler = new CreateEventInventoryFromCatalogHandler(db, client);

            await Assert.ThrowsAsync<EventCatalogContractException>(
                () => handler.Handle(Guid.NewGuid()));
        }
    }

    [Fact]
    public async Task Handle_RejectsEmptyEventIdWithBookingValidationError()
    {
        await using var db = CreateDbContext();
        var handler = new CreateEventInventoryFromCatalogHandler(
            db, new StubEventCatalogClient(new CatalogEvent([])));

        var exception = await Assert.ThrowsAsync<BookingValidationException>(
            () => handler.Handle(Guid.Empty));

        Assert.Contains("eventId", exception.Errors.Keys);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private sealed class StubEventCatalogClient(CatalogEvent catalogEvent) : IEventCatalogClient
    {
        public Task<CatalogEvent> GetEventAsync(Guid eventId, CancellationToken cancellationToken) =>
            Task.FromResult(catalogEvent);
    }
}
