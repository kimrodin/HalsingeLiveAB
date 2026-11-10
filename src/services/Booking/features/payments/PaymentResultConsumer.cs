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
                using var deliveries = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ShutdownAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                consumer.UnregisteredAsync += (_, _) => { disconnected.TrySetResult(); return Task.CompletedTask; };
                consumer.ReceivedAsync += async (_, delivery) =>
                {
                    try { await ReceiveAsync(channel, delivery, deliveries.Token); }
                    catch (OperationCanceledException) when (deliveries.IsCancellationRequested) { }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "Payment result delivery interrupted; reconnecting for redelivery.");
                        disconnected.TrySetResult();
                    }
                };
                try
                {
                    await channel.BasicConsumeAsync(PaymentResultMessagingTopology.Queue, false, consumer, stoppingToken);
                    await disconnected.Task.WaitAsync(stoppingToken);
                }
                finally { await deliveries.CancelAsync(); }
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

        var messageContext = message switch
        {
            PaymentSucceededV1 succeeded => (succeeded.MessageId, succeeded.BookingId, succeeded.CorrelationId),
            PaymentFailedV1 failed => (failed.MessageId, failed.BookingId, failed.CorrelationId),
            _ => throw new InvalidDataException("Unknown payment result.")
        };

        for (var attempt = 1; !token.IsCancellationRequested; attempt++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var inbox = scope.ServiceProvider.GetRequiredService<PaymentResultInbox>();
                if (message is PaymentSucceededV1 succeeded) await inbox.ReceiveAsync(succeeded, token);
                else await inbox.ReceiveAsync((PaymentFailedV1)message, token);
                // The committed inbox owns retries, including refunds, after this acknowledgement.
                await channel.BasicAckAsync(delivery.DeliveryTag, false, token);
                if (message is PaymentSucceededV1 succeededMessage)
                    logger.LogInformation(
                        "Received successful payment {PaymentId} for booking {BookingId}, correlation {CorrelationId} (message {MessageId}).",
                        succeededMessage.PaymentId, succeededMessage.BookingId, succeededMessage.CorrelationId,
                        succeededMessage.MessageId);
                else if (message is PaymentFailedV1 failed)
                    logger.LogWarning(
                        "Received failed payment {PaymentId} for booking {BookingId}, correlation {CorrelationId}; reason {FailureCode} (message {MessageId}).",
                        failed.PaymentId, failed.BookingId, failed.CorrelationId, failed.FailureCode, failed.MessageId);
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
                if (!channel.IsOpen) return;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempt - 1, 5))));
                logger.LogWarning(exception,
                    "Could not store payment result {MessageId} for booking {BookingId}, correlation {CorrelationId} on attempt {Attempt}; retaining the unacknowledged delivery and retrying in {RetryDelay}.",
                    messageContext.MessageId, messageContext.BookingId, messageContext.CorrelationId, attempt, delay);
                await Task.Delay(delay, token);
            }
        }
    }
}
