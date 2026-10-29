using System.Net;
using System.Net.Http.Json;
using HalsingeLive.Contracts.Messaging;
using Microsoft.Extensions.Configuration;

namespace Booking.Features.Tickets;

public sealed class HttpTicketServiceClient(
    HttpClient httpClient,
    IConfiguration configuration) : ITicketServiceClient
{
    public async Task IssueTicketsAsync(
        BookingConfirmedV1 message,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "tickets/issue-from-booking");
        request.Content = JsonContent.Create(message);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"TicketService rejected ticket issuance with status code {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
    }

    public async Task<IReadOnlyList<TicketServiceTicketResponse>> GetTicketsForBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"tickets/by-booking/{bookingId:D}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "ticket lookup");
        return await response.Content.ReadFromJsonAsync<TicketServiceTicketResponse[]>(cancellationToken)
            ?? throw new HttpRequestException("TicketService returned an empty ticket-list response.");
    }

    public async Task<byte[]> GetTicketPdfAsync(
        Guid bookingId,
        Guid ticketId,
        bool download,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"tickets/by-booking/{bookingId:D}/{ticketId:D}/pdf?download={download.ToString().ToLowerInvariant()}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, "ticket PDF retrieval");
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string uri)
    {
        var apiKey = configuration["TicketService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new HttpRequestException("TicketService internal API key is not configured.");

        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Internal-Api-Key", apiKey);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        var reason = response.StatusCode == HttpStatusCode.NotFound
            ? "resource not found"
            : $"status code {(int)response.StatusCode}";
        throw new HttpRequestException(
            $"TicketService rejected {operation} with {reason}.",
            inner: null,
            response.StatusCode);
    }
}
