using PaymentSimulator.Infrastructure.Messaging;
using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using RabbitMQ.Client;

namespace PaymentSimulator.Messaging.Results;

public sealed class RabbitMqPaymentResultPublisher(
    RabbitMqConnectionProvider connectionProvider,
    ILogger<RabbitMqPaymentResultPublisher> logger) : IPaymentResultPublisher
{
    public Task PublishAsync(PaymentSucceededV1 message, CancellationToken cancellationToken = default) =>
        PublishAsync(message.MessageId, message.CorrelationId, PaymentResultMessagingTopology.Succeeded,
            JsonSerializer.SerializeToUtf8Bytes(message), cancellationToken);

    public Task PublishAsync(PaymentFailedV1 message, CancellationToken cancellationToken = default) =>
        PublishAsync(message.MessageId, message.CorrelationId, PaymentResultMessagingTopology.Failed,
            JsonSerializer.SerializeToUtf8Bytes(message), cancellationToken);

    private async Task PublishAsync(Guid messageId, Guid correlationId, string routingKey, byte[] body, CancellationToken token)
    {
        var connection = await connectionProvider.GetConnectionAsync(token);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
        await PaymentResultMessagingTopology.DeclareAsync(channel, token);
        var properties = new BasicProperties
        {
            Persistent = true, ContentType = "application/json", Type = routingKey,
            MessageId = messageId.ToString("D"), CorrelationId = correlationId.ToString("D")
        };
        await channel.BasicPublishAsync(PaymentResultMessagingTopology.Exchange, routingKey,
            mandatory: true, properties, body, token);
        logger.LogInformation(
            "Published payment result {MessageId}, type {Type}, correlation {CorrelationId}.",
            messageId, routingKey, correlationId);
    }
}
