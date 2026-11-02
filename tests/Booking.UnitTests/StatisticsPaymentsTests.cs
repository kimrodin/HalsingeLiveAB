using System.Net;
using System.Net.Http.Json;
using Booking.Features.Payments;
using Booking.Features.Statistics;
using Microsoft.AspNetCore.Http;

namespace Booking.UnitTests;

public sealed class StatisticsPaymentsTests
{
    private static readonly DateTimeOffset From = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
    private static readonly DateTimeOffset To = From.AddDays(1);

    [Fact]
    public async Task RangeAndAdminTokenAreForwardedAndCursorPagesAreFollowed()
    {
        var first = Payment();
        var second = Payment() with { PaidAt = first.PaidAt!.Value.AddHours(1) };
        var calls = 0;
        using var client = Client(async (request, ct) =>
        {
            await Task.CompletedTask;
            Assert.Equal("Bearer admin-token", request.Headers.Authorization!.ToString());
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            Assert.Contains(From.ToString("O"), query);
            Assert.Contains(To.ToString("O"), query);
            if (++calls == 1)
            {
                Assert.DoesNotContain("afterPaymentId", query);
                return Page([first], new { first.PaidAt, first.PaymentId });
            }
            Assert.Contains($"afterPaymentId={first.PaymentId}", query);
            return Page([second]);
        });
        var pages = await Read(new StatisticsPayments(client, Context()));
        Assert.Equal(2, calls);
        Assert.Equal(new[] { first.BookingId, second.BookingId }, pages.SelectMany(p => p).Select(p => p.BookingId));
    }

    private static PaymentResult Payment() => new(Guid.NewGuid(), Guid.NewGuid(), 100, "SEK", "Succeeded", From, From, From, null);
    private static HttpContextAccessor Context()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer admin-token";
        return new() { HttpContext = context };
    }
    private static HttpResponseMessage Page(PaymentResult[] payments, object? next = null) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new { Payments = payments, Next = next }) };
    private static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new Stub(send)) { BaseAddress = new Uri("http://payments/") };
    private static async Task<List<IReadOnlyList<PaymentResult>>> Read(StatisticsPayments gateway)
    {
        var pages = new List<IReadOnlyList<PaymentResult>>();
        await foreach (var page in gateway.GetAsync(From, To, default)) pages.Add(page);
        return pages;
    }
    private sealed class Stub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
