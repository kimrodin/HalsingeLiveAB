using Booking.Data;
using Booking.Features.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Booking.Features.Statistics;

namespace Booking.Features.Payments;

public sealed class PaymentResultInboxDispatcher(BookingDbContext db, BookingService bookings,
    TimeProvider clock, ILogger<PaymentResultInboxDispatcher> logger,
    IHubContext<SalesUpdatesHub> salesHub = null)
{
    public async Task DispatchPendingAsync(CancellationToken token = default)
    {
        var now = clock.GetUtcNow();
        var messages = await db.PaymentResultInboxMessages
            .Where(row => row.ProcessedAtUtc == null && row.RejectedAtUtc == null && row.NextAttemptAtUtc <= now)
            .OrderBy(row => row.ReceivedAtUtc).Take(25).AsNoTracking().ToListAsync(token);
        foreach (var message in messages)
        {
            try
            {
                var result = await bookings.ApplyPaymentResultAsync(message, token);
                if (result.PaymentStatus is not ("Succeeded" or "Failed" or "TimedOut" or "Refunded"))
                    throw new HttpRequestException(result.Message);
                var processed = await db.PaymentResultInboxMessages.Where(row => row.MessageId == message.MessageId && row.RejectedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.ProcessedAtUtc, clock.GetUtcNow())
                        .SetProperty(row => row.LastError, (string)null), token);
                if (processed != 0 && (result.PaymentStatus is "Succeeded" or "Refunded"))
                {
                    try { if (salesHub is not null) await salesHub.Clients.All.SendAsync("SalesUpdated", cancellationToken: token); }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        logger.LogWarning(exception, "Could not notify admin clients about updated sales statistics.");
                    }
                }
            }
            catch (InvalidDataException exception)
            {
                var error = exception.Message[..Math.Min(2000, exception.Message.Length)];
                await db.PaymentResultInboxMessages.Where(row => row.MessageId == message.MessageId && row.ProcessedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.RejectedAtUtc, clock.GetUtcNow())
                        .SetProperty(row => row.LastError, error), token);
                logger.LogError(exception, "Quarantined payment result {MessageId} for booking {BookingId}.", message.MessageId, message.BookingId);
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                var error = exception.Message[..Math.Min(2000, exception.Message.Length)];
                var retry = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(8, message.AttemptCount + 1))));
                await db.PaymentResultInboxMessages.Where(row => row.MessageId == message.MessageId
                        && row.ProcessedAtUtc == null && row.RejectedAtUtc == null)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.AttemptCount, row => row.AttemptCount + 1)
                        .SetProperty(row => row.NextAttemptAtUtc, retry).SetProperty(row => row.LastError, error), token);
                logger.LogError(exception, "Payment result {MessageId} remains pending; retry scheduled.", message.MessageId);
            }
            finally
            {
                // CancelAsync loads tracked booking state; each receipt must read fresh state.
                db.ChangeTracker.Clear();
            }
        }
    }
}
