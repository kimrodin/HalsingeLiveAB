using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TicketService.Features.Tickets.IssueTickets;

public sealed class BookingConfirmedConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<BookingConfirmedConsumer> logger) : BackgroundService
{
    private const int MaximumProcessingAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Ticket issuance consumer stopped; reconnecting shortly.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await TicketMessagingTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) => await HandleDeliveryAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(TicketMessagingTopology.Queue, autoAck: false, consumer, stoppingToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        BookingConfirmedV1 message;
        try
        {
            message = JsonSerializer.Deserialize<BookingConfirmedV1>(delivery.Body.Span)
                ?? throw new InvalidDataException("The booking confirmation message was empty.");
            if (message.MessageId == Guid.Empty)
                throw new InvalidDataException("The booking confirmation message ID was missing.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger.LogError(exception, "Rejecting an invalid booking confirmation message.");
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        for (var attempt = 1; attempt <= MaximumProcessingAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IssueTicketsHandler>();
                var result = await handler.HandleAsync(message, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                logger.LogInformation(
                    "Processed booking confirmation {MessageId} for booking {BookingId}, item {BookingItemId}; already issued: {AlreadyIssued}.",
                    message.MessageId,
                    message.BookingId,
                    message.BookingItemId,
                    result.AlreadyIssued);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                if (attempt == MaximumProcessingAttempts)
                {
                    logger.LogError(exception,
                        "Ticket issuance failed after {Attempts} attempts for message {MessageId}; sending it to the dead-letter queue.",
                        attempt,
                        message.MessageId);
                    await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                    return;
                }

                logger.LogWarning(exception,
                    "Ticket issuance attempt {Attempt} failed for message {MessageId}; retrying.",
                    attempt,
                    message.MessageId);
                await Task.Delay(TimeSpan.FromSeconds(attempt), stoppingToken);
            }
        }
    }
}

internal static class TicketMessagingTopology
{
    public const string Exchange = "halsingelive.events";
    public const string RoutingKey = "booking.confirmed.v1";
    public const string Queue = "tickets.booking-confirmed.v1";
    public const string DeadLetterExchange = "halsingelive.dead";
    public const string DeadLetterQueue = "tickets.booking-confirmed.v1.dead";
    public const string DeadLetterRoutingKey = "ticket-issue.dead";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);

        var queueArguments = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
        };

        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: queueArguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey,
            cancellationToken: cancellationToken);
    }
}
