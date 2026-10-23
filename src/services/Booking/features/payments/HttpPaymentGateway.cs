using System.Net;
using System.Text.Json;

namespace Booking.Features.Payments;

public sealed class HttpPaymentGateway(HttpClient client) : IPaymentGateway
{
    public async Task<PaymentResult> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"api/payments/by-booking/{bookingId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<PaymentResult> ProcessAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("api/payments", new { bookingId, amount, currency }, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<PaymentResult> RefundAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsync($"api/payments/{paymentId}/refund", null, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<PaymentResult> CancelAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("api/payments/cancel", new { bookingId, amount, currency }, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    private static async Task<PaymentResult> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Lookup's expected 404 is handled by GetByBookingAsync. A 404 here (for
        // example on refund), 400 or 409 indicates a rejected/inconsistent contract.
        // 408, 429 and server errors remain uncertain and may be retried safely.
        if ((int)response.StatusCode is >= 400 and < 500
            && response.StatusCode is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests))
            throw new PaymentContractException(
                $"Payment service rejected the request with HTTP {(int)response.StatusCode}. Reconciliation remains open.",
                response.StatusCode);

        response.EnsureSuccessStatusCode();
        try
        {
            return await response.Content.ReadFromJsonAsync<PaymentResult>(cancellationToken)
                ?? throw new JsonException("Payment service returned an empty response.");
        }
        catch (JsonException exception)
        {
            throw new PaymentContractException("Payment service returned an invalid payment response. Reconciliation remains open.",
                response.StatusCode, exception);
        }
    }
}
