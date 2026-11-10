using System.Text.Json;
using Booking.Features.Payments.Gateway;
using Booking.Features.Payments.Requests;
using Booking.Features.Payments.Results;
using HalsingeLive.Contracts.Messaging;
using Messaging.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging.Requests;
using PaymentSimulator.Messaging.Results;
using RabbitMQ.Client;
using BookingConnection = Booking.Infrastructure.Messaging.RabbitMqConnectionProvider;
using PaymentConnection = PaymentSimulator.Infrastructure.Messaging.RabbitMqConnectionProvider;

namespace Booking.IntegrationTests;

public sealed class PaymentResultMessagingFactAttribute : FactAttribute
{
    public PaymentResultMessagingFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKING_TEST_POSTGRES"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BOOKING_TEST_REDIS"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYMENT_TEST_RABBITMQ")))
            Skip = "Set BOOKING_TEST_POSTGRES, BOOKING_TEST_REDIS, PAYMENT_TEST_DB and PAYMENT_TEST_RABBITMQ for messaging tests.";
    }
}

public sealed class PaymentResultMessagingTheoryAttribute : TheoryAttribute
{
    public PaymentResultMessagingTheoryAttribute()
    {
        Skip = new PaymentResultMessagingFactAttribute().Skip;
    }
}

