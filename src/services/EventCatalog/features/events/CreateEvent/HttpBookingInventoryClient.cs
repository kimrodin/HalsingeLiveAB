using System.Net;
using System.Net.Http.Headers;

namespace EventCatalog.Features.Events.CreateEvent;

public sealed class HttpBookingInventoryClient(
    HttpClient httpClient, IHttpContextAccessor httpContextAccessor) : IBookingInventoryClient
{
    private const int MaximumAttempts = 3;

    public async Task EnsureInventoryCreatedAsync(Guid eventId, CancellationToken cancellationToken)
    {
        // Capture the validated caller's token before awaiting; never share headers between requests.
        var context = httpContextAccessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true || !context.User.IsInRole("admin")
            || !AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            throw new BookingInventoryProvisioningException(
                "Inventory provisioning requires the calling administrator's bearer token.",
                new InvalidOperationException("An authenticated admin request with a bearer token is required."));
        }

        Exception lastException = null;

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"CreateEventInventoryFromCatalog/from-catalog/{eventId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorization.Parameter);
                using var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new BookingInventoryProvisioningException(
                        "Booking rejected the forwarded administrator token.",
                        new HttpRequestException("Inventory authorization failed.",
                            inner: null, response.StatusCode));

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
