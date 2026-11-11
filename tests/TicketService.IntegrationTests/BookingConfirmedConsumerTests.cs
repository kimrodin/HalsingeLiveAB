using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using Messaging.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Infrastructure.Messaging;
using TicketService.Messaging.Issuance;

namespace TicketService.IntegrationTests;

public sealed class TicketMessagingTheoryAttribute : TheoryAttribute
{
    public TicketMessagingTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKET_TEST_POSTGRES"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TICKET_TEST_RABBITMQ")))
            Skip = "Set TICKET_TEST_POSTGRES and TICKET_TEST_RABBITMQ to run ticket messaging tests.";
    }
}

public sealed class BookingConfirmedConsumerTests(TicketDatabaseFixture database) : IClassFixture<TicketDatabaseFixture>
{
    private const string Exchange = "halsingelive.events";
    private const string Queue = "tickets.booking-confirmed.v1";
    private const string RoutingKey = "booking.confirmed.v1";

    [TicketMessagingTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Consume_RetainsTicketsThroughDatabaseOutageAndReplay(bool restartDuringOutage)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        var outage = new DatabaseOutage();
        var rabbit = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("TICKET_TEST_RABBITMQ")!) };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = rabbit.HostName, ["RabbitMq:Port"] = rabbit.Port.ToString(),
            ["RabbitMq:UserName"] = rabbit.UserName, ["RabbitMq:Password"] = rabbit.Password,
            ["RabbitMq:VirtualHost"] = rabbit.VirtualHost
        }).Build();
        await using var services = new ServiceCollection().AddSingleton(TimeProvider.System)
            .AddScoped(_ => database.CreateContext(outage)).AddScoped<IssueTicketsHandler>().BuildServiceProvider();
        await using var provider = new RabbitMqConnectionProvider(configuration);
        await using var connection = await rabbit.CreateConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
        await DeclareQueueAsync(channel, token);
        using var consumer = new BookingConfirmedConsumer(provider, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingConfirmedConsumer>.Instance);
        using var restarted = new BookingConfirmedConsumer(provider, services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingConfirmedConsumer>.Instance);
        await consumer.StartAsync(token);
        try
        {
            await WaitForConsumerAsync(channel, token);
            var message = new BookingConfirmed
            {
                MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), BookingId = Guid.NewGuid(),
                BookingItemId = Guid.NewGuid(), EventId = Guid.NewGuid(), EventName = "Recovered after database outage",
                EventStartsAtUtc = DateTimeOffset.Parse("2026-12-01T19:00:00.1234567Z"), VenueName = "Venue", SectionName = "Standing",
                Quantity = 2, UnitPrice = 100m, Currency = "SEK", OccurredAtUtc = DateTimeOffset.UtcNow
            };
            Task Publish(BookingConfirmed value) => channel.BasicPublishAsync(Exchange, RoutingKey, mandatory: true,
                new BasicProperties { Persistent = true, ContentType = "application/json" },
                JsonSerializer.SerializeToUtf8Bytes(value), token).AsTask();
            const string deadQueue = "tickets.booking-confirmed.v1.dead";
            await Publish(message);
            await outage.RepeatedFailures.WaitAsync(TimeSpan.FromSeconds(20), token);
            Assert.Null(await channel.BasicGetAsync(deadQueue, autoAck: true, token));
            await using (var db = database.CreateContext())
                Assert.False(await db.Tickets.AnyAsync(ticket => ticket.BookingId == message.BookingId, token));

            if (restartDuringOutage)
            {
                await consumer.StopAsync(token).WaitAsync(TimeSpan.FromSeconds(5), token);
                Assert.Equal(1u, (await channel.QueueDeclarePassiveAsync(Queue, token)).MessageCount);
            }
            outage.Recover();
            if (restartDuringOutage)
                await restarted.StartAsync(token);
            // Recovery must issue the original delivery without another publication.
            while (true)
            {
                await using var db = database.CreateContext();
                if (await db.Tickets.AnyAsync(ticket => ticket.BookingItemId == message.BookingItemId, token)) break;
                await Task.Delay(25, token);
            }
            await Publish(message);
            var conflict = message with { UnitPrice = message.UnitPrice + 1 };
            await Publish(conflict);
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
            await using var verify = database.CreateContext();
            var tickets = await verify.Tickets.Where(ticket => ticket.BookingItemId == message.BookingItemId).ToArrayAsync(token);
            Assert.Equal(message.Quantity, tickets.Length);
            Assert.All(tickets, ticket => Assert.Equal(message.UnitPrice, ticket.UnitPrice));
            Assert.Null(await channel.BasicGetAsync(deadQueue, autoAck: true, token));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
            await restarted.StopAsync(CancellationToken.None);
        }
    }

    [TicketMessagingTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Consume_ResubscribesAndIssuesTicketsAfterInterruption(bool recreateQueue)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var token = timeout.Token;
        var rabbit = new ConnectionFactory { Uri = new Uri(Environment.GetEnvironmentVariable("TICKET_TEST_RABBITMQ")!) };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RabbitMq:HostName"] = rabbit.HostName, ["RabbitMq:Port"] = rabbit.Port.ToString(),
            ["RabbitMq:UserName"] = rabbit.UserName, ["RabbitMq:Password"] = rabbit.Password,
            ["RabbitMq:VirtualHost"] = rabbit.VirtualHost
        }).Build();
        await using var services = new ServiceCollection().AddSingleton(configuration).AddSingleton(TimeProvider.System)
            .AddScoped(_ => database.CreateContext()).AddScoped<IssueTicketsHandler>()
            .AddSingleton<RabbitMqConnectionProvider>().BuildServiceProvider();
        var connectionProvider = services.GetRequiredService<RabbitMqConnectionProvider>();
        // Keep the test publisher independent of the connection interrupted below.
        await using var connection = await rabbit.CreateConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
        await DeclareQueueAsync(channel, token);
        using var consumer = new BookingConfirmedConsumer(connectionProvider,
            services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BookingConfirmedConsumer>.Instance);
        await consumer.StartAsync(token);
        try
        {
            await WaitForConsumerAsync(channel, token);
            if (recreateQueue)
            {
                await channel.QueueDeleteAsync(Queue, ifUnused: false, ifEmpty: false, cancellationToken: token);
                await DeclareQueueAsync(channel, token);
            }
            else
            {
                var consumerConnection = await connectionProvider.GetConnectionAsync(token);
                await consumerConnection.CloseAsync(token);
            }

            var message = new BookingConfirmed
            {
                MessageId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), BookingId = Guid.NewGuid(),
                BookingItemId = Guid.NewGuid(), EventId = Guid.NewGuid(), EventName = "Recovered ticket issuance",
                EventStartsAtUtc = DateTimeOffset.Parse("2026-12-01T19:00:00Z"), VenueName = "Test venue", SectionName = "Standing",
                Quantity = 1, UnitPrice = 100m, Currency = "SEK", OccurredAtUtc = DateTimeOffset.UtcNow
            };
            // A confirmation published after the interruption must still be issued.
            await channel.BasicPublishAsync(Exchange, RoutingKey, mandatory: true,
                new BasicProperties { Persistent = true, ContentType = "application/json", MessageId = message.MessageId.ToString("D") },
                JsonSerializer.SerializeToUtf8Bytes(message), token);
            await WaitForConsumerAsync(channel, token);
            while (true)
            {
                await using var db = database.CreateContext();
                var tickets = await db.Tickets.AsNoTracking().Where(ticket => ticket.BookingId == message.BookingId).ToArrayAsync(token);
                if (tickets.Length > 0)
                {
                    var ticket = Assert.Single(tickets);
                    Assert.Equal(message.BookingItemId, ticket.BookingItemId);
                    Assert.Equal(message.EventId, ticket.EventId);
                    break;
                }
                await Task.Delay(25, token);
            }
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }

    private static async Task DeclareQueueAsync(IChannel channel, CancellationToken token)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: token);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "halsingelive.dead", ["x-dead-letter-routing-key"] = "ticket-issue.dead"
            }, cancellationToken: token);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: token);
    }

    private static async Task WaitForConsumerAsync(IChannel channel, CancellationToken token)
    {
        while ((await channel.QueueDeclarePassiveAsync(Queue, token)).ConsumerCount != 1)
            await Task.Delay(25, token);
    }
}
