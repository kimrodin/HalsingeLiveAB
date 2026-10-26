using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.GetPayment;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;

namespace PaymentSimulator.IntegrationTests;

// Each test owns a random PostgreSQL database. The configured account must have CREATEDB.
public sealed class PaymentProcessingTests : IAsyncLifetime
{
    private readonly string databaseName = "payment_test_" + Guid.NewGuid().ToString("N");
    private string connectionString = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("PAYMENT_TEST_DB")
            ?? "Host=localhost;Port=5434;Database=postgres;Username=postgres;Password=postgres";
        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await command.ExecuteNonQueryAsync();
        builder.Database = databaseName;
        connectionString = builder.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        NpgsqlConnection.ClearAllPools();
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    private PaymentDbContext CreateDb() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql(connectionString).Options);

    private static ProcessPaymentHandler Handler(PaymentDbContext db, string outcome = "Succeeded",
        int delay = 0, int timeout = 5000) => new(db, new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentSimulation:Outcome"] = outcome,
            ["PaymentSimulation:DelayMs"] = delay.ToString(),
            ["PaymentSimulation:TimeoutMs"] = timeout.ToString()
        }).Build(), new RefundPaymentHandler(db));

    [Theory]
    [InlineData("Succeeded", PaymentStatus.Succeeded)]
    [InlineData("Failed", PaymentStatus.Failed)]
    [InlineData("TimedOut", PaymentStatus.TimedOut)]
    public async Task Outcome_is_persisted_and_reused(string outcome, PaymentStatus expected)
    {
        await using var db = CreateDb();
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 125.50m, "SEK");
        var result = await Handler(db, outcome).Handle(request, default);
        Assert.Equal(expected, result.Status);
        Assert.NotNull(result.CompletedAt);
        var repeat = await Handler(db, "Failed").Handle(request, default);
        Assert.Equal(result, repeat);
        var lookup = await new GetPaymentHandler(db).GetByBooking(request.BookingId, default);
        Assert.Equal(result.PaymentId, lookup!.PaymentId);
    }

    [Fact]
    public async Task Concurrent_requests_share_one_payment()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var db = CreateDb();
            return await Handler(db, delay: 100).Handle(request, default);
        }));
        Assert.Single(results.Select(r => r.PaymentId).Distinct());
        Assert.All(results, r => Assert.Equal(PaymentStatus.Succeeded, r.Status));
        await using var verify = CreateDb();
        Assert.Equal(1, await verify.Payments.CountAsync());
    }

    [Fact]
    public async Task Request_cancellation_leaves_durable_work_for_recovery()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        await using var db = CreateDb();
        using var cancelled = new CancellationTokenSource();
        var task = Handler(db, delay: 10000, timeout: 20000).Handle(request, cancelled.Token);
        await using var verify = CreateDb();
        for (var count = 0; !await verify.Payments.AnyAsync() && count < 200; count++)
            await Task.Delay(10);
        Assert.True(await verify.Payments.AnyAsync());
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        await verify.Payments.ExecuteUpdateAsync(s => s.SetProperty(p => p.CompleteAfter,
            DateTimeOffset.UtcNow.AddSeconds(-1)));
        // A new handler after restart uses the stored plan despite different current settings.
        await Handler(verify, "Failed").CompleteDuePayments(default);
        Assert.Equal(PaymentStatus.Succeeded, (await verify.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Delay_beyond_timeout_never_later_succeeds()
    {
        await using var db = CreateDb();
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        var result = await Handler(db, delay: 200, timeout: 10).Handle(request, default);
        Assert.Equal(PaymentStatus.TimedOut, result.Status);
        await Handler(db).CompleteDuePayments(default);
        Assert.Equal(result, await Handler(db).Handle(request, default));
    }

    [Fact]
    public async Task Successful_refund_is_idempotent_and_preserves_original_outcome()
    {
        await using var db = CreateDb();
        var payment = await Handler(db).Handle(new(Guid.NewGuid(), 100m, "SEK"), default);
        var refund = new RefundPaymentHandler(db);
        var first = await refund.Handle(payment.PaymentId, default);
        Assert.Equal(PaymentStatus.Succeeded, first!.Status);
        Assert.NotNull(first.RefundedAt);
        Assert.Equal(first, await refund.Handle(payment.PaymentId, default));
    }

    [Fact]
    public async Task Cancel_fences_delayed_creation_and_pending_completion()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        await using var db = CreateDb();
        var cancelled = await Handler(db).Cancel(request, default);
        Assert.Equal(PaymentStatus.TimedOut, cancelled.Status);
        Assert.NotNull(cancelled.RefundedAt);
        Assert.Equal(cancelled, await Handler(db).Handle(request, default));

        var pending = new Payment
        {
            Id = Guid.NewGuid(), BookingId = Guid.NewGuid(), Amount = 100m, Currency = "SEK",
            CreatedAt = DateTimeOffset.UtcNow, CompleteAfter = DateTimeOffset.UtcNow.AddSeconds(-1),
            PlannedOutcome = PaymentStatus.Succeeded, Status = PaymentStatus.Pending
        };
        db.Payments.Add(pending);
        await db.SaveChangesAsync();
        await new RefundPaymentHandler(db).Handle(pending.Id, default);
        await Handler(db).CompleteDuePayments(default);
        var stored = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == pending.Id);
        Assert.Equal(PaymentStatus.TimedOut, stored.Status);
        Assert.NotNull(stored.RefundedAt);
    }

    [Fact]
    public async Task Changed_amount_is_rejected_instead_of_reusing_wrong_payment()
    {
        await using var db = CreateDb();
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        await Handler(db).Handle(request, default);
        await Assert.ThrowsAsync<PaymentConflictException>(() =>
            Handler(db).Handle(request with { Amount = 101m }, default));
    }

    [Fact]
    public async Task EnsureCreated_database_is_safely_adopted_without_losing_payments()
    {
        await using var db = CreateDb();
        // Roll back the new columns, then remove history to reproduce the original EnsureCreated schema.
        await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
            .MigrateAsync("20261022080921_InitialCreate");
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\"");
        var id = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Payments" ("Id", "BookingId", "Amount", "Currency", "Status", "CreatedAt")
            VALUES ({id}, {bookingId}, {100m}, 'SEK', 'Pending', {DateTimeOffset.UtcNow})
            """);
        await PaymentDatabaseMigration.MigrateAsync(db);
        await Handler(db).CompleteDuePayments(default);
        var recovered = await db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal(id, recovered.Id);
        Assert.Equal(PaymentStatus.TimedOut, recovered.Status);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Startup_migration_creates_missing_database()
    {
        await using var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
        await PaymentDatabaseMigration.MigrateAsync(db);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
