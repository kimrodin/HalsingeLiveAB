using System.Net;

namespace EventCatalog.Features.Events.CreateEvent;

public sealed class HttpBookingInventoryClient(HttpClient httpClient) : IBookingInventoryClient
{
    private const int MaximumAttempts = 3;

    public async Task EnsureInventoryCreatedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        Exception lastException = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                using var response = await httpClient.PostAsync(
                    $"CreateEventInventoryFromCatalog/from-catalog/{eventId}",
                    content: null,
                    cancellationToken);

                // A previous delivery may have completed before EventCatalog received its response.
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict)
                    return;

                lastException = new HttpRequestException(
                    $"Booking returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                // HttpClient timeout; treat it as transient and use the next attempt.
                lastException = exception;
            }
            catch (HttpRequestException exception)
            {
                lastException = exception;
            }

            if (attempt < MaximumAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
        }

        throw new BookingInventoryProvisioningException(
            $"Booking did not confirm inventory creation for event {eventId} after {MaximumAttempts} attempts.",
            lastException);
    }
}
