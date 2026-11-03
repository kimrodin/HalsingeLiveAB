using System.Text;
using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.RefundPayment;
using PaymentSimulator.Features.Payments.Simulation;
using PaymentSimulator.Messaging;
using RabbitMQ.Client;

namespace PaymentSimulator.IntegrationTests;

public sealed class PaymentMessagingFactAttribute : FactAttribute
{
    public PaymentMessagingFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PAYMENT_TEST_RABBITMQ")))
            Skip = "Set PAYMENT_TEST_RABBITMQ to a dedicated test broker and PAYMENT_TEST_DB to PostgreSQL.";
    }
}

public sealed class PaymentRequestedConsumerTests : IAsyncLifetime
{
    private const string Exchange = "halsingelive.events";
    private const string RoutingKey = "payment.requested.v1";
    private const string Queue = "payments.payment-requested.v1";
    private const string DeadQueue = "payments.payment-requested.v1.dead";
    private readonly string databaseName = "payment_messaging_test_" + Guid.NewGuid().ToString("N");
    private string? connectionString;

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PAYMENT_TEST_DB")
            ?? "Host=localhost;Port=5434;Database=postgres;Username=postgres;Password=postgres") { Database = "postgres" };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin);
        await create.ExecuteNonQueryAsync();
        builder.Database = databaseName;
        connectionString = builder.ConnectionString;
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (connectionString is null) return;
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres", Pooling = false };
        await using var admin = new NpgsqlConnection(builder.ConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    [PaymentMessagingFact]
    public async Task Consume_ProcessesTerminalOutcomesIdempotentlyAndDeadLettersInvalidRequests()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        await using var services = CreateServices();
        var connection = await services.GetRequiredService<RabbitMqConnectionProvider>().GetConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
        using var consumer = CreateConsumer(services);
        await consumer.StartAsync(token);
        try
        {
            await WaitForConsumerAsync(channel, token);
            foreach (var outcome in new[] { SimulationOutcome.Succeeded, SimulationOutcome.Failed, SimulationOutcome.TimedOut })
            {
                await using var scope = services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SimulationSettingsService>()
                    .Update(new(outcome, 0, 5000), token);
                var message = CreateMessage();
                await PublishAsync(channel, JsonSerializer.SerializeToUtf8Bytes(message), token);
                await PublishAsync(channel, JsonSerializer.SerializeToUtf8Bytes(message), token);
                await PublishAsync(channel, JsonSerializer.SerializeToUtf8Bytes(message with { MessageId = Guid.NewGuid() }), token);
                var conflicting = JsonSerializer.SerializeToUtf8Bytes(message with { MessageId = Guid.NewGuid(), Amount = 999m });
                await PublishAsync(channel, conflicting, token);

                // Prefetch is one: the conflict's dead letter proves earlier duplicates finished.
                Assert.Equal(conflicting, await ReadDeadLetterAsync(channel, token));
                await using var db = CreateDb();
                var payment = Assert.Single(await db.Payments.Where(p => p.BookingId == message.BookingId).ToListAsync(token));
                Assert.Equal(outcome.ToString(), payment.Status.ToString());
                Assert.Equal(message.Amount, payment.Amount);
                Assert.Equal(message.Currency, payment.Currency);
                Assert.NotNull(payment.CompletedAt);
                Assert.Equal(outcome == SimulationOutcome.Succeeded, payment.PaidAt is not null);
            }

            var invalid = CreateMessage();
            var missingCurrency = JsonSerializer.SerializeToNode(invalid)!.AsObject();
            missingCurrency.Remove("Currency");
            foreach (var payload in new[]
            {
                Encoding.UTF8.GetBytes("{"), Encoding.UTF8.GetBytes("null"),
                Encoding.UTF8.GetBytes(missingCurrency.ToJsonString()),
                JsonSerializer.SerializeToUtf8Bytes(invalid with { MessageId = Guid.Empty }),
                JsonSerializer.SerializeToUtf8Bytes(invalid with { CorrelationId = Guid.Empty }),
                JsonSerializer.SerializeToUtf8Bytes(invalid with { OccurredAtUtc = default }),
                JsonSerializer.SerializeToUtf8Bytes(invalid with { Amount = 0m })
            })
            {
                await PublishAsync(channel, payload, token);
                Assert.Equal(payload, await ReadDeadLetterAsync(channel, token));
            }
            await using var verification = CreateDb();
            Assert.Equal(3, await verification.Payments.CountAsync(token));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
        Assert.Equal(0u, (await channel.QueueDeclarePassiveAsync(Queue, token)).MessageCount);
    }

    [PaymentMessagingFact]
    public async Task Consume_ShutdownRequeuesDurablePendingPaymentAndRestartReusesItsOutcome()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        await using var services = CreateServices();
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SimulationSettingsService>()
                .Update(new(SimulationOutcome.Succeeded, 20000, 30000), token);
        var connection = await services.GetRequiredService<RabbitMqConnectionProvider>().GetConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
        var message = CreateMessage();
        using (var consumer = CreateConsumer(services))
        {
            await consumer.StartAsync(token);
            try
            {
                await WaitForConsumerAsync(channel, token);
                await PublishAsync(channel, JsonSerializer.SerializeToUtf8Bytes(message), token);
                await WaitForPaymentAsync(message.BookingId, PaymentStatus.Pending, token);
            }
            finally
            {
                await consumer.StopAsync(CancellationToken.None);
            }
        }
        Assert.Equal(1u, (await channel.QueueDeclarePassiveAsync(Queue, token)).MessageCount);
        await using (var db = CreateDb())
            await db.Payments.ExecuteUpdateAsync(update => update.SetProperty(p => p.CompleteAfter,
                DateTimeOffset.UtcNow.AddSeconds(-1)), token);
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SimulationSettingsService>()
                .Update(new(SimulationOutcome.Failed, 0, 5000), token);

        using var restarted = CreateConsumer(services);
        await restarted.StartAsync(token);
        try
        {
            await WaitForPaymentAsync(message.BookingId, PaymentStatus.Succeeded, token);
            var conflicting = JsonSerializer.SerializeToUtf8Bytes(message with { Amount = 999m });
            await PublishAsync(channel, conflicting, token);
            Assert.Equal(conflicting, await ReadDeadLetterAsync(channel, token));
        }
        finally
        {
            await restarted.StopAsync(CancellationToken.None);
        }
        await using var verification = CreateDb();
        Assert.Single(await verification.Payments.ToListAsync(token));
        Assert.Equal(0u, (await channel.QueueDeclarePassiveAsync(Queue, token)).MessageCount);
    }

    private PaymentDbContext CreateDb() => new(new DbContextOptionsBuilder<PaymentDbContext>().UseNpgsql(connectionString).Options);

    private ServiceProvider CreateServices()
    {
        var rabbit = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("PAYMENT_TEST_RABBITMQ")!) };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = rabbit.HostName, ["RabbitMq:Port"] = rabbit.Port.ToString(),
            ["RabbitMq:UserName"] = rabbit.UserName, ["RabbitMq:Password"] = rabbit.Password,
            ["RabbitMq:VirtualHost"] = rabbit.VirtualHost
        }).Build();
        return new ServiceCollection().AddSingleton(configuration)
            .AddDbContext<PaymentDbContext>(options => options.UseNpgsql(connectionString))
            .AddScoped<SimulationSettingsService>().AddScoped<RefundPaymentHandler>()
            .AddScoped<ProcessPaymentHandler>().AddScoped<PaymentRequestedHandler>()
            .AddSingleton<RabbitMqConnectionProvider>().BuildServiceProvider();
    }

    private static PaymentRequestedConsumer CreateConsumer(ServiceProvider services) => new(
        services.GetRequiredService<RabbitMqConnectionProvider>(), services.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<PaymentRequestedConsumer>.Instance);

    private static PaymentRequestedV1 CreateMessage() => new()
    {
        MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), BookingId = Guid.NewGuid(),
        Amount = 125.50m, Currency = "SEK", OccurredAtUtc = DateTimeOffset.UtcNow
    };

    private static Task PublishAsync(IChannel channel, byte[] payload, CancellationToken token) =>
        channel.BasicPublishAsync(Exchange, RoutingKey, mandatory: true,
            new BasicProperties { Persistent = true, ContentType = "application/json", Type = RoutingKey }, payload, token).AsTask();

    private static async Task WaitForConsumerAsync(IChannel channel, CancellationToken token)
    {
        // Passive declarations of missing queues close a channel, so predeclare the publisher's topology.
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: token);
        await channel.ExchangeDeclareAsync("halsingelive.dead", ExchangeType.Direct, durable: true, cancellationToken: token);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "halsingelive.dead", ["x-dead-letter-routing-key"] = "payment-request.dead"
            }, cancellationToken: token);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: token);
        await channel.QueueDeclareAsync(DeadQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: token);
        await channel.QueueBindAsync(DeadQueue, "halsingelive.dead", "payment-request.dead", cancellationToken: token);
        while ((await channel.QueueDeclarePassiveAsync(Queue, token)).ConsumerCount == 0)
            await Task.Delay(25, token);
    }

    private static async Task<byte[]> ReadDeadLetterAsync(IChannel channel, CancellationToken token)
    {
        while (true)
        {
            var delivery = await channel.BasicGetAsync(DeadQueue, autoAck: true, token);
            if (delivery is not null) return delivery.Body.ToArray();
            await Task.Delay(25, token);
        }
    }

    private async Task WaitForPaymentAsync(Guid bookingId, PaymentStatus status, CancellationToken token)
    {
        await using var db = CreateDb();
        while (!await db.Payments.AsNoTracking().AnyAsync(p => p.BookingId == bookingId && p.Status == status, token))
            await Task.Delay(25, token);
    }
}
