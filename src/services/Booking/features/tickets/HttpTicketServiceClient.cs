using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Booking.Features.Tickets;

public sealed class HttpTicketServiceClient(
    HttpClient httpClient,
    IConfiguration configuration) : ITicketServiceClient
{
    public async Task IssueTicketsAsync(
        BookingConfirmedMessage message,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "tickets/issue-from-booking")
        {
            Content = JsonContent.Create(message)
        };
        var apiKey = configuration["TicketService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new HttpRequestException("TicketService internal API key is not configured.");

        request.Headers.Add("X-Internal-Api-Key", apiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"TicketService rejected ticket issuance with status code {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
    }
}
