using System.Text.Json;
using HalsingeLive.Contracts.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Infrastructure.Messaging;

namespace TicketService.Messaging.Issuance;

public sealed class BookingConfirmedConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<BookingConfirmedConsumer> logger) : BackgroundService
{
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
        await TicketMessagingTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deliveries = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
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
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try { await HandleDeliveryAsync(channel, delivery, deliveries.Token); }
            catch (OperationCanceledException) when (deliveries.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Ticket issuance delivery interrupted; reconnecting for redelivery.");
                disconnected.TrySetResult();
            }
        };
        try
        {
            await channel.BasicConsumeAsync(TicketMessagingTopology.Queue, autoAck: false, consumer, stoppingToken);
            await disconnected.Task.WaitAsync(stoppingToken);
        }
        finally { await deliveries.CancelAsync(); }
    }

    private async Task HandleDeliveryAsync(
        IChannel channel,
        BasicDeliverEventArgs delivery,
        CancellationToken stoppingToken)
    {
        BookingConfirmed message;
        try
        {
            message = JsonSerializer.Deserialize<BookingConfirmed>(delivery.Body.Span)
                ?? throw new InvalidDataException("The booking confirmation message was empty.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            logger.LogError(exception, "Rejecting an invalid booking confirmation message.");
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<IssueTicketsHandler>();
                var result = await handler.HandleAsync(message, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                logger.LogInformation(
                    "Processed booking confirmation {MessageId} for booking {BookingId}, correlation {CorrelationId}, item {BookingItemId}; already issued: {AlreadyIssued}.",
                    message.MessageId,
                    message.BookingId,
                    message.CorrelationId,
                    message.BookingItemId,
                    result.AlreadyIssued);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is TicketIssueValidationException or TicketIssueConflictException)
            {
                logger.LogError(exception,
                    "Rejecting invalid or conflicting booking confirmation {MessageId} for booking {BookingId}, correlation {CorrelationId}.",
                    message.MessageId, message.BookingId, message.CorrelationId);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                return;
            }
            catch (Exception exception)
            {
                if (!channel.IsOpen) return;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, Math.Min(attempt - 1, 5))));
                logger.LogWarning(exception,
                    "Ticket issuance attempt {Attempt} failed for message {MessageId}, booking {BookingId}, correlation {CorrelationId}; retaining the unacknowledged delivery and retrying in {RetryDelay}.",
                    attempt,
                    message.MessageId, message.BookingId, message.CorrelationId, delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}

