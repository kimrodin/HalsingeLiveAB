using System.Text.Json;
using Booking.Features.Tickets;
using HalsingeLive.Contracts.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Booking.Features.Payments;

public sealed class PaymentResultConsumer(RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopes, ILogger<PaymentResultConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await PaymentResultMessagingTopology.DeclareAsync(channel, stoppingToken);
                await channel.BasicQosAsync(0, 1, false, stoppingToken);
                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                consumer.UnregisteredAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                consumer.ReceivedAsync += (_, delivery) => ReceiveAsync(channel, delivery, stoppingToken);
                await channel.BasicConsumeAsync(PaymentResultMessagingTopology.Queue, false, consumer, stoppingToken);
                await disconnected.Task.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Payment result consumer stopped; reconnecting shortly.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ReceiveAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken token)
    {
        object message;
        try
        {
            message = delivery.RoutingKey switch
            {
                PaymentResultMessagingTopology.Succeeded => JsonSerializer.Deserialize<PaymentSucceededV1>(delivery.Body.Span),
                PaymentResultMessagingTopology.Failed => JsonSerializer.Deserialize<PaymentFailedV1>(delivery.Body.Span),
                _ => throw new InvalidDataException("Unknown payment result routing key.")
            };
            if (message is null) throw new InvalidDataException("Payment result was empty.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger.LogError(exception, "Rejecting an invalid payment result.");
            await channel.BasicNackAsync(delivery.DeliveryTag, false, false, token);
            return;
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var inbox = scope.ServiceProvider.GetRequiredService<PaymentResultInbox>();
                if (message is PaymentSucceededV1 succeeded) await inbox.ReceiveAsync(succeeded, token);
                else await inbox.ReceiveAsync((PaymentFailedV1)message, token);
                // The committed inbox owns retries, including refunds, after this acknowledgement.
                await channel.BasicAckAsync(delivery.DeliveryTag, false, token);
                return;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (InvalidDataException exception)
            {
                logger.LogError(exception, "Rejecting a payment result that failed contract validation.");
                await channel.BasicNackAsync(delivery.DeliveryTag, false, false, token);
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not store payment result on attempt {Attempt}.", attempt);
                if (attempt == 3)
                {
                    await channel.BasicNackAsync(delivery.DeliveryTag, false, false, token);
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(attempt), token);
            }
        }
    }
}
