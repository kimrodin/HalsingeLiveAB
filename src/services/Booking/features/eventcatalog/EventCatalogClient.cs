using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Booking.Features.EventCatalog;

public sealed class EventCatalogClient(HttpClient httpClient) : IEventSnapshotProvider
{
    public async Task<EventSnapshot> GetSnapshotAsync(
        Guid eventId,
        Guid sectionId,
        CancellationToken cancellationToken = default)
    {
        var eventDetails = await GetRequiredAsync<EventDetailsResponse>(
            $"GetEvent/{eventId}",
            cancellationToken);

        if (eventDetails.EventId != eventId
            || !eventDetails.SectionPrices.Any(section => section.SectionId == sectionId))
        {
            throw new EventCatalogSnapshotNotFoundException(
                $"Section {sectionId} is not part of event {eventId}.");
        }

        var venue = await GetRequiredAsync<VenueDetailsResponse>(
            $"GetVenue/{eventDetails.VenueId}",
            cancellationToken);
        var sectionDetails = venue.Sections.SingleOrDefault(section => section.SectionId == sectionId);

        if (sectionDetails is null)
        {
            throw new EventCatalogSnapshotNotFoundException(
                $"Section {sectionId} was not found in venue {venue.VenueId}.");
        }

        return new EventSnapshot(
            eventDetails.EventId,
            eventDetails.Name,
            eventDetails.StartDate,
            venue.Name,
            sectionDetails.Name);
    }

    private async Task<T> GetRequiredAsync<T>(
        string requestUri,
        CancellationToken cancellationToken)
        where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new EventCatalogUnavailableException(
                "Could not retrieve event details from EventCatalog.",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EventCatalogUnavailableException(
                "The request to EventCatalog timed out.",
                exception);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new EventCatalogSnapshotNotFoundException(
                    $"EventCatalog resource '{requestUri}' was not found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new EventCatalogUnavailableException(
                    $"EventCatalog returned HTTP {(int)response.StatusCode} for '{requestUri}'.");
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                    ?? throw new EventCatalogUnavailableException(
                        $"EventCatalog returned an empty response for '{requestUri}'.");
            }
            catch (HttpRequestException exception)
            {
                throw new EventCatalogUnavailableException(
                    $"Could not read the EventCatalog response for '{requestUri}'.",
                    exception);
            }
            catch (JsonException exception)
            {
                throw new EventCatalogUnavailableException(
                    $"EventCatalog returned invalid data for '{requestUri}'.",
                    exception);
            }
        }
    }

    private sealed record EventDetailsResponse(
        Guid EventId,
        string Name,
        DateTimeOffset StartDate,
        Guid VenueId,
        IReadOnlyList<EventSectionPriceResponse> SectionPrices);

    private sealed record EventSectionPriceResponse(Guid SectionId, decimal Price);

    private sealed record VenueDetailsResponse(
        Guid VenueId,
        string Name,
        string StreetAddress,
        string PostalCode,
        string City,
        int Capacity,
        IReadOnlyList<VenueSectionResponse> Sections);

    private sealed record VenueSectionResponse(
        Guid SectionId,
        string Name,
        int Capacity,
        int SeatingType);
}

public sealed record EventSnapshot(
    Guid EventId,
    string EventName,
    DateTimeOffset EventStartsAt,
    string VenueName,
    string SectionName);

public interface IEventSnapshotProvider
{
    Task<EventSnapshot> GetSnapshotAsync(
        Guid eventId,
        Guid sectionId,
        CancellationToken cancellationToken = default);
}

public sealed class EventCatalogUnavailableException(string message, Exception innerException = null)
    : Exception(message, innerException);

public sealed class EventCatalogSnapshotNotFoundException(string message) : Exception(message);
