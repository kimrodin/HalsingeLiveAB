using System.Net;
using System.Collections.Concurrent;
using System.Security.Claims;
using EventCatalog.Features.Events.CreateEvent;
using Microsoft.AspNetCore.Http;

namespace EventCatalog.UnitTests.Features.Events;

public sealed class HttpBookingInventoryClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task Provisioning_ForwardsCallingAdminToken(HttpStatusCode status)
    {
        var eventId = Guid.NewGuid();
        using var handler = new BookingHandler(status, eventId);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://booking.example/") };
        var client = new HttpBookingInventoryClient(http, CreateAccessor());
        await client.EnsureInventoryCreatedAsync(eventId, default);
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task RejectedCredentials_FailWithoutRetrying(HttpStatusCode status)
    {
        var eventId = Guid.NewGuid();
        using var handler = new BookingHandler(status, eventId);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://booking.example/") };
        var client = new HttpBookingInventoryClient(http, CreateAccessor());
        await Assert.ThrowsAsync<BookingInventoryProvisioningException>(
            () => client.EnsureInventoryCreatedAsync(eventId, default));
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData(null, true, "admin")]
    [InlineData("Basic credentials", true, "admin")]
    [InlineData("Bearer admin-token", false, "admin")]
    [InlineData("Bearer admin-token", true, "customer")]
    public async Task MissingAdminCredentials_FailWithoutSendingRequest(string? header, bool authenticated, string role)
    {
        var eventId = Guid.NewGuid();
        using var handler = new BookingHandler(HttpStatusCode.Created, eventId);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://booking.example/") };
        var client = new HttpBookingInventoryClient(http, CreateAccessor(header, authenticated, role));
        await Assert.ThrowsAsync<BookingInventoryProvisioningException>(
            () => client.EnsureInventoryCreatedAsync(eventId, default));
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task ConcurrentAdministrators_KeepTheirOwnTokens()
    {
        var accessor = new HttpContextAccessor();
        using var handler = new ConcurrentBookingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://booking.example/") };
        var client = new HttpBookingInventoryClient(http, accessor);
        var calls = Enumerable.Range(0, 20).Select(index => Task.Run(async () =>
        {
            var eventId = Guid.NewGuid();
            var token = $"admin-token-{index}";
            accessor.HttpContext = CreateAccessor($"Bearer {token}").HttpContext;
            try
            {
                await client.EnsureInventoryCreatedAsync(eventId, default);
                Assert.Equal(token, handler.Tokens[eventId]);
            }
            finally
            {
                accessor.HttpContext = null;
            }
        }));
        await Task.WhenAll(calls);
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    private static IHttpContextAccessor CreateAccessor(
        string? header = "Bearer admin-token", bool authenticated = true, string role = "admin")
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("roles", role)],
                authenticated ? "Bearer" : null, "preferred_username", "roles"))
        };
        if (header is not null)
            context.Request.Headers.Authorization = header;
        return new HttpContextAccessor { HttpContext = context };
    }

    private sealed class ConcurrentBookingHandler : HttpMessageHandler
    {
        public ConcurrentDictionary<Guid, string> Tokens { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Yield();
            var eventId = Guid.Parse(request.RequestUri!.Segments.Last());
            Tokens[eventId] = request.Headers.Authorization!.Parameter!;
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }

    private sealed class BookingHandler(HttpStatusCode status, Guid eventId) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/CreateEventInventoryFromCatalog/from-catalog/{eventId}", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("admin-token", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
