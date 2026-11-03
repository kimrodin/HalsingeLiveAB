using Booking.Features.Payments;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging;
using RabbitMQ.Client;
using BookingConnection = Booking.Features.Tickets.RabbitMqConnectionProvider;
using PaymentConnection = PaymentSimulator.Messaging.RabbitMqConnectionProvider;

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

public sealed class PaymentResultMessagingTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    [PaymentResultMessagingFact]
    public async Task Outbox_to_RabbitMq_to_inbox_survives_replay_and_applies_all_terminal_outcomes()
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
            await using var services = new ServiceCollection().AddSingleton(TimeProvider.System)
                .AddScoped(_ => fixture.CreateContext()).AddScoped<PaymentResultInbox>().BuildServiceProvider();
            using var consumer = new PaymentResultConsumer(bookingConnection, services.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<PaymentResultConsumer>.Instance);
            await consumer.StartAsync(token);
            try
            {
                var gateway = new DatabasePaymentGateway(CreatePaymentDb);
                foreach (var outcome in new[] { "Succeeded", "Failed", "TimedOut" })
                {
                    var inventory = await fixture.CreateInventoryAsync();
                    var bookingId = await fixture.ReserveAsync(inventory, gateway);
                    await using (var db = fixture.CreateContext())
                        await db.Bookings.Where(row => row.BookingId == bookingId)
                            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PaymentRequested, true), token);
                    PaymentResultOutboxMessage result;
                    await using (var db = CreatePaymentDb())
                    {
                        var settings = new SimulationSettingsService(db, new ConfigurationBuilder().AddInMemoryCollection(
                            new Dictionary<string, string?> { ["PaymentSimulation:Outcome"] = outcome, ["PaymentSimulation:DelayMs"] = "0" }).Build());
                        await new ProcessPaymentHandler(db, settings, new RefundPaymentHandler(db))
                            .Handle(new(bookingId, inventory.Price, "SEK"), token);
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
            }
            finally { await consumer.StopAsync(CancellationToken.None); }
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
        public Task<PaymentResult> CancelAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Payment already exists.");
    }
}
