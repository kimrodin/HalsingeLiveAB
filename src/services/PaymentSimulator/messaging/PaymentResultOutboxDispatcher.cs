using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;

namespace PaymentSimulator.Messaging;

public sealed class PaymentResultOutboxDispatcher(PaymentDbContext db, IPaymentResultPublisher publisher,
    TimeProvider clock, ILogger<PaymentResultOutboxDispatcher> logger)
{
    public async Task DispatchPendingAsync(CancellationToken token = default)
    {
        var now = clock.GetUtcNow();
        var messages = await db.PaymentResultOutboxMessages
            .Where(message => message.DeliveredAtUtc == null && message.NextAttemptAtUtc <= now)
            .OrderBy(message => message.OccurredAtUtc).Take(25).ToListAsync(token);
        foreach (var message in messages)
        {
            try
            {
                if (message.Outcome == "Succeeded")
                    await publisher.PublishAsync(new PaymentSucceededV1
                    {
                        MessageId = message.MessageId, CorrelationId = message.BookingId,
                        PaymentId = message.PaymentId, BookingId = message.BookingId,
                        Amount = message.Amount, Currency = message.Currency,
                        PaidAtUtc = message.PaidAtUtc ?? throw new InvalidDataException("Successful payment has no PaidAt."),
                        OccurredAtUtc = message.OccurredAtUtc
                    }, token);
                else if (message.Outcome is "Failed" or "TimedOut")
                    await publisher.PublishAsync(new PaymentFailedV1
                    {
                        MessageId = message.MessageId, CorrelationId = message.BookingId,
                        PaymentId = message.PaymentId, BookingId = message.BookingId,
                        Amount = message.Amount, Currency = message.Currency,
                        FailureCode = message.Outcome, OccurredAtUtc = message.OccurredAtUtc
                    }, token);
                else
                    throw new InvalidDataException($"Unsupported payment result: {message.Outcome}.");
                message.DeliveredAtUtc = clock.GetUtcNow();
                message.LastError = null;
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                message.AttemptCount++;
                message.NextAttemptAtUtc = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(8, message.AttemptCount))));
                message.LastError = exception.Message[..Math.Min(2000, exception.Message.Length)];
                logger.LogError(exception, "Could not publish payment result {MessageId}; retry scheduled.", message.MessageId);
            }
            await db.SaveChangesAsync(token);
        }
    }
}
