using Booking.Data;
using Booking.Features.BookingHolds;
using Booking.Features.Bookings;
using Booking.Features.EventCatalog;
using Booking.Features.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;
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
    private string? adminConnectionString;
    private string? connectionString;
    public IConnectionMultiplexer Redis { get; private set; } = null!;
    public RedisReservationService Reservations => new(Redis);
    public IEventSnapshotProvider EventSnapshots => new TestEventSnapshotProvider();

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
        connectionString = builder.ConnectionString;
        Redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("BOOKING_TEST_REDIS")!);
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    public BookingDbContext CreateContext() => new(new DbContextOptionsBuilder<BookingDbContext>()
        .UseNpgsql(connectionString ?? throw new InvalidOperationException("Integration test environment is not configured."))
        .Options);

    public BookingService CreateService(BookingDbContext db, IPaymentGateway gateway) =>
        new(db, Reservations, gateway, new TestEventSnapshotProvider());

    public async Task<Inventory> CreateInventoryAsync(int stock = 10, decimal price = 125.50m)
    {
        var inventory = new Inventory
        {
            InventoryId = Guid.NewGuid(), EventId = Guid.NewGuid(), SectionId = Guid.NewGuid(),
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
            inventory.InventoryId, quantity, "Integration Customer", "customer@example.com"));
        Assert.Equal(ReservationStatus.Reserved, result.Status);
        var id = result.BookingId!.Value;
        bookingIds.Add(id);
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
