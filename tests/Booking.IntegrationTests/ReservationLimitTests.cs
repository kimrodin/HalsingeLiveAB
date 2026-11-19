using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Booking.Features.BookingHolds;
using Booking.Features.Bookings;
using Booking.Features.Payments.Gateway;
using Booking.Features.Tickets;
using HalsingeLive.ClientAddressing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using IServer = Microsoft.AspNetCore.Hosting.Server.IServer;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Inventory = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.IntegrationTests;

public sealed class ReservationLimitTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    private static ReservationLimitOptions Limits(int attempts = 100, int window = 60, int active = 20, int quantity = 100) => new()
    {
        AttemptsPerWindow = attempts, WindowSeconds = window,
        MaxActiveReservationsPerEvent = active, MaxReservedQuantityPerEvent = quantity,
        HmacKey = new string('b', 64), KeyPrefix = $"booking:limits:tests:{Guid.NewGuid():N}"
    };

    [BookingIntegrationFact]
    public async Task ThreePerSixtySeconds_FourthGets429_OtherAddressWorks_OriginalAddressRecovers()
    {
        var limits = Limits(attempts: 3);
        var inventory = await fixture.CreateInventoryAsync(stock: 100);
        await using var host = await StartHostAsync(limits);
        var started = DateTimeOffset.UtcNow;
        for (var i = 0; i < 3; i++)
            using (var response = await PostAsync(host.Client, inventory))
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using (var request = new HttpRequestMessage(HttpMethod.Post, "/CreateBooking"))
        {
            request.Content = JsonContent.Create(Request(inventory));
            request.Headers.Add("X-Forwarded-For", "198.51.100.88");
            request.Headers.Add("X-Real-IP", "203.0.113.99");
            using var response = await host.Client.SendAsync(request);
            await AssertLimitedAsync(response, "AttemptLimitExceeded", 60);
        }
        Assert.Equal(3, await BookingCountAsync(inventory));
        Assert.Equal(97, await StockAsync(inventory));
        using var other = host.ClientFor(IPAddress.Parse("127.0.0.2"));
        using (var response = await PostAsync(other, inventory))
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var remaining = started.AddSeconds(61) - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
        using var recovered = await PostAsync(host.Client, inventory);
        Assert.Equal(HttpStatusCode.Accepted, recovered.StatusCode);
        Assert.Equal(5, await BookingCountAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task RejectedAttempts_DoNotExtendWindowOrExistingHold()
    {
        var limits = Limits(attempts: 1, window: 3);
        var inventory = await fixture.CreateInventoryAsync();
        await using var host = await StartHostAsync(limits);
        using var accepted = await PostAsync(host.Client, inventory);
        var booking = (await accepted.Content.ReadFromJsonAsync<CreateBookingResponse>())!;
        var index = $"inventory:{inventory.EventId:N}:{inventory.SectionId:N}:reservations";
        var expiry = await fixture.Redis.GetDatabase().SortedSetScoreAsync(index, booking.BookingId.ToString("N"));
        await Task.Delay(1800);
        using var denied = await PostAsync(host.Client, inventory);
        await AssertLimitedAsync(denied, "AttemptLimitExceeded", 3);
        Assert.Equal(expiry, await fixture.Redis.GetDatabase().SortedSetScoreAsync(index, booking.BookingId.ToString("N")));
        await Task.Delay(1500);
        using var recovered = await PostAsync(host.Client, inventory);
        Assert.Equal(HttpStatusCode.Accepted, recovered.StatusCode);
    }

    [BookingIntegrationFact]
    public async Task ActiveCount_CoversAllSectionsOfEvent_ButNotOtherEventsOrAddresses()
    {
        var limits = Limits(active: 2);
        var first = await fixture.CreateInventoryAsync(stock: 100);
        var second = await fixture.CreateInventoryAsync(stock: 100, eventId: first.EventId);
        var otherEvent = await fixture.CreateInventoryAsync();
        await using var host = await StartHostAsync(limits);
        using var one = await PostAsync(host.Client, first);
        using var two = await PostAsync(host.Client, second);
        Assert.Equal(HttpStatusCode.Accepted, one.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, two.StatusCode);
        using var denied = await PostAsync(host.Client, second);
        await AssertLimitedAsync(denied, "ActiveReservationLimitExceeded", 600);
        Assert.Equal(1, await BookingCountAsync(second));
        Assert.Equal(99, await StockAsync(second));
        using var other = await PostAsync(host.Client, otherEvent);
        Assert.Equal(HttpStatusCode.Accepted, other.StatusCode);
        using var otherClient = host.ClientFor(IPAddress.Parse("127.0.0.2"));
        using var otherAddress = await PostAsync(otherClient, second);
        Assert.Equal(HttpStatusCode.Accepted, otherAddress.StatusCode);
    }

    [BookingIntegrationFact]
    public async Task TicketQuota_CancellationReleasesOnlyItsOwnQuantity_AndIsIdempotent()
    {
        var limits = Limits(quantity: 10);
        var first = await fixture.CreateInventoryAsync(stock: 100);
        var second = await fixture.CreateInventoryAsync(stock: 100, eventId: first.EventId);
        await using var host = await StartHostAsync(limits);
        using var one = await PostAsync(host.Client, first, 6);
        using var two = await PostAsync(host.Client, second, 4);
        var booking = (await one.Content.ReadFromJsonAsync<CreateBookingResponse>())!;
        using var denied = await PostAsync(host.Client, second);
        await AssertLimitedAsync(denied, "ReservedQuantityLimitExceeded", 600);
        var reservations = new RedisReservationService(fixture.Redis, Options.Create(limits));
        // Same transition used by cancellation and failed-create compensation.
        for (var i = 0; i < 2; i++)
            Assert.Equal(ReservationFinalizationResult.Completed,
                await reservations.ReleaseReservationAsync(first.EventId, first.SectionId, booking.BookingId));
        Assert.Equal(100, await StockAsync(first));
        Assert.Equal(96, await StockAsync(second));
        using var replacement = await PostAsync(host.Client, second, 6);
        Assert.Equal(HttpStatusCode.Accepted, replacement.StatusCode);
        using var stillLimited = await PostAsync(host.Client, second);
        await AssertLimitedAsync(stillLimited, "ReservedQuantityLimitExceeded", 600);
    }

    [BookingIntegrationFact]
    public async Task ConfirmedPayment_ReleasesActiveQuota_WithoutRestoringSoldStock()
    {
        var limits = Limits(active: 1);
        var inventory = await fixture.CreateInventoryAsync();
        var gateway = new BookingPaymentTests.FakePaymentGateway();
        await using var db = fixture.CreateContext();
        var service = fixture.CreateService(db, gateway, limits);
        var created = await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress);
        var id = created.BookingId!.Value;
        await service.StartPaymentAsync(id);
        await gateway.ProcessAsync(id, inventory.Price, "SEK", CancellationToken.None);
        Assert.Equal(BookingStatuses.Confirmed, (await service.ReconcilePaymentAsync(id)).BookingStatus);
        Assert.Equal(BookingStatuses.Confirmed, (await service.ReconcilePaymentAsync(id)).BookingStatus);
        var replacement = await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress);
        Assert.Equal(ReservationStatus.Reserved, replacement.Status);
        Assert.Equal(8, await StockAsync(inventory));
    }

    [BookingIntegrationFact]
    public async Task OrphanWithoutDatabaseRow_IsCleanedUpByAnotherInstanceExactlyOnce()
    {
        var limits = Limits(active: 1);
        var inventory = await fixture.CreateInventoryAsync();
        var options = Options.Create(limits);
        var reservations = new RedisReservationService(fixture.Redis, options);
        await reservations.InitializeInventoryAsync(inventory.EventId, inventory.SectionId, 10);
        var id = Guid.NewGuid();
        var clientKey = new ReservationClientIdentity(options).GetKey(fixture.ClientAddress);
        var result = await reservations.TryReserveAsync(id, inventory.EventId, inventory.SectionId, 4,
            DateTimeOffset.UtcNow.AddSeconds(1), clientKey);
        Assert.Equal(RedisReservationStatus.Reserved, result.Status);
        var redis = fixture.Redis.GetDatabase();
        var quotaKey = $"{limits.KeyPrefix}:active:{clientKey}:{inventory.EventId:N}";
        var ttl = await redis.KeyTimeToLiveAsync(quotaKey);
        Assert.InRange(ttl!.Value.TotalSeconds, 59, 62);
        Assert.Equal(0, await BookingCountAsync(inventory));
        await Task.Delay(1200);
        using var independent = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("BOOKING_TEST_REDIS")!);
        var cleaner = new RedisReservationService(independent, options);
        Assert.Equal(1, await cleaner.CleanupExpiredReservationsAsync());
        Assert.Equal(0, await cleaner.CleanupExpiredReservationsAsync());
        Assert.Equal(10, await StockAsync(inventory));
        Assert.False(await redis.KeyExistsAsync(quotaKey));
        Assert.False(await redis.KeyExistsAsync($"{limits.KeyPrefix}:owner:{id:N}"));
    }

    [BookingIntegrationFact]
    public async Task FailedDatabaseSave_CompensatesStockAndQuota_ButDoesNotResetAttemptCount()
    {
        var limits = Limits(attempts: 2, active: 1);
        var inventory = await fixture.CreateInventoryAsync();
        await using (var db = fixture.CreateContext(new RejectBookingSave()))
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.CreateService(db, null!, limits)
                .CreateReservationAsync(Request(inventory), fixture.ClientAddress));
        Assert.Equal(0, await BookingCountAsync(inventory));
        Assert.Equal(10, await StockAsync(inventory));
        await using var healthy = fixture.CreateContext();
        var service = fixture.CreateService(healthy, null!, limits);
        Assert.Equal(ReservationStatus.Reserved,
            (await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress)).Status);
        Assert.Equal(ReservationStatus.AttemptLimitExceeded,
            (await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress)).Status);
    }

    [BookingIntegrationFact]
    public async Task TwentyConcurrentRequestsAcrossTwoHttpHosts_RespectSharedLimits()
    {
        foreach (var policy in new[]
        {
            (Options: Limits(attempts: 3), Quantity: 1, Accepted: 3, Code: "AttemptLimitExceeded"),
            (Options: Limits(active: 2), Quantity: 1, Accepted: 2, Code: "ActiveReservationLimitExceeded"),
            (Options: Limits(quantity: 10), Quantity: 3, Accepted: 3, Code: "ReservedQuantityLimitExceeded")
        })
        {
            var inventory = await fixture.CreateInventoryAsync(stock: 100);
            var gate = new ConcurrentRequestGate(20);
            await using var first = await StartHostAsync(policy.Options, gate);
            await using var second = await StartHostAsync(policy.Options, gate);
            var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
                PostAsync(i % 2 == 0 ? first.Client : second.Client, inventory, policy.Quantity)));
            try
            {
                Assert.Equal(policy.Accepted, responses.Count(r => r.StatusCode == HttpStatusCode.Accepted));
                foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Accepted))
                    await AssertLimitedAsync(response, policy.Code, 600);
                Assert.Equal(policy.Accepted, await BookingCountAsync(inventory));
                Assert.Equal(100 - policy.Accepted * policy.Quantity, await StockAsync(inventory));
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
    }

    [BookingIntegrationFact]
    public async Task RedisUnavailable_Returns503WithoutCreatingHold_ThenHealthyInstanceWorks()
    {
        var limits = Limits();
        var inventory = await fixture.CreateInventoryAsync();
        await fixture.Reservations.InitializeInventoryAsync(inventory.EventId, inventory.SectionId, 10);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var config = ConfigurationOptions.Parse($"127.0.0.1:{port}");
        config.AbortOnConnectFail = false;
        config.ConnectTimeout = 100; config.ConnectRetry = 0; config.AsyncTimeout = 100;
        config.BacklogPolicy = BacklogPolicy.FailFast;
        using var unavailable = await ConnectionMultiplexer.ConnectAsync(config);
        await using (var host = await StartHostAsync(limits, connection: unavailable))
        {
            using var response = await PostAsync(host.Client, inventory);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(503, (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
        }
        Assert.Equal(0, await BookingCountAsync(inventory));
        Assert.Equal(10, await StockAsync(inventory));
        await using var healthy = await StartHostAsync(limits);
        using var recovered = await PostAsync(healthy.Client, inventory);
        Assert.Equal(HttpStatusCode.Accepted, recovered.StatusCode);
    }

    [BookingIntegrationFact]
    public async Task BookingCancellationAndExpiry_ReleaseQuotaWithoutResettingAttemptWindow()
    {
        var limits = Limits(attempts: 3, active: 1);
        var inventory = await fixture.CreateInventoryAsync();
        await using var db = fixture.CreateContext();
        var service = fixture.CreateService(db, new BookingPaymentTests.FakePaymentGateway(), limits);
        var first = await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress);
        Assert.Equal(BookingStatuses.Cancelled, (await service.CancelAsync(first.BookingId!.Value)).BookingStatus);
        Assert.Equal(BookingStatuses.Cancelled, (await service.CancelAsync(first.BookingId.Value)).BookingStatus);
        var second = await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress);
        Assert.Equal(ReservationStatus.Reserved, second.Status);
        await db.Bookings.Where(b => b.BookingId == second.BookingId).ExecuteUpdateAsync(update =>
            update.SetProperty(b => b.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
        db.ChangeTracker.Clear(); // ExecuteUpdate bypasses tracked entities; model the next request scope.
        Assert.Equal(BookingStatuses.Expired, (await service.CancelAsync(second.BookingId!.Value)).BookingStatus);
        Assert.Equal(10, await StockAsync(inventory));
        Assert.Equal(ReservationStatus.Reserved,
            (await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress)).Status);
        Assert.Equal(ReservationStatus.AttemptLimitExceeded,
            (await service.CreateReservationAsync(Request(inventory), fixture.ClientAddress)).Status);
    }

    [BookingIntegrationFact]
    public async Task ExpiredQuotaInAnotherSection_IsReclaimedBeforeNewHold()
    {
        var limits = Limits(active: 1);
        var first = await fixture.CreateInventoryAsync();
        var second = await fixture.CreateInventoryAsync(eventId: first.EventId);
        var options = Options.Create(limits);
        var reservations = new RedisReservationService(fixture.Redis, options);
        await reservations.InitializeInventoryAsync(first.EventId, first.SectionId, 10);
        var client = new ReservationClientIdentity(options).GetKey(fixture.ClientAddress);
        Assert.Equal(RedisReservationStatus.Reserved, (await reservations.TryReserveAsync(Guid.NewGuid(),
            first.EventId, first.SectionId, 4, DateTimeOffset.UtcNow.AddSeconds(1), client)).Status);
        await Task.Delay(1200);
        await using var db = fixture.CreateContext();
        Assert.Equal(ReservationStatus.Reserved, (await fixture.CreateService(db, null!, limits)
            .CreateReservationAsync(Request(second), fixture.ClientAddress)).Status);
        // Read raw stock: a GetAvailableQuantity call would itself clean the first section.
        Assert.Equal(10, (int)await fixture.Redis.GetDatabase().StringGetAsync(
            $"inventory:{first.EventId:N}:{first.SectionId:N}:available"));
    }

    [BookingIntegrationFact]
    public async Task QuantityRetryAfter_WaitsForEnoughTickets_AndImpossibleRequestIs400()
    {
        var limits = Limits(quantity: 10);
        var inventory = await fixture.CreateInventoryAsync(stock: 100);
        var options = Options.Create(limits);
        var reservations = new RedisReservationService(fixture.Redis, options);
        await reservations.InitializeInventoryAsync(inventory.EventId, inventory.SectionId, 100);
        var client = new ReservationClientIdentity(options).GetKey(IPAddress.Loopback);
        await reservations.TryReserveAsync(Guid.NewGuid(), inventory.EventId, inventory.SectionId, 4,
            DateTimeOffset.UtcNow.AddSeconds(30), client);
        await reservations.TryReserveAsync(Guid.NewGuid(), inventory.EventId, inventory.SectionId, 6,
            DateTimeOffset.UtcNow.AddSeconds(60), client);
        await using var host = await StartHostAsync(limits);
        using var denied = await PostAsync(host.Client, inventory, 10);
        await AssertLimitedAsync(denied, "ReservedQuantityLimitExceeded", 60);
        Assert.True(denied.Headers.RetryAfter!.Delta!.Value.TotalSeconds > 30);
        Assert.Equal(90, await StockAsync(inventory));
        Assert.Equal(0, await BookingCountAsync(inventory));

        var smallerLimit = Limits(quantity: 2);
        await using var small = await StartHostAsync(smallerLimit);
        using var impossible = await PostAsync(small.Client, inventory, 3);
        Assert.Equal(HttpStatusCode.BadRequest, impossible.StatusCode);
        Assert.Equal(90, await StockAsync(inventory));
    }

    private async Task<TestHost> StartHostAsync(ReservationLimitOptions limits,
        ConcurrentRequestGate? gate = null, IConnectionMultiplexer? connection = null)
    {
        var ownConnection = connection is null;
        connection ??= await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("BOOKING_TEST_REDIS")!);
        var options = Options.Create(limits);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.AddClientAddressing(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(BookingsController).Assembly);
        builder.Services.AddScoped(_ => fixture.CreateContext());
        builder.Services.AddSingleton(new RedisReservationService(connection, options));
        builder.Services.AddSingleton(new RedisReservationAttemptLimiter(connection, options));
        builder.Services.AddSingleton(new ReservationClientIdentity(options));
        builder.Services.AddSingleton(fixture.EventSnapshots);
        builder.Services.AddSingleton<IPaymentGateway, BookingPaymentTests.FakePaymentGateway>();
        builder.Services.AddSingleton<ITicketServiceClient, BookingPaymentHttpTests.StubTicketServiceClient>();
        builder.Services.AddScoped<BookingService>();
        var app = builder.Build();
        app.UseClientAddressing();
        if (gate is not null) app.Use(async (context, next) => { await gate.ArriveAsync(context.RequestAborted); await next(context); });
        app.MapControllers();
        try
        {
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new TestHost(app, new Uri(address), ownConnection ? connection : null);
        }
        catch { await app.DisposeAsync(); if (ownConnection) connection.Dispose(); throw; }
    }

    private static CreateBookingRequest Request(Inventory inventory, int quantity = 1) =>
        new(inventory.InventoryId, quantity, "Limit Customer", "customer@example.com");
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Inventory inventory, int quantity = 1) =>
        client.PostAsJsonAsync("/CreateBooking", Request(inventory, quantity));
    private async Task<int> BookingCountAsync(Inventory inventory)
    {
        await using var db = fixture.CreateContext();
        return await db.Bookings.CountAsync(b => b.Items.Any(i => i.InventoryId == inventory.InventoryId));
    }
    private Task<int> StockAsync(Inventory inventory) => fixture.Reservations.GetAvailableQuantityAsync(
        inventory.EventId, inventory.SectionId, inventory.TotalQuantity);
    private static async Task AssertLimitedAsync(HttpResponseMessage response, string code, int maximumWait)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.InRange(response.Headers.RetryAfter!.Delta!.Value.TotalSeconds, 1, maximumWait);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal(429, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.Equal(code, problem.Extensions["code"]?.ToString());
    }

    private sealed class RejectBookingSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated persistence failure after Redis reservation.");
    }

    private sealed class TestHost(WebApplication app, Uri address, IConnectionMultiplexer? connection) : IAsyncDisposable
    {
        public HttpClient Client { get; } = new() { BaseAddress = address };
        public HttpClient ClientFor(IPAddress source) => new(new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    socket.Bind(new IPEndPoint(source, 0));
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        }) { BaseAddress = address };
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await app.DisposeAsync(); connection?.Dispose();
        }
    }
}
