using RabbitMQ.Client;

namespace Booking.Features.Payments.Results;

internal static class PaymentResultMessagingTopology
{
    public const string Exchange = "halsingelive.events";
    public const string Succeeded = "payment.succeeded.v1";
    public const string Failed = "payment.failed.v1";
    public const string Queue = "booking.payment-results.v1";
    public const string DeadLetterExchange = "halsingelive.dead";
    public const string DeadLetterQueue = "booking.payment-results.v1.dead";
    public const string DeadLetterRoutingKey = "payment-result.dead";

    public static async Task DeclareAsync(IChannel channel, CancellationToken token)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: token);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: token);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
            }, cancellationToken: token);
        await channel.QueueBindAsync(Queue, Exchange, Succeeded, cancellationToken: token);
        await channel.QueueBindAsync(Queue, Exchange, Failed, cancellationToken: token);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: token);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey, cancellationToken: token);
    }
}
