using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Booking.Features.Seed.SeedPurchases;

public interface ISeedPaymentsClient
{
    Task<int> DeletePayments(CancellationToken ct);
    Task EnsurePayments(IReadOnlyList<SeedPayment> rows, CancellationToken ct);
}

public sealed class SeedPaymentsClient(HttpClient client, IHttpContextAccessor context) : ISeedPaymentsClient
{
    public async Task<int> DeletePayments(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "api/payments/seed");
        if (!AuthenticationHeaderValue.TryParse(context.HttpContext?.Request.Headers.Authorization.ToString(), out var auth)
            || !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An admin token is required for seed payments.");
        request.Headers.Authorization = auth;
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DeletedPaymentsResponse>(ct))?.DeletedPayments
            ?? throw new HttpRequestException("Payment seed deletion returned no response.");
    }

    private sealed record DeletedPaymentsResponse(int DeletedPayments);

    public async Task EnsurePayments(IReadOnlyList<SeedPayment> rows, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/payments/seed");
        if (!AuthenticationHeaderValue.TryParse(context.HttpContext?.Request.Headers.Authorization.ToString(), out var auth)
            || !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("An admin token is required for seed payments.");
        request.Headers.Authorization = auth;
        request.Content = JsonContent.Create(rows);
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new SeedPurchasesConflictException("Seed payments conflict with existing payments.");
        response.EnsureSuccessStatusCode();
    }
}

public sealed record SeedPayment(Guid BookingId, decimal Amount, DateTimeOffset PaidAt);
