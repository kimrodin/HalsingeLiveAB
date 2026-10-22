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
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaymentResult>(cancellationToken)
            ?? throw new JsonException("Payment service returned an empty response.");
    }
}
