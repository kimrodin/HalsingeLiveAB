using Booking.Data;
using HalsingeLive.Contracts.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Tickets;

public sealed class TicketIssueOutboxDispatcher(
    BookingDbContext dbContext,
    IBookingConfirmedPublisher publisher,
    ITicketServiceClient ticketServiceClient,
    TimeProvider timeProvider,
    ILogger<TicketIssueOutboxDispatcher> logger)
{
    private const int BatchSize = 25;
    private const int MaximumErrorLength = 2000;

    public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var messages = await dbContext.TicketIssueOutboxMessages
            .Where(message => message.DeliveredAtUtc == null && message.NextAttemptAtUtc <= now)
            .OrderBy(message => message.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            if (!message.BookingId.ToString().StartsWith("51000000-", StringComparison.Ordinal))
            {
                await DispatchMessageAsync(message, cancellationToken);
                continue;
            }
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(51000000)", cancellationToken);
            // A deletion can remove queued messages while this batch waits on its
            // lock. Reload instead of issuing a stale copy from the original batch.
            await dbContext.Entry(message).ReloadAsync(cancellationToken);
            if (dbContext.Entry(message).State == EntityState.Detached || message.DeliveredAtUtc is not null)
                continue;
            if (!await dbContext.Bookings.AnyAsync(row => row.BookingId == message.BookingId
                && row.Status == Bookings.BookingStatuses.Confirmed, cancellationToken))
                continue;
            await DispatchMessageAsync(message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task DispatchMessageAsync(TicketIssueOutboxMessage message, CancellationToken cancellationToken)
    {
        try
        {
            if (message.BookingId.ToString().StartsWith("51000000-", StringComparison.Ordinal))
            {
                // Seed deletion shares the lock above. Wait for issuance to finish;
                // a broker confirmation would still leave tickets queued after deletion.
                await ticketServiceClient.IssueTicketsAsync(ToContract(message), cancellationToken);
            }
            else
            {
                await publisher.PublishAsync(ToContract(message), cancellationToken);
            }
            message.DeliveredAtUtc = timeProvider.GetUtcNow();
            message.LastError = null;
            logger.LogInformation(
                "Delivered ticket issuance for booking {BookingId}, correlation {CorrelationId}, item {BookingItemId}.",
                message.BookingId,
                message.BookingId,
                message.BookingItemId);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            message.AttemptCount++;
            message.NextAttemptAtUtc = timeProvider.GetUtcNow()
                .Add(GetRetryDelay(message.AttemptCount));
            message.LastError = Truncate(exception.Message);
            logger.LogError(
                exception,
                "Could not issue tickets for booking {BookingId}, correlation {CorrelationId}, item {BookingItemId}; retry scheduled for {NextAttemptAtUtc}.",
                message.BookingId,
                message.BookingId,
                message.BookingItemId,
                message.NextAttemptAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static BookingConfirmedV1 ToContract(TicketIssueOutboxMessage message) => new()
    {
        MessageId = message.MessageId,
        CorrelationId = message.BookingId,
        BookingId = message.BookingId,
        BookingItemId = message.BookingItemId,
        EventId = message.EventId,
        EventName = message.EventName,
        EventStartsAtUtc = message.EventStartsAtUtc,
        VenueName = message.VenueName,
        SectionName = message.SectionName,
        Quantity = message.Quantity,
        UnitPrice = message.UnitPrice,
        Currency = message.Currency,
        OccurredAtUtc = message.CreatedAtUtc.UtcDateTime
    };

    private static TimeSpan GetRetryDelay(int attemptCount) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Min(attemptCount, 8)), 300));

    private static string Truncate(string error) =>
        error.Length <= MaximumErrorLength ? error : error[..MaximumErrorLength];
}
