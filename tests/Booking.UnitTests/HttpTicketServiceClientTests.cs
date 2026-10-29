using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Features.Tickets;
using HalsingeLive.Contracts.Messaging;
using Microsoft.Extensions.Configuration;

namespace Booking.UnitTests;

public sealed class HttpTicketServiceClientTests
{
    [Fact]
    public async Task IssueTicketsAsync_PostsConfirmationWithInternalApiKey()
    {
        var message = new BookingConfirmedV1
        {
            CorrelationId = Guid.NewGuid(),
            BookingId = Guid.NewGuid(),
            BookingItemId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventName = "Snapshot concert",
            EventStartsAtUtc = DateTimeOffset.UtcNow,
            VenueName = "Snapshot hall",
            SectionName = "Standing",
            Quantity = 2,
            UnitPrice = 199.50m,
            Currency = "SEK",
            OccurredAtUtc = DateTime.UtcNow
        };
        var httpClient = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/tickets/issue-from-booking", request.RequestUri!.AbsolutePath);
            Assert.Equal("configured-key", request.Headers.GetValues("X-Internal-Api-Key").Single());
            return VerifyBodyAndRespondAsync(request, message);
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TicketService:InternalApiKey"] = "configured-key"
            })
            .Build();

        await new HttpTicketServiceClient(httpClient, configuration).IssueTicketsAsync(message);
    }

    [Fact]
    public async Task IssueTicketsAsync_ThrowsWhenTicketServiceRejectsRequest()
    {
        var client = CreateClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TicketService:InternalApiKey"] = "configured-key"
            })
            .Build();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new HttpTicketServiceClient(client, configuration)
                .IssueTicketsAsync(new BookingConfirmedV1()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) =>
        new(new StubHttpMessageHandler(sendAsync))
        {
            BaseAddress = new Uri("http://ticket-service/")
        };

    private static async Task<HttpResponseMessage> VerifyBodyAndRespondAsync(
        HttpRequestMessage request,
        BookingConfirmedV1 expected)
    {
        var actual = await request.Content!.ReadFromJsonAsync<BookingConfirmedV1>();
        Assert.Equal(expected, actual);
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            sendAsync(request);
    }
}