public sealed class PaymentResultMessagingTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    [PaymentResultMessagingTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consume_RetainsResultThroughDatabaseOutageAndReplay(bool restartDuringOutage)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        var bookingId = Guid.NewGuid();
        await using (var db = fixture.CreateContext())
        {
            db.Bookings.Add(new Booking.Features.Bookings.Models.Booking
            {
                BookingId = bookingId, CustomerName = "Recovery test", CustomerEmail = "recovery@example.test",
                Status = "Reserved", CreatedAt = DateTime.UtcNow, TotalPrice = 125m, PaymentRequested = true
            });
            await db.SaveChangesAsync(token);
        }
        var outage = new DatabaseOutage();
        var rabbit = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("PAYMENT_TEST_RABBITMQ")!) };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = rabbit.HostName, ["RabbitMq:Port"] = rabbit.Port.ToString(),
            ["RabbitMq:UserName"] = rabbit.UserName, ["RabbitMq:Password"] = rabbit.Password,
            ["RabbitMq:VirtualHost"] = rabbit.VirtualHost
        }).Build();
        await using var services = new ServiceCollection().AddSingleton(TimeProvider.System)
            .AddScoped(_ => fixture.CreateContext(outage)).AddScoped<PaymentResultInbox>().BuildServiceProvider();
        await using var bookingConnection = new BookingConnection(configuration);
        await using var paymentConnection = new PaymentConnection(configuration);
        var publisher = new RabbitMqPaymentResultPublisher(paymentConnection, NullLogger<RabbitMqPaymentResultPublisher>.Instance);
        var connection = await paymentConnection.GetConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: token);
        using var consumer = new PaymentResultConsumer(bookingConnection, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PaymentResultConsumer>.Instance);
        using var restarted = new PaymentResultConsumer(bookingConnection, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PaymentResultConsumer>.Instance);
        await consumer.StartAsync(token);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var message = new PaymentSucceededV1
            {
                MessageId = Guid.NewGuid(), CorrelationId = bookingId, BookingId = bookingId, PaymentId = Guid.NewGuid(),
                Amount = 125m, Currency = "SEK", PaidAtUtc = now, OccurredAtUtc = now
            };
            await publisher.PublishAsync(message, token);
            await outage.RepeatedFailures.WaitAsync(TimeSpan.FromSeconds(20), token);
            const string queue = "booking.payment-results.v1";
            const string deadQueue = "booking.payment-results.v1.dead";
            Assert.Null(await channel.BasicGetAsync(deadQueue, autoAck: true, token));
            await using (var db = fixture.CreateContext())
                Assert.False(await db.PaymentResultInboxMessages.AnyAsync(row => row.MessageId == message.MessageId, token));

            if (restartDuringOutage)
            {
                await consumer.StopAsync(token).WaitAsync(TimeSpan.FromSeconds(5), token);
                Assert.Equal(1u, (await channel.QueueDeclarePassiveAsync(queue, token)).MessageCount);
            }
            outage.Recover();
            if (restartDuringOutage)
                await restarted.StartAsync(token);
            // Recovery must save the original delivery without another publication.
            while (true)
            {
                await using var db = fixture.CreateContext();
                if (await db.PaymentResultInboxMessages.AnyAsync(row => row.MessageId == message.MessageId, token)) break;
                await Task.Delay(25, token);
            }
            await publisher.PublishAsync(message, token);
            var conflict = message with { Amount = message.Amount + 1 };
            await publisher.PublishAsync(conflict, token);
            while (true)
            {
                var dead = await channel.BasicGetAsync(deadQueue, autoAck: true, token);
                if (dead is not null)
                {
                    Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(conflict), dead.Body.ToArray());
                    break;
                }
                await Task.Delay(25, token);
            }
            await using var verify = fixture.CreateContext();
            var saved = Assert.Single(await verify.PaymentResultInboxMessages.Where(row => row.BookingId == bookingId).ToArrayAsync(token));
            Assert.Equal(message.MessageId, saved.MessageId);
            Assert.Null(saved.ProcessedAtUtc);
            Assert.Null(await channel.BasicGetAsync(deadQueue, autoAck: true, token));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
            await restarted.StopAsync(CancellationToken.None);
        }
    }

    [PaymentResultMessagingFact]
    public async Task Request_outbox_to_payment_consumer_and_result_inbox_survives_replay_and_applies_all_outcomes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = timeout.Token;
        var databaseName = "payment_results_test_" + Guid.NewGuid().ToString("N");
        var database = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PAYMENT_TEST_DB")
            ?? "Host=localhost;Port=5434;Database=postgres;Username=postgres;Password=postgres") { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(database.ConnectionString);
        await admin.OpenAsync(token);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync(token);
        database.Database = databaseName;
        database.Pooling = true;
        database.MaxPoolSize = 10;
        PaymentDbContext CreatePaymentDb() => new(new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(database.ConnectionString).Options);
        try
        {
            await using (var db = CreatePaymentDb()) await db.Database.MigrateAsync(token);
            var rabbit = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("PAYMENT_TEST_RABBITMQ")!) };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMq:HostName"] = rabbit.HostName, ["RabbitMq:Port"] = rabbit.Port.ToString(),
                ["RabbitMq:UserName"] = rabbit.UserName, ["RabbitMq:Password"] = rabbit.Password,
                ["RabbitMq:VirtualHost"] = rabbit.VirtualHost
            }).Build();
            await using var paymentConnection = new PaymentConnection(configuration);
            await using var bookingConnection = new BookingConnection(configuration);
            var publisher = new RabbitMqPaymentResultPublisher(paymentConnection, NullLogger<RabbitMqPaymentResultPublisher>.Instance);
            var requestPublisher = new RabbitMqPaymentRequestedPublisher(bookingConnection, NullLogger<RabbitMqPaymentRequestedPublisher>.Instance);
            var simulation = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PaymentSimulation:Outcome"] = "Succeeded", ["PaymentSimulation:DelayMs"] = "0"
            }).Build();
            await using var services = new ServiceCollection().AddSingleton(TimeProvider.System)
                .AddScoped(_ => fixture.CreateContext()).AddScoped<PaymentResultInbox>()
                .AddScoped(_ => CreatePaymentDb()).AddScoped(db => new SimulationSettingsService(db.GetRequiredService<PaymentDbContext>(), simulation))
                .AddScoped<RefundPaymentHandler>().AddScoped<ProcessPaymentHandler>().AddScoped<PaymentRequestedHandler>()
                .BuildServiceProvider();
            using var consumer = new PaymentResultConsumer(bookingConnection, services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<PaymentResultConsumer>.Instance);
            using var requestConsumer = new PaymentRequestedConsumer(paymentConnection, services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<PaymentRequestedConsumer>.Instance);
            using var recovery = new PaymentRecoveryWorker(services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<PaymentRecoveryWorker>.Instance);
            await recovery.StartAsync(token);
            await consumer.StartAsync(token);
            await requestConsumer.StartAsync(token);
            try
            {
                var gateway = new DatabasePaymentGateway(CreatePaymentDb);
                foreach (var outcome in new[] { "Succeeded", "Failed", "TimedOut" })
                {
                    simulation["PaymentSimulation:Outcome"] = outcome;
                    var inventory = await fixture.CreateInventoryAsync();
                    var bookingId = await fixture.ReserveAsync(inventory, gateway);
                    await using (var db = fixture.CreateContext())
                    {
                        var started = await fixture.CreateService(db, gateway).StartPaymentAsync(bookingId, token);
                        Assert.Equal("Pending", started.PaymentStatus);
                        await new PaymentRequestOutboxDispatcher(db, requestPublisher, TimeProvider.System,
                            NullLogger<PaymentRequestOutboxDispatcher>.Instance).DispatchPendingAsync(token);
                        var request = await db.PaymentRequestOutboxMessages.SingleAsync(row => row.BookingId == bookingId, token);
                        Assert.NotNull(request.DeliveredAtUtc);
                        // Repeat delivery after losing the source delivery marker.
                        request.DeliveredAtUtc = null;
                        await db.SaveChangesAsync(token);
                        await new PaymentRequestOutboxDispatcher(db, requestPublisher, TimeProvider.System,
                            NullLogger<PaymentRequestOutboxDispatcher>.Instance).DispatchPendingAsync(token);
                    }
                    while (true)
                    {
                        await using var db = CreatePaymentDb();
                        if (await db.PaymentResultOutboxMessages.AnyAsync(row => row.BookingId == bookingId, token)) break;
                        await Task.Delay(25, token);
                    }
                    PaymentResultOutboxMessage result;
                    await using (var db = CreatePaymentDb())
                    {
                        Assert.Equal(1, await db.Payments.CountAsync(row => row.BookingId == bookingId, token));
                        await new PaymentResultOutboxDispatcher(db, publisher, TimeProvider.System,
                            NullLogger<PaymentResultOutboxDispatcher>.Instance).DispatchPendingAsync(token);
                        result = await db.PaymentResultOutboxMessages.AsNoTracking().SingleAsync(row => row.BookingId == bookingId, token);
                        Assert.NotNull(result.DeliveredAtUtc);
                    }
                    while (true)
                    {
                        await using var db = fixture.CreateContext();
                        if (await db.PaymentResultInboxMessages.AnyAsync(row => row.MessageId == result.MessageId, token)) break;
                        await Task.Delay(25, token);
                    }
                    // Replay the original source event after losing its delivery marker.
                    await using (var db = CreatePaymentDb())
                    {
                        await db.PaymentResultOutboxMessages.Where(row => row.MessageId == result.MessageId)
                            .ExecuteUpdateAsync(update => update.SetProperty(row => row.DeliveredAtUtc, (DateTimeOffset?)null), token);
                        await new PaymentResultOutboxDispatcher(db, publisher, TimeProvider.System,
                            NullLogger<PaymentResultOutboxDispatcher>.Instance).DispatchPendingAsync(token);
                    }
                    // An invalid following message acts as a barrier for the earlier duplicate.
                    var invalid = new PaymentFailedV1
                    {
                        MessageId = Guid.NewGuid(), CorrelationId = bookingId, PaymentId = result.PaymentId,
                        BookingId = bookingId, Amount = inventory.Price + 1, Currency = "SEK", FailureCode = "Failed",
                        OccurredAtUtc = result.OccurredAtUtc
                    };
                    await publisher.PublishAsync(invalid, token);
                    var connection = await paymentConnection.GetConnectionAsync(token);
                    await using (var channel = await connection.CreateChannelAsync(cancellationToken: token))
                    {
                        while (true)
                        {
                            var dead = await channel.BasicGetAsync("booking.payment-results.v1.dead", true, token);
                            if (dead is not null)
                            {
                                Assert.Equal(invalid.MessageId.ToString("D"), dead.BasicProperties.MessageId);
                                break;
                            }
                            await Task.Delay(25, token);
                        }
                    }
                    await using (var db = fixture.CreateContext())
                    {
                        Assert.Single(await db.PaymentResultInboxMessages.Where(row => row.BookingId == bookingId).ToListAsync(token));
                        Assert.Equal("Reserved", (await db.Bookings.SingleAsync(row => row.BookingId == bookingId, token)).Status);
                        await new PaymentResultInboxDispatcher(db, fixture.CreateService(db, gateway), TimeProvider.System,
                            NullLogger<PaymentResultInboxDispatcher>.Instance).DispatchPendingAsync(token);
                    }
                    await using var verify = fixture.CreateContext();
                    Assert.NotNull((await verify.PaymentResultInboxMessages.SingleAsync(row => row.MessageId == result.MessageId, token)).ProcessedAtUtc);
                    Assert.Equal(outcome == "Succeeded" ? "Confirmed" : "Cancelled",
                        (await verify.Bookings.SingleAsync(row => row.BookingId == bookingId, token)).Status);
                    Assert.Equal(outcome == "Succeeded" ? 1 : 0,
                        await verify.TicketIssueOutboxMessages.CountAsync(row => row.BookingId == bookingId, token));
                }

                // Hold a real request in RabbitMQ until the reservation has expired and
                // HTTP reconciliation has established a cancellation fence.
                await requestConsumer.StopAsync(token);
                var lateInventory = await fixture.CreateInventoryAsync();
                var lateBookingId = await fixture.ReserveAsync(lateInventory, gateway);
                PaymentRequestOutboxMessage lateRequest;
                await using (var db = fixture.CreateContext())
                {
                    await fixture.CreateService(db, gateway).StartPaymentAsync(lateBookingId, token);
                    await new PaymentRequestOutboxDispatcher(db, requestPublisher, TimeProvider.System,
                        NullLogger<PaymentRequestOutboxDispatcher>.Instance).DispatchPendingAsync(token);
                    lateRequest = await db.PaymentRequestOutboxMessages.SingleAsync(row => row.BookingId == lateBookingId, token);
                    Assert.NotNull(lateRequest.DeliveredAtUtc);
                    await db.Bookings.Where(row => row.BookingId == lateBookingId).ExecuteUpdateAsync(update =>
                        update.SetProperty(row => row.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)), token);
                    Assert.Equal("Refunded", (await fixture.CreateService(db, gateway).ReconcilePaymentAsync(lateBookingId, token)).PaymentStatus);
                }
                using var lateConsumer = new PaymentRequestedConsumer(paymentConnection, services.GetRequiredService<IServiceScopeFactory>(),
                    NullLogger<PaymentRequestedConsumer>.Instance);
                await lateConsumer.StartAsync(token);
                try
                {
                    // A conflicting following request reaching DLQ proves the original
                    // queued request was consumed after cancellation, not merely left queued.
                    var barrierId = Guid.NewGuid();
                    await requestPublisher.PublishAsync(new PaymentRequestedV1
                    {
                        MessageId = barrierId, CorrelationId = lateBookingId, BookingId = lateBookingId,
                        Amount = lateRequest.Amount + 1, Currency = "SEK", OccurredAtUtc = lateRequest.CreatedAtUtc
                    }, token);
                    var connection = await paymentConnection.GetConnectionAsync(token);
                    await using var channel = await connection.CreateChannelAsync(cancellationToken: token);
                    while (true)
                    {
                        var dead = await channel.BasicGetAsync("payments.payment-requested.v1.dead", true, token);
                        if (dead is not null)
                        {
                            Assert.Equal(barrierId.ToString("D"), dead.BasicProperties.MessageId);
                            break;
                        }
                        await Task.Delay(25, token);
                    }
                    await using var verify = CreatePaymentDb();
                    var fenced = await verify.Payments.SingleAsync(row => row.BookingId == lateBookingId, token);
                    Assert.Equal("TimedOut", fenced.Status.ToString());
                    Assert.Null(fenced.PaidAt);
                    Assert.NotNull(fenced.RefundedAt);
                    Assert.Equal(1, await verify.PaymentResultOutboxMessages.CountAsync(row => row.BookingId == lateBookingId, token));
                }
                finally { await lateConsumer.StopAsync(CancellationToken.None); }
            }
            finally
            {
                await requestConsumer.StopAsync(CancellationToken.None);
                await consumer.StopAsync(CancellationToken.None);
                await recovery.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private sealed class DatabasePaymentGateway(Func<PaymentDbContext> createDb) : IPaymentGateway
    {
        public async Task<PaymentResult?> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken)
        {
            await using var db = createDb();
            var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(row => row.BookingId == bookingId, cancellationToken);
            return payment is null ? null : new(payment.Id, payment.BookingId, payment.Amount, payment.Currency,
                payment.Status.ToString(), payment.CreatedAt, payment.CompletedAt, payment.PaidAt, payment.RefundedAt);
        }
        public async Task<PaymentResult> RefundAsync(Guid paymentId, CancellationToken cancellationToken)
        {
            await using var db = createDb();
            var response = (await new RefundPaymentHandler(db).Handle(paymentId, cancellationToken))!;
            return new(response.PaymentId, response.BookingId, response.Amount, response.Currency, response.Status.ToString(),
                response.CreatedAt, response.CompletedAt, response.PaidAt, response.RefundedAt);
        }
        public Task<PaymentResult> ProcessAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Result handling must not start another payment.");
        public async Task<PaymentResult> CancelAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken)
        {
            await using var db = createDb();
            var handler = new ProcessPaymentHandler(db, new SimulationSettingsService(db, new ConfigurationBuilder().Build()),
                new RefundPaymentHandler(db));
            var response = await handler.Cancel(new(bookingId, amount, currency), cancellationToken);
            return new(response.PaymentId, response.BookingId, response.Amount, response.Currency, response.Status.ToString(),
                response.CreatedAt, response.CompletedAt, response.PaidAt, response.RefundedAt);
        }
    }
}
