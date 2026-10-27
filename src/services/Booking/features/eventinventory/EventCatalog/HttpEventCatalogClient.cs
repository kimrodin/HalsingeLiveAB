using System.Net;
using System.Net.Http.Json;

namespace Booking.Features.EventInventory.EventCatalog;

public sealed class HttpEventCatalogClient(HttpClient httpClient) : IEventCatalogClient
{
    public async Task<CatalogEvent> GetEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync($"GetEvent/{eventId}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new EventCatalogNotFoundException(eventId);

            response.EnsureSuccessStatusCode();
            var eventResponse = await response.Content.ReadFromJsonAsync<EventCatalogResponse>(
                cancellationToken: cancellationToken);
            if (eventResponse is null)
                throw new EventCatalogContractException("EventCatalog returned an empty event response.");

            return new CatalogEvent(eventResponse.SectionPrices
                ?? throw new EventCatalogContractException("EventCatalog returned no section prices."));
        }
        catch (EventCatalogNotFoundException)
        {
            throw;
        }
        catch (EventCatalogContractException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new EventCatalogContractException("EventCatalog returned an unreadable event response.", exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException)
        {
            throw new EventCatalogUnavailableException("Could not retrieve the event from EventCatalog.", exception);
        }
    }

    private sealed record EventCatalogResponse(
        IReadOnlyList<CatalogEventSectionPrice> SectionPrices);
}
