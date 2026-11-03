using Booking.Data;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Booking.Features.Payments;

public sealed class PaymentResultInbox(BookingDbContext db, TimeProvider clock)
{
    public Task ReceiveAsync(PaymentSucceededV1 message, CancellationToken token = default) => ReceiveAsync(new PaymentResultInboxMessage
    {
        MessageId = message.MessageId, CorrelationId = message.CorrelationId, PaymentId = message.PaymentId,
        BookingId = message.BookingId, Amount = message.Amount, Currency = message.Currency,
        Outcome = "Succeeded", PaidAtUtc = message.PaidAtUtc, OccurredAtUtc = message.OccurredAtUtc
    }, token);

    public Task ReceiveAsync(PaymentFailedV1 message, CancellationToken token = default) => ReceiveAsync(new PaymentResultInboxMessage
    {
        MessageId = message.MessageId, CorrelationId = message.CorrelationId, PaymentId = message.PaymentId,
        BookingId = message.BookingId, Amount = message.Amount, Currency = message.Currency,
        Outcome = message.FailureCode, OccurredAtUtc = message.OccurredAtUtc
    }, token);

    private async Task ReceiveAsync(PaymentResultInboxMessage message, CancellationToken token)
    {
        Validate(message);
        var existing = await db.PaymentResultInboxMessages.AsNoTracking().SingleOrDefaultAsync(row => row.MessageId == message.MessageId, token);
        if (existing is not null)
        {
            EnsureSameMessage(existing, message);
            return;
        }
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(row => row.BookingId == message.BookingId, token);
        if (booking is null || !booking.PaymentRequested || booking.TotalPrice != message.Amount || message.Currency != "SEK")
            throw new InvalidDataException("Payment result does not match a requested booking payment.");
        message.PaidAtUtc = message.PaidAtUtc?.ToUniversalTime();
        message.OccurredAtUtc = message.OccurredAtUtc.ToUniversalTime();
        message.ReceivedAtUtc = message.NextAttemptAtUtc = clock.GetUtcNow();
        db.PaymentResultInboxMessages.Add(message);
        try
        {
            await db.SaveChangesAsync(token);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_PaymentResultInboxMessages" })
        {
            db.ChangeTracker.Clear();
            existing = await db.PaymentResultInboxMessages.AsNoTracking().SingleAsync(row => row.MessageId == message.MessageId, token);
            EnsureSameMessage(existing, message);
        }
    }

    private static void Validate(PaymentResultInboxMessage message)
    {
        if (message.MessageId == Guid.Empty || message.CorrelationId == Guid.Empty
            || message.PaymentId == Guid.Empty || message.BookingId == Guid.Empty)
            throw new InvalidDataException("MessageId, CorrelationId, PaymentId and BookingId must be non-empty.");
        if (message.OccurredAtUtc == default || message.Outcome is not ("Succeeded" or "Failed" or "TimedOut"))
            throw new InvalidDataException("Payment result requires an occurrence time and a known terminal outcome.");
        if (message.Amount <= 0 || message.Amount > 9999999999999999.99m || decimal.Round(message.Amount, 2) != message.Amount
            || message.Currency is not { Length: 3 } || message.Currency.Any(character => character is < 'A' or > 'Z'))
            throw new InvalidDataException("Payment result requires a positive amount with at most two decimals and an uppercase currency code.");
        if (message.Outcome == "Succeeded" && (message.PaidAtUtc is null || message.PaidAtUtc == default(DateTimeOffset)
            || message.PaidAtUtc > message.OccurredAtUtc))
            throw new InvalidDataException("Successful payment requires a valid PaidAtUtc no later than OccurredAtUtc.");
    }

    private static void EnsureSameMessage(PaymentResultInboxMessage existing, PaymentResultInboxMessage incoming)
    {
        if (existing.CorrelationId != incoming.CorrelationId || existing.PaymentId != incoming.PaymentId
            || existing.BookingId != incoming.BookingId || existing.Amount != incoming.Amount || existing.Currency != incoming.Currency
            || existing.Outcome != incoming.Outcome || !SameTime(existing.PaidAtUtc, incoming.PaidAtUtc)
            || !SameTime(existing.OccurredAtUtc, incoming.OccurredAtUtc))
            throw new InvalidDataException("MessageId was reused with a different payment result.");
    }

    private static bool SameTime(DateTimeOffset? left, DateTimeOffset? right) => left?.UtcTicks / 10 == right?.UtcTicks / 10;
}
