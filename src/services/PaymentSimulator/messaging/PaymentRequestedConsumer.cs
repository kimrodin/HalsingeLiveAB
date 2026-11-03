using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using PaymentSimulator.Features.Payments.ProcessPayment;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PaymentSimulator.Messaging;

public sealed class PaymentRequestedConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentRequestedConsumer> logger) : BackgroundService
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
                logger.LogError(exception, "Payment request consumer stopped; reconnecting shortly.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await PaymentRequestMessagingTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        consumer.ShutdownAsync += (_, _) =>
        {
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };
        consumer.UnregisteredAsync += (_, _) =>
        {
            disconnected.TrySetResult();
            return Task.CompletedTask;
        };
        consumer.ReceivedAsync += async (_, delivery) => await HandleDeliveryAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(PaymentRequestMessagingTopology.Queue, autoAck: false, consumer, stoppingToken);
        await disconnected.Task.WaitAsync(stoppingToken);
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        PaymentRequestedV1 message;
        try
        {
            message = JsonSerializer.Deserialize<PaymentRequestedV1>(delivery.Body.Span)
                ?? throw new InvalidDataException("The payment request was empty.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger.LogError(exception, "Rejecting an invalid payment request.");
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        for (var attempt = 1; attempt <= MaximumProcessingAttempts; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<PaymentRequestedHandler>();
                var result = await handler.HandleAsync(message, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                logger.LogInformation(
                    "Processed payment request {MessageId} for booking {BookingId}, correlation {CorrelationId}; payment {PaymentId}, status {Status}.",
                    message.MessageId, message.BookingId, message.CorrelationId, result.PaymentId, result.Status);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Leave the delivery unacknowledged so closing the channel requeues it.
                return;
            }
            catch (Exception exception) when (exception is ArgumentException or PaymentConflictException)
            {
                logger.LogError(exception, "Rejecting payment request {MessageId} for booking {BookingId}.",
                    message.MessageId, message.BookingId);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                return;
            }
            catch (Exception exception)
            {
                if (attempt == MaximumProcessingAttempts)
                {
                    logger.LogError(exception,
                        "Payment request {MessageId} failed after {Attempts} attempts; sending it to the dead-letter queue.",
                        message.MessageId, attempt);
                    await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                    return;
                }

                logger.LogWarning(exception, "Payment request {MessageId} failed on attempt {Attempt}; retrying.",
                    message.MessageId, attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), stoppingToken);
            }
        }
    }
}
