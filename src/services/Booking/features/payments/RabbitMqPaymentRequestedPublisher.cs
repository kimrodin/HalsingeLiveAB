using System.Text.Json;
using Booking.Features.Tickets;
using HalsingeLive.Contracts.Messaging;
using RabbitMQ.Client;

namespace Booking.Features.Payments;

public sealed class RabbitMqPaymentRequestedPublisher(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqPaymentRequestedPublisher> logger) : IPaymentRequestedPublisher
{
    public async Task PublishAsync(PaymentRequestedV1 message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await PaymentRequestMessagingTopology.DeclareAsync(channel, cancellationToken);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.MessageId.ToString("D"),
            CorrelationId = message.CorrelationId.ToString("D"),
            Type = PaymentRequestMessagingTopology.RoutingKey
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        await channel.BasicPublishAsync(
            PaymentRequestMessagingTopology.Exchange,
            PaymentRequestMessagingTopology.RoutingKey,
            mandatory: true,
            properties,
            body,
            cancellationToken);

        logger.LogInformation(
            "Published payment request {MessageId} for booking {BookingId}.",
            message.MessageId,
            message.BookingId);
    }
}

internal static class PaymentRequestMessagingTopology
{
    public const string Exchange = "halsingelive.events";
    public const string RoutingKey = "payment.requested.v1";
    public const string Queue = "payments.payment-requested.v1";
    public const string DeadLetterExchange = "halsingelive.dead";
    public const string DeadLetterQueue = "payments.payment-requested.v1.dead";
    public const string DeadLetterRoutingKey = "payment-request.dead";

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
