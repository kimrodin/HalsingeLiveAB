#nullable enable
using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Booking.Features.Payments;

namespace Booking.Features.Statistics;

public interface IStatisticsPayments
{
    IAsyncEnumerable<IReadOnlyList<PaymentResult>> GetAsync(DateTimeOffset from, DateTimeOffset toExclusive,
        CancellationToken cancellationToken);
}

public sealed class StatisticsPayments(HttpClient client, IHttpContextAccessor context) : IStatisticsPayments
{
    public async IAsyncEnumerable<IReadOnlyList<PaymentResult>> GetAsync(DateTimeOffset from, DateTimeOffset toExclusive,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(context.HttpContext?.Request.Headers.Authorization.ToString(), out var auth)
            || !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(auth.Parameter))
            throw new InvalidDataException("An authenticated admin token is required for payment statistics.");
        SalesCursor? cursor = null;
        var seenIds = new HashSet<Guid>();
        var seenBookings = new HashSet<Guid>();
        do
        {
            var query = $"from={Encode(from)}&toExclusive={Encode(toExclusive)}";
            if (cursor is not null) query += $"&afterPaidAt={Encode(cursor.PaidAt)}&afterPaymentId={cursor.PaymentId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/payments/sales?{query}");
            request.Headers.Authorization = auth;
            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            SalesPage? page;
            try { page = await response.Content.ReadFromJsonAsync<SalesPage>(cancellationToken); }
            catch (JsonException ex) { throw new InvalidDataException("Invalid payment sales response.", ex); }
            if (page?.Payments is null || page.Payments.Length > 500)
                throw new InvalidDataException("Invalid payment sales page.");
            foreach (var payment in page.Payments)
            {
                if (payment is null || payment.PaidAt is null || payment.PaidAt < from || payment.PaidAt >= toExclusive
                    || payment.PaymentId == Guid.Empty || payment.BookingId == Guid.Empty
                    || !seenIds.Add(payment.PaymentId) || !seenBookings.Add(payment.BookingId)
                    || cursor is not null && (payment.PaidAt < cursor.PaidAt
                        || payment.PaidAt == cursor.PaidAt && payment.PaymentId.CompareTo(cursor.PaymentId) <= 0))
                    throw new InvalidDataException("Payment sales page is duplicated, unordered or outside the requested range.");
                cursor = new(payment.PaidAt.Value, payment.PaymentId);
            }
            if (page.Next is not null && (page.Payments.Length == 0 || page.Next != cursor))
                throw new InvalidDataException("Payment sales cursor does not match the last row.");
            yield return page.Payments;
            if (page.Next is null) yield break;
        } while (true);
    }

    private static string Encode(DateTimeOffset value) => Uri.EscapeDataString(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    private sealed record SalesPage(PaymentResult[] Payments, SalesCursor? Next);
    private sealed record SalesCursor(DateTimeOffset PaidAt, Guid PaymentId);
}
