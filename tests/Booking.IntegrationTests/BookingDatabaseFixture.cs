using Booking.Data;
using Booking.Features.BookingHolds;
using Booking.Features.Bookings;
using Booking.Features.EventCatalog;
using Booking.Features.Payments.Gateway;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using StackExchange.Redis;
using Microsoft.Extensions.Options;
using System.Net;
using Inventory = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.IntegrationTests;

public sealed class BookingIntegrationFactAttribute : FactAttribute
{
    public BookingIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKING_TEST_POSTGRES"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKING_TEST_REDIS")))
            Skip = "Set BOOKING_TEST_POSTGRES and BOOKING_TEST_REDIS to run against PostgreSQL and Redis.";
    }
}

public sealed class BookingDatabaseFixture : IAsyncLifetime
{
    private readonly string databaseName = $"booking_tests_{Guid.NewGuid():N}";
    private readonly List<Inventory> inventories = [];
    private readonly List<Guid> bookingIds = [];
    private readonly Dictionary<Guid, string> guestAccessTokens = [];
    private string? adminConnectionString;
    private NpgsqlDataSource? dataSource;
    public IConnectionMultiplexer Redis { get; private set; } = null!;
    // These suites test stock/payment behavior rather than IP policy (including a 200-request race).
    public ReservationLimitOptions Limits { get; } = new()
    {
        AttemptsPerWindow = 10000, MaxActiveReservationsPerEvent = 1000, MaxReservedQuantityPerEvent = 100000,
        HmacKey = new string('a', 64), KeyPrefix = $"booking:limits:tests:{Guid.NewGuid():N}"
    };
    public IPAddress ClientAddress { get; } = IPAddress.Parse("192.0.2.50");
    public RedisReservationService Reservations => new(Redis, Options.Create(Limits));
    public IEventSnapshotProvider EventSnapshots => new TestEventSnapshotProvider();
    public string GetGuestAccessToken(Guid bookingId) => guestAccessTokens[bookingId];

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("BOOKING_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKING_TEST_REDIS")))
            return;

        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres", Pooling = false };
        adminConnectionString = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        builder.Database = databaseName;
        // Hundreds of requests share a bounded pool instead of exhausting PostgreSQL's connections.
        builder.Pooling = true;
        builder.MaxPoolSize = 20;
        dataSource = NpgsqlDataSource.Create(builder.ConnectionString);
        Redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("BOOKING_TEST_REDIS")!);
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    public BookingDbContext CreateContext(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<BookingDbContext>()
        .UseNpgsql(dataSource ?? throw new InvalidOperationException("Integration test environment is not configured."))
        .AddInterceptors(interceptors)
        .Options);

    public BookingService CreateService(BookingDbContext db, IPaymentGateway gateway, ReservationLimitOptions? limits = null)
    {
        var options = Options.Create(limits ?? Limits);
        return new(db, new RedisReservationService(Redis, options), gateway, new TestEventSnapshotProvider(),
            new RedisReservationAttemptLimiter(Redis, options), new ReservationClientIdentity(options));
    }

    public async Task<Inventory> CreateInventoryAsync(int stock = 10, decimal price = 125.50m, Guid? eventId = null)
    {
        var inventory = new Inventory
        {
            InventoryId = Guid.NewGuid(), EventId = eventId ?? Guid.NewGuid(), SectionId = Guid.NewGuid(),
            TotalQuantity = stock, AvailableQuantity = stock, Price = price
        };
        await using var db = CreateContext();
        db.EventInventory.Add(inventory);
        await db.SaveChangesAsync();
        inventories.Add(inventory);
        return inventory;
    }

    public async Task<Guid> ReserveAsync(Inventory inventory, IPaymentGateway gateway, int quantity = 1)
    {
        await using var db = CreateContext();
        var result = await CreateService(db, gateway).CreateReservationAsync(new CreateBookingRequest(
            inventory.InventoryId, quantity, "Integration Customer", "customer@example.com"), ClientAddress);
        Assert.Equal(ReservationStatus.Reserved, result.Status);
        var id = result.BookingId!.Value;
        bookingIds.Add(id);
        guestAccessTokens.Add(id, result.GuestAccessToken);
        return id;
    }

    public async Task DisposeAsync()
    {
        if (Redis is not null)
        {
            foreach (var inventory in inventories)
                await Reservations.DeleteInventoryAsync(inventory.EventId, inventory.SectionId);
            foreach (var id in bookingIds)
                await Redis.GetDatabase().KeyDeleteAsync(new RedisKey[]
                {
                    $"reservation:{id:N}", $"reservation-expired:{id:N}",
                    $"reservation-confirmed:{id:N}", $"reservation-cancelled:{id:N}"
                });
            await Redis.CloseAsync();
            Redis.Dispose();
        }

        if (dataSource is not null)
            await dataSource.DisposeAsync();

        if (adminConnectionString is null)
            return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    private sealed class TestEventSnapshotProvider : IEventSnapshotProvider
    {
        public Task<EventSnapshot> GetSnapshotAsync(
            Guid eventId,
            Guid sectionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EventSnapshot(
                eventId,
                "Integration event",
                DateTimeOffset.UtcNow.AddDays(1),
                "Integration venue",
                "Integration section"));
    }
}
