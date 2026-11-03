using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Features.Tickets;
using Microsoft.Extensions.Configuration;

namespace Booking.UnitTests;

public sealed class HttpTicketServiceClientTests
{
    [Fact]
    public async Task IssueTicketsAsync_PostsConfirmationWithInternalApiKey()
    {
        var message = new BookingConfirmedMessage
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
                .IssueTicketsAsync(new BookingConfirmedMessage()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    [Theory]
    [InlineData("check-in", "CheckedIn")]
    [InlineData("cancel", "Cancelled")]
    public async Task TicketStatusChange_PostsWithInternalApiKeyAndReturnsStatus(
        string action,
        string status)
    {
        var ticketId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow;
        var httpClient = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/tickets/{ticketId:D}/{action}", request.RequestUri!.AbsolutePath);
            Assert.Equal("configured-key", request.Headers.GetValues("X-Internal-Api-Key").Single());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new TicketServiceStatusChangeResponse(ticketId, status, changedAt))
            });
        });
        var client = CreateTicketClient(httpClient);

        var result = action == "check-in"
            ? await client.CheckInTicketAsync(ticketId, eventId)
            : await client.CancelTicketAsync(ticketId);

        Assert.Equal(ticketId, result.TicketId);
        Assert.Equal(status, result.Status);
        Assert.Equal(changedAt, result.ChangedAt);
    }

    [Fact]
    public async Task CheckInTicketAsync_PreservesConflictStatus()
    {
        var client = CreateTicketClient(CreateClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict))));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CheckInTicketAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) =>
        new(new StubHttpMessageHandler(sendAsync))
        {
            BaseAddress = new Uri("http://ticket-service/")
        };

    private static HttpTicketServiceClient CreateTicketClient(HttpClient httpClient)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TicketService:InternalApiKey"] = "configured-key"
            })
            .Build();
        return new HttpTicketServiceClient(httpClient, configuration);
    }

    private static async Task<HttpResponseMessage> VerifyBodyAndRespondAsync(
        HttpRequestMessage request,
        BookingConfirmedMessage expected)
    {
        var actual = await request.Content!.ReadFromJsonAsync<BookingConfirmedMessage>();
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
