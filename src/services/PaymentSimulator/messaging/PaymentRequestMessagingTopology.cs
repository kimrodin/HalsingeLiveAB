using RabbitMQ.Client;

namespace PaymentSimulator.Messaging;

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
        var queueArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
        };
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: queueArguments, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, DeadLetterRoutingKey, cancellationToken: cancellationToken);
    }
}
