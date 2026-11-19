using System.Net.Http.Json;
using Booking.Features.Payments.Gateway;

namespace Booking.Features.Privacy;

public sealed class HttpPaymentPrivacyDataClient(HttpClient client, IConfiguration configuration)
    : IPaymentPrivacyDataClient
{
    public async Task<IReadOnlyList<PaymentResultOutboxSummaryResponse>> GetResultOutboxAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var apiKey = configuration["PaymentSimulator:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new PaymentContractException("PaymentSimulator:InternalApiKey is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/payments/by-booking/{bookingId}/result-outbox");
        request.Headers.Add("X-Internal-Api-Key", apiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode is >= 400 and < 500)
                throw new PaymentContractException(
                    $"Payment service rejected the privacy lookup with HTTP {(int)response.StatusCode}.",
                    response.StatusCode);

            response.EnsureSuccessStatusCode();
        }

        return await response.Content.ReadFromJsonAsync<List<PaymentResultOutboxSummaryResponse>>(
                cancellationToken)
            ?? throw new PaymentContractException(
                "Payment service returned an empty result-outbox response.");
    }
}
