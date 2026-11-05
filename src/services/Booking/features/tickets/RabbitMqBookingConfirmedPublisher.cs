using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using RabbitMQ.Client;

namespace Booking.Features.Tickets;

public sealed class RabbitMqBookingConfirmedPublisher(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqBookingConfirmedPublisher> logger) : IBookingConfirmedPublisher
{
    public async Task PublishAsync(BookingConfirmedV1 message, CancellationToken cancellationToken = default)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await TicketMessagingTopology.DeclareAsync(channel, cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.MessageId.ToString("D"),
            CorrelationId = message.CorrelationId.ToString("D"),
            Type = "booking.confirmed.v1"
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await channel.BasicPublishAsync(
            TicketMessagingTopology.Exchange,
            TicketMessagingTopology.RoutingKey,
            mandatory: true,
            properties,
            body,
            cancellationToken);

        logger.LogInformation(
            "Published booking confirmation {MessageId} for booking {BookingId}, item {BookingItemId}.",
            message.MessageId,
            message.BookingId,
            message.BookingItemId);
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
