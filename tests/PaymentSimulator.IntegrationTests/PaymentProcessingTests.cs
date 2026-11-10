using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.GetPayment;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging;
using HalsingeLive.Contracts.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

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
        using var pool = new NpgsqlConnection(connectionString);
        NpgsqlConnection.ClearPool(pool);
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task SeedPayments_CreatesHistoricalPaymentsAndDeletesOnlySeedRows()
    {
        var row = new PaymentSimulator.Features.Seed.SeedPayment(
            Guid.Parse("51000000-0001-0001-0000-000000000001"), 100m,
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        var ordinaryId = Guid.NewGuid();
        await using var db = CreateDb();
        var handler = new PaymentSimulator.Features.Seed.SeedPaymentsHandler(db);
        Assert.Equal(1, await handler.Handle([row], default));
        Assert.Equal(0, await handler.Handle([row], default));
        var payment = Assert.Single(await db.Payments.AsNoTracking().ToArrayAsync());
        Assert.Equal(row.PaidAt, payment.PaidAt);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        db.Payments.Add(new Payment { Id = Guid.NewGuid(), BookingId = ordinaryId, Amount = 125m,
            Currency = "SEK", Status = PaymentStatus.Succeeded, PlannedOutcome = PaymentStatus.Succeeded,
            CreatedAt = row.PaidAt, PaidAt = row.PaidAt, CompleteAfter = row.PaidAt });
        await db.SaveChangesAsync();
        Assert.Equal(1, await handler.Delete(default));
        Assert.Equal(0, await handler.Delete(default));
        Assert.Equal(ordinaryId, Assert.Single(await db.Payments.AsNoTracking().ToArrayAsync()).BookingId);
    }

    [Fact]
    public async Task SalesRange_ExcludesOldPaymentsAndPaginatesTiedTimestamps()
    {
        await using var db = CreateDb();
        var paid = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
        var expected = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var id = Guid.NewGuid();
            var date = i == 0 ? paid.AddYears(-1) : i == 3 ? paid.AddDays(1) : paid;
            if (date == paid) expected.Add(id);
            db.Payments.Add(new Payment
            {
                Id = id, BookingId = Guid.NewGuid(), Amount = i == 0 ? 99m : 125m, Currency = "SEK",
                Status = PaymentStatus.Succeeded, PlannedOutcome = PaymentStatus.Succeeded,
                CreatedAt = date, CompleteAfter = date, CompletedAt = date, PaidAt = date
            });
        }
        await db.SaveChangesAsync();
        var controller = new PaymentSimulator.Features.Payments.Statistics.PaymentSalesController(db);
        var found = new List<Guid>();
        PaymentSimulator.Features.Payments.Statistics.PaymentSalesCursor? cursor = null;
        do
        {
            var result = await controller.Get(new(paid, paid.AddDays(1), cursor?.PaidAt, cursor?.PaymentId, 1), default);
            var page = Assert.IsType<PaymentSimulator.Features.Payments.Statistics.PaymentSalesPage>(
                Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result).Value);
            Assert.Equal(125m, Assert.Single(page.Payments).Amount);
            found.Add(page.Payments[0].PaymentId);
            cursor = page.Next;
        } while (cursor is not null);
        Assert.Equal(expected.Order(), found);
        Assert.Equal(2, found.Count);
    }

    private PaymentDbContext CreateDb() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql(connectionString).Options);

    private static ProcessPaymentHandler Handler(PaymentDbContext db, string outcome = "Succeeded",
        int delay = 0, int timeout = 5000) => new(db, new SimulationSettingsService(db, new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PaymentSimulation:Outcome"] = outcome,
            ["PaymentSimulation:DelayMs"] = delay.ToString(),
            ["PaymentSimulation:TimeoutMs"] = timeout.ToString()
        }).Build()), new RefundPaymentHandler(db));

    [Fact]
    public async Task Settings_survive_new_context_and_only_affect_new_payments()
    {
        var originalRequest = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        await using (var db = CreateDb())
        {
            var defaults = new ConfigurationBuilder().Build();
            var settings = new SimulationSettingsService(db, defaults);
            Assert.Equal(new(SimulationOutcome.Succeeded, 500, 5000), await settings.Get(default));
            await Handler(db).Handle(originalRequest, default);
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(), BookingId = Guid.NewGuid(), Amount = 100m, Currency = "SEK",
                CreatedAt = DateTimeOffset.UtcNow, CompleteAfter = DateTimeOffset.UtcNow.AddSeconds(-1),
                PlannedOutcome = PaymentStatus.Succeeded, Status = PaymentStatus.Pending
            });
            await db.SaveChangesAsync();
            await settings.Update(new(SimulationOutcome.Failed, 0, 5000), default);
        }

        await using var fresh = CreateDb();
        var saved = new SimulationSettingsService(fresh, new ConfigurationBuilder().Build());
        Assert.Equal(new(SimulationOutcome.Failed, 0, 5000), await saved.Get(default));
        var handler = Handler(fresh);
        Assert.Equal(PaymentStatus.Succeeded, (await handler.Handle(originalRequest, default)).Status);
        await handler.CompleteDuePayments(default);
        Assert.Equal(2, await fresh.Payments.CountAsync(p => p.Status == PaymentStatus.Succeeded));
        Assert.Equal(PaymentStatus.Failed,
            (await handler.Handle(new(Guid.NewGuid(), 100m, "SEK"), default)).Status);
        await saved.Update(new(SimulationOutcome.TimedOut, 0, 5000), default);
        Assert.Equal(PaymentStatus.TimedOut,
            (await handler.Handle(new(Guid.NewGuid(), 100m, "SEK"), default)).Status);
        await saved.Update(new(SimulationOutcome.Succeeded, 10, 1), default);
        Assert.Equal(PaymentStatus.TimedOut,
            (await handler.Handle(new(Guid.NewGuid(), 100m, "SEK"), default)).Status);
    }

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
        var stored = await db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal(TimeSpan.FromMilliseconds(expected == PaymentStatus.TimedOut ? 5000 : 0),
            stored.CompleteAfter - stored.CreatedAt);
        Assert.True(result.CompletedAt >= stored.CompleteAfter);
        var repeat = await Handler(db, "Failed").Handle(request, default);
        Assert.Equal(result, repeat);
        var lookup = await new GetPaymentHandler(db).GetByBooking(request.BookingId, default);
        Assert.Equal(result.PaymentId, lookup!.PaymentId);
        var outbox = Assert.Single(await db.PaymentResultOutboxMessages.AsNoTracking().ToListAsync());
        Assert.Equal(result.PaymentId, outbox.PaymentId);
        Assert.Equal(request.BookingId, outbox.BookingId);
        Assert.Equal(expected.ToString(), outbox.Outcome);
        Assert.Equal(result.PaidAt, outbox.PaidAtUtc);
        Assert.Equal(result.CompletedAt, outbox.OccurredAtUtc);
        Assert.Null(outbox.DeliveredAtUtc);
    }

    [Fact]
    public async Task Random_settings_are_persisted_and_attempts_reuse_their_selected_outcome()
    {
        await using (var db = CreateDb())
        {
            await new SimulationSettingsService(db, new ConfigurationBuilder().Build())
                .Update(new(SimulationOutcome.Random, 0, 5000), default);
        }
        await using var fresh = CreateDb();
        var settings = new SimulationSettingsService(fresh, new ConfigurationBuilder().Build());
        Assert.Equal(SimulationOutcome.Random, (await settings.Get(default)).Outcome);
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        var result = await Handler(fresh).Handle(request, default);
        Assert.Contains(result.Status, new[] { PaymentStatus.Succeeded, PaymentStatus.Failed, PaymentStatus.TimedOut });
        var stored = await fresh.Payments.AsNoTracking().SingleAsync();
        Assert.Equal(result.Status, stored.PlannedOutcome);
        Assert.Equal(TimeSpan.FromMilliseconds(result.Status == PaymentStatus.TimedOut ? 5000 : 0),
            stored.CompleteAfter - stored.CreatedAt);
        Assert.True(result.CompletedAt >= stored.CompleteAfter);
        Assert.Equal(result, await Handler(fresh).Handle(request, default));

        await settings.Update(new(SimulationOutcome.Random, 10, 1), default);
        Assert.Equal(PaymentStatus.TimedOut,
            (await Handler(fresh).Handle(new(Guid.NewGuid(), 100m, "SEK"), default)).Status);
    }

    [Fact]
    public async Task TimedOut_waits_for_timeout_and_recovery_reuses_the_saved_deadline()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        await using (var db = CreateDb())
        {
            var handler = Handler(db, "TimedOut", delay: 0, timeout: 30000);
            var accepted = await handler.AcceptAsync(request, default);
            Assert.Equal(PaymentStatus.Pending, accepted.Status);
            var stored = await db.Payments.AsNoTracking().SingleAsync();
            Assert.Equal(PaymentStatus.TimedOut, stored.PlannedOutcome);
            Assert.Equal(TimeSpan.FromSeconds(30), stored.CompleteAfter - stored.CreatedAt);

            await handler.CompleteDuePayments(default);
            var pending = await db.Payments.AsNoTracking().SingleAsync();
            Assert.Equal(PaymentStatus.Pending, pending.Status);
            Assert.Null(pending.CompletedAt);
            Assert.Empty(await db.PaymentResultOutboxMessages.ToListAsync());
        }

        await using var fresh = CreateDb();
        var retry = Handler(fresh, "Succeeded", delay: 0, timeout: 1);
        Assert.Equal(PaymentStatus.Pending, (await retry.AcceptAsync(request, default)).Status);
        await retry.CompleteDuePayments(default);
        Assert.Equal(PaymentStatus.Pending, (await fresh.Payments.AsNoTracking().SingleAsync()).Status);

        // Move the saved deadline into the past to exercise recovery without a 30-second sleep.
        await fresh.Payments.ExecuteUpdateAsync(update => update.SetProperty(payment => payment.CompleteAfter,
            DateTimeOffset.UtcNow.AddSeconds(-1)));
        await retry.CompleteDuePayments(default);
        Assert.Equal(PaymentStatus.TimedOut, (await retry.Handle(request, default)).Status);
        Assert.Equal("TimedOut", (await fresh.PaymentResultOutboxMessages.SingleAsync()).Outcome);
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
        Assert.Equal(1, await verify.PaymentResultOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Request_cancellation_leaves_durable_work_for_recovery()
    {
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        await using var db = CreateDb();
        using var cancelled = new CancellationTokenSource();
        var task = Handler(db, delay: 20000, timeout: 30000).Handle(request, cancelled.Token);
        await using var verify = CreateDb();
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (!await verify.Payments.AnyAsync(deadline.Token))
            {
                if (task.IsFaulted) await task;
                await Task.Delay(10, deadline.Token);
            }
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        }
        finally
        {
            // Always stop the request before disposing its context, even when a wait fails.
            cancelled.Cancel();
            try { await task; }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested) { }
        }
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
        var outbox = Assert.Single(await db.PaymentResultOutboxMessages.AsNoTracking().ToListAsync());
        Assert.Equal("Succeeded", outbox.Outcome);
        Assert.Equal(payment.PaidAt, outbox.PaidAtUtc);
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

    [Fact]
    public async Task Completion_rolls_back_when_the_outbox_insert_fails()
    {
        await using var db = CreateDb();
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), BookingId = Guid.NewGuid(), Amount = 100m, Currency = "SEK",
            Status = PaymentStatus.Pending, PlannedOutcome = PaymentStatus.Succeeded,
            CreatedAt = DateTimeOffset.UtcNow, CompleteAfter = DateTimeOffset.UtcNow.AddSeconds(-1)
        });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"PaymentResultOutboxMessages\" ADD CONSTRAINT reject_results CHECK (FALSE)");

        await Assert.ThrowsAsync<PostgresException>(() => Handler(db).CompleteDuePayments(default));

        var payment = await db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(payment.CompletedAt);
        Assert.Null(payment.PaidAt);
        Assert.Empty(await db.PaymentResultOutboxMessages.ToListAsync());
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"PaymentResultOutboxMessages\" DROP CONSTRAINT reject_results");
        await Handler(db).CompleteDuePayments(default);
        Assert.Equal(1, await db.PaymentResultOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Cancellation_fences_publish_one_terminal_failure_and_preserve_its_identity()
    {
        await using var db = CreateDb();
        var request = new ProcessPaymentRequest(Guid.NewGuid(), 100m, "SEK");
        await Handler(db).Cancel(request, default);
        var original = await db.PaymentResultOutboxMessages.AsNoTracking().SingleAsync();
        await Handler(db).Cancel(request, default);
        await Handler(db).Handle(request, default);
        var repeated = Assert.Single(await db.PaymentResultOutboxMessages.AsNoTracking().ToListAsync());
        Assert.Equal("TimedOut", repeated.Outcome);
        Assert.Equal(original.MessageId, repeated.MessageId);
        Assert.Equal(original.OccurredAtUtc, repeated.OccurredAtUtc);
        Assert.Null(repeated.PaidAtUtc);
    }

    [Fact]
    public async Task Outbox_retries_the_original_message_after_publish_failure()
    {
        await using var db = CreateDb();
        await Handler(db).Handle(new(Guid.NewGuid(), 100m, "SEK"), default);
        var publisher = new RecordingResultPublisher { Fail = true };
        await new PaymentResultOutboxDispatcher(db, publisher, TimeProvider.System,
            NullLogger<PaymentResultOutboxDispatcher>.Instance).DispatchPendingAsync();
        var failed = await db.PaymentResultOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Null(failed.DeliveredAtUtc);
        Assert.Equal(1, failed.AttemptCount);
        Assert.NotNull(failed.LastError);
        db.ChangeTracker.Clear();
        await db.PaymentResultOutboxMessages.ExecuteUpdateAsync(update => update.SetProperty(row => row.NextAttemptAtUtc,
            DateTimeOffset.UtcNow.AddSeconds(-1)));
        publisher.Fail = false;
        await new PaymentResultOutboxDispatcher(db, publisher, TimeProvider.System,
            NullLogger<PaymentResultOutboxDispatcher>.Instance).DispatchPendingAsync();
        var delivered = await db.PaymentResultOutboxMessages.AsNoTracking().SingleAsync();
        Assert.NotNull(delivered.DeliveredAtUtc);
        Assert.Null(delivered.LastError);
        Assert.Equal(2, publisher.Messages.Count);
        Assert.Equal(publisher.Messages[0], publisher.Messages[1]);
    }

    private sealed class RecordingResultPublisher : IPaymentResultPublisher
    {
        public bool Fail { get; set; }
        public List<object> Messages { get; } = [];
        public Task PublishAsync(PaymentSucceededV1 message, CancellationToken cancellationToken = default) => Record(message);
        public Task PublishAsync(PaymentFailedV1 message, CancellationToken cancellationToken = default) => Record(message);
        private Task Record(object message)
        {
            Messages.Add(message);
            return Fail ? Task.FromException(new IOException("Broker unavailable.")) : Task.CompletedTask;
        }
    }
}
