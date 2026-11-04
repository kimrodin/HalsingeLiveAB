using System.Net.Http.Json;

namespace Booking.Features.Seed.SeedPurchases;

public interface ISeedTicketsClient
{
    Task<int> DeleteTickets(CancellationToken ct);
}

public sealed class SeedTicketsClient(HttpClient client, IConfiguration configuration) : ISeedTicketsClient
{
    public async Task<int> DeleteTickets(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "tickets/seed");
        var key = configuration["TicketService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new HttpRequestException("TicketService internal API key is not configured.");
        request.Headers.Add("X-Internal-Api-Key", key);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeletedTicketsResponse>(ct))?.DeletedTickets
            ?? throw new HttpRequestException("Ticket seed deletion returned no response.");
    }
    private sealed record DeletedTicketsResponse(int DeletedTickets);
}
