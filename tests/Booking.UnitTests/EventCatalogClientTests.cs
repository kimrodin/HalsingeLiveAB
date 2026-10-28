using System.Net;
using System.Net.Http.Json;
using Booking.Features.EventCatalog;

namespace Booking.UnitTests;

public sealed class EventCatalogClientTests
{
    [Fact]
    public async Task GetSnapshotAsync_ReturnsEventVenueAndSectionSnapshot()
    {
        var eventId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var startDate = new DateTimeOffset(2026, 11, 15, 19, 0, 0, TimeSpan.Zero);
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == $"/GetEvent/{eventId}")
            {
                return Task.FromResult(JsonResponse(new
                {
                    eventId,
                    name = "Konsert",
                    description = "Beskrivning",
                    startDate,
                    endDate = startDate.AddHours(3),
                    onSaleFrom = startDate.AddMonths(-1),
                    venueId,
                    sectionPrices = new[] { new { sectionId, price = 450m } }
                }));
            }

            if (request.RequestUri.AbsolutePath == $"/GetVenue/{venueId}")
            {
                return Task.FromResult(JsonResponse(new
                {
                    venueId,
                    name = "Hälsingehallen",
                    streetAddress = "Storgatan 1",
                    postalCode = "820 10",
                    city = "Ljusdal",
                    capacity = 1000,
                    sections = new[]
                    {
                        new { sectionId, name = "Ståplats", capacity = 500, seatingType = 0 }
                    }
                }));
            }

            throw new InvalidOperationException($"Unexpected request URI: {request.RequestUri}");
        });
        var client = new EventCatalogClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://eventcatalog/")
        });

        var snapshot = await client.GetSnapshotAsync(eventId, sectionId);

        Assert.Equal(eventId, snapshot.EventId);
        Assert.Equal("Konsert", snapshot.EventName);
        Assert.Equal(startDate, snapshot.EventStartsAt);
        Assert.Equal("Hälsingehallen", snapshot.VenueName);
        Assert.Equal("Ståplats", snapshot.SectionName);
    }

    [Fact]
    public async Task GetSnapshotAsync_ThrowsWhenSectionDoesNotBelongToEvent()
    {
        var eventId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var requestedSectionId = Guid.NewGuid();
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(new
        {
            eventId,
            name = "Konsert",
            description = "Beskrivning",
            startDate = DateTimeOffset.UtcNow,
            endDate = DateTimeOffset.UtcNow.AddHours(3),
            onSaleFrom = DateTimeOffset.UtcNow.AddMonths(-1),
            venueId,
            sectionPrices = Array.Empty<object>()
        })));
        var client = new EventCatalogClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://eventcatalog/")
        });

        await Assert.ThrowsAsync<EventCatalogSnapshotNotFoundException>(
            () => client.GetSnapshotAsync(eventId, requestedSectionId));
    }

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
