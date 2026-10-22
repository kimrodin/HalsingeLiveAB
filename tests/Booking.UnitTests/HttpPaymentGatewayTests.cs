using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Features.Payments;

namespace Booking.UnitTests;

public sealed class HttpPaymentGatewayTests
{
    [Fact]
    public async Task ProcessAsync_SendsServerSuppliedBookingAmountAndCurrency()
    {
        var bookingId = Guid.NewGuid();
        var expected = Payment(bookingId);
        using var client = CreateClient(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/payments", request.RequestUri!.AbsolutePath);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(bookingId, json.RootElement.GetProperty("bookingId").GetGuid());
            Assert.Equal(299.50m, json.RootElement.GetProperty("amount").GetDecimal());
            Assert.Equal("SEK", json.RootElement.GetProperty("currency").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) };
        });

        var result = await new HttpPaymentGateway(client).ProcessAsync(bookingId, 299.50m, "SEK", default);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task LookupAsync_ReturnsNullOnlyWhenPaymentDoesNotExist()
    {
        var bookingId = Guid.NewGuid();
        using var client = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal($"/api/payments/by-booking/{bookingId}", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        Assert.Null(await new HttpPaymentGateway(client).GetByBookingAsync(bookingId, default));
    }

    [Fact]
    public async Task LookupAsync_DoesNotInterpretServiceFailureAsMissingPayment()
    {
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new HttpPaymentGateway(client).GetByBookingAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task RefundAsync_UsesTheExistingPaymentIdentifier()
    {
        var payment = Payment(Guid.NewGuid()) with { RefundedAt = DateTimeOffset.UtcNow };
        using var client = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/api/payments/{payment.PaymentId}/refund", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payment) });
        });

        Assert.Equal(payment, await new HttpPaymentGateway(client).RefundAsync(payment.PaymentId, default));
    }

    [Fact]
    public async Task CancelAsync_CreatesACancellationFenceUsingTheBookingDetails()
    {
        var payment = Payment(Guid.NewGuid()) with { Status = "TimedOut", RefundedAt = DateTimeOffset.UtcNow };
        using var client = CreateClient(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/payments/cancel", request.RequestUri!.AbsolutePath);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(payment.BookingId, json.RootElement.GetProperty("bookingId").GetGuid());
            Assert.Equal(payment.Amount, json.RootElement.GetProperty("amount").GetDecimal());
            Assert.Equal(payment.Currency, json.RootElement.GetProperty("currency").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payment) };
        });

        Assert.Equal(payment, await new HttpPaymentGateway(client).CancelAsync(
            payment.BookingId, payment.Amount, payment.Currency, default));
    }

    [Fact]
    public async Task ProcessAsync_PropagatesNetworkFailureForWorkflowReconciliation()
    {
        using var client = CreateClient(_ => throw new HttpRequestException("Connection closed before response."));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new HttpPaymentGateway(client).ProcessAsync(Guid.NewGuid(), 299.50m, "SEK", default));
    }

    private static PaymentResult Payment(Guid bookingId) => new(Guid.NewGuid(), bookingId, 299.50m, "SEK",
        "Succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) =>
        new(new StubHandler(handle)) { BaseAddress = new Uri("http://payment-simulator/") };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            handle(request);
    }
}
