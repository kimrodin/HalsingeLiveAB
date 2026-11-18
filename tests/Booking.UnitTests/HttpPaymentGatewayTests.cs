using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Booking.Features.Payments.Gateway;
using Microsoft.Extensions.Configuration;

namespace Booking.UnitTests;

public sealed class HttpPaymentGatewayTests
{
    private const string ApiKey = "payment-gateway-test-key";

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

        var result = await Gateway(client).ProcessAsync(bookingId, 299.50m, "SEK", default);

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

        Assert.Null(await Gateway(client).GetByBookingAsync(bookingId, default));
    }

    [Fact]
    public async Task LookupAsync_DoesNotInterpretServiceFailureAsMissingPayment()
    {
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Gateway(client).GetByBookingAsync(Guid.NewGuid(), default));
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

        Assert.Equal(payment, await Gateway(client).RefundAsync(payment.PaymentId, default));
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

        Assert.Equal(payment, await Gateway(client).CancelAsync(
            payment.BookingId, payment.Amount, payment.Currency, default));
    }

    [Fact]
    public async Task ProcessAsync_PropagatesNetworkFailureForWorkflowReconciliation()
    {
        using var client = CreateClient(_ => throw new HttpRequestException("Connection closed before response."));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Gateway(client).ProcessAsync(Guid.NewGuid(), 299.50m, "SEK", default));
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("TimedOut")]
    public async Task ProcessAsync_ReturnsThePaymentOutcomeFromASuccessfulHttpResponse(string status)
    {
        var payment = Payment(Guid.NewGuid()) with { Status = status };
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payment)
        }));

        var result = await Gateway(client).ProcessAsync(payment.BookingId, payment.Amount, "SEK", default);

        Assert.Equal(status, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task ProcessAsync_MapsRejectedRequestsToContractErrors(HttpStatusCode status)
    {
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(status)));

        var error = await Assert.ThrowsAsync<PaymentContractException>(() =>
            Gateway(client).ProcessAsync(Guid.NewGuid(), 299.50m, "SEK", default));

        Assert.Equal(status, error.StatusCode);
    }

    [Fact]
    public async Task RefundAsync_MissingPaymentIsAContractErrorInsteadOfAnAbsentBooking()
    {
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var error = await Assert.ThrowsAsync<PaymentContractException>(() =>
            Gateway(client).RefundAsync(Guid.NewGuid(), default));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ProcessAsync_PreservesUncertainServiceErrorsForReconciliation(HttpStatusCode status)
    {
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(status)));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Gateway(client).ProcessAsync(Guid.NewGuid(), 299.50m, "SEK", default));

        Assert.Equal(status, error.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("")]
    public async Task ProcessAsync_MapsMalformedSuccessfulResponsesToContractErrors(string body)
    {
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));

        await Assert.ThrowsAsync<PaymentContractException>(() =>
            Gateway(client).ProcessAsync(Guid.NewGuid(), 299.50m, "SEK", default));
    }

    [Fact]
    public async Task Missing_internal_key_does_not_send_a_payment_request()
    {
        using var client = CreateClient(_ => throw new Xunit.Sdk.XunitException("An unauthenticated request was sent."));
        var gateway = new HttpPaymentGateway(client, new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<PaymentContractException>(() =>
            gateway.ProcessAsync(Guid.NewGuid(), 299.50m, "SEK", default));
    }

    private static HttpPaymentGateway Gateway(HttpClient client) => new(client,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentSimulator:InternalApiKey"] = ApiKey
        }).Build());

    private static PaymentResult Payment(Guid bookingId) => new(Guid.NewGuid(), bookingId, 299.50m, "SEK",
        "Succeeded", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

    private static HttpClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) =>
        new(new StubHandler(handle)) { BaseAddress = new Uri("http://payment-simulator/") };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(ApiKey, Assert.Single(request.Headers.GetValues("X-Internal-Api-Key")));
            return handle(request);
        }
    }
}
