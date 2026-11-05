using Booking.Data;
using Booking.Features.Bookings;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Payments;

public sealed class PaymentRequestOutboxDispatcher(BookingDbContext db, IPaymentRequestedPublisher publisher,
    TimeProvider clock, ILogger<PaymentRequestOutboxDispatcher> logger)
{
    public async Task DispatchPendingAsync(CancellationToken token = default)
    {
        var now = clock.GetUtcNow();
        var messages = await db.PaymentRequestOutboxMessages
            .Where(row => row.DeliveredAtUtc == null && row.SuppressedAtUtc == null && row.NextAttemptAtUtc <= now)
            .OrderBy(row => row.CreatedAtUtc).Take(25).ToListAsync(token);
        foreach (var message in messages)
        {
            try
            {
                // Never deliberately start a payment for an invalid reservation. A publish
                // may already have succeeded before an earlier failure; reconciliation
                // independently fences/cancels/refunds that attempt before settling it.
                var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(row => row.BookingId == message.BookingId, token);
                if (booking is null || !booking.PaymentRequested || booking.Status != BookingStatuses.Reserved || booking.PaymentSettled
                    || booking.ExpiresAt is null || booking.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
                {
                    message.SuppressedAtUtc = clock.GetUtcNow();
                    message.LastError = null;
                }
                else
                {
                    await publisher.PublishAsync(new PaymentRequestedV1
                    {
                        MessageId = message.MessageId, CorrelationId = message.BookingId,
                        BookingId = message.BookingId, Amount = message.Amount, Currency = message.Currency,
                        OccurredAtUtc = message.CreatedAtUtc
                    }, token);
                    message.DeliveredAtUtc = clock.GetUtcNow();
                    message.LastError = null;
                }
            }
            catch (Exception exception) when (!token.IsCancellationRequested)
            {
                message.AttemptCount++;
                message.NextAttemptAtUtc = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(8, message.AttemptCount))));
                message.LastError = exception.Message[..Math.Min(2000, exception.Message.Length)];
                logger.LogError(exception, "Payment request {MessageId} for booking {BookingId} remains pending; retry scheduled.",
                    message.MessageId, message.BookingId);
            }
            await db.SaveChangesAsync(token);
        }
    }
}
