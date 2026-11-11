using RabbitMQ.Client;

namespace TicketService.Messaging.Issuance;

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
