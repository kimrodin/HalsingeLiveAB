using Booking.Data;
using Booking.Features.Payments.Gateway;
using Booking.Features.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Privacy;

public sealed class PrivacyOperationsService(
    BookingDbContext db,
    IPaymentGateway paymentGateway,
    IPaymentPrivacyDataClient paymentPrivacyDataClient,
    ITicketServiceClient ticketServiceClient,
    TimeProvider clock) : IPrivacyOperationsService
{
    private const int RetentionBatchSize = 250;

    public async Task<PersonalDataSummaryResponse> GetSummaryAsync(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking()
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (booking is null)
            return null;

        var payment = await paymentGateway.GetByBookingAsync(bookingId, cancellationToken);
        var paymentResultOutbox = await paymentPrivacyDataClient.GetResultOutboxAsync(
            bookingId, cancellationToken);
        var tickets = await ticketServiceClient.GetTicketsForBookingAsync(bookingId, cancellationToken);

        var processingRecords = new List<PersonalDataProcessingRecordResponse>();
        processingRecords.AddRange(await db.PaymentRequestOutboxMessages.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new PersonalDataProcessingRecordResponse(
                item.MessageId,
                "PaymentRequestOutbox",
                item.DeliveredAtUtc != null ? "Delivered"
                    : item.SuppressedAtUtc != null ? "Suppressed" : "Pending",
                item.CreatedAtUtc,
                item.AttemptCount))
            .ToListAsync(cancellationToken));
        processingRecords.AddRange(await db.PaymentResultInboxMessages.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new PersonalDataProcessingRecordResponse(
                item.MessageId,
                "PaymentResultInbox",
                item.ProcessedAtUtc != null ? "Processed"
                    : item.RejectedAtUtc != null ? "Rejected" : "Pending",
                item.ReceivedAtUtc,
                item.AttemptCount))
            .ToListAsync(cancellationToken));
        processingRecords.AddRange(await db.TicketIssueOutboxMessages.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => new PersonalDataProcessingRecordResponse(
                item.MessageId,
                "TicketIssueOutbox",
                item.DeliveredAtUtc != null ? "Delivered" : "Pending",
                item.CreatedAtUtc,
                item.AttemptCount))
            .ToListAsync(cancellationToken));

        return new PersonalDataSummaryResponse(
            booking.BookingId,
            booking.CustomerId,
            booking.CustomerName,
            booking.CustomerEmail,
            booking.PseudonymizedCustomerId,
            booking.Status,
            booking.TotalPrice,
            new DateTimeOffset(DateTime.SpecifyKind(booking.CreatedAt, DateTimeKind.Utc)),
            booking.PersonalDataPseudonymizedAtUtc,
            booking.Items.Select(item => new PersonalDataBookingItemResponse(
                item.BookingItemId,
                item.EventName,
                item.EventStartsAt,
                item.VenueName,
                item.SectionName,
                item.Quantity,
                item.UnitPrice,
                item.Currency)).ToArray(),
            payment,
            paymentResultOutbox,
            tickets,
            processingRecords.OrderBy(item => item.CreatedAtUtc).ToArray(),
            [
                "RabbitMQ queues and dead-letter queues: inspect messages by BookingId; do not re-publish during review.",
                "Inbox/outbox free-text errors: inspect for personal data before including them in the response.",
                "Redis: check transient reservation keys and their expiry.",
                "Seq and container logs: search BookingId/CorrelationId and apply the documented log-retention process.",
                "Browser sessionStorage: the guest access token is not exportable from the server; its server-side hash is retained by pseudonymization.",
                "Backups: follow the backup restore and re-apply-pseudonymization procedure.",
                "Keycloak: the current customer flow uses guest tokens; verify realm accounts and event logs for any linked identity."
            ]);
    }

    public async Task<bool> PseudonymizeAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings
            .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (booking is null)
            return false;
        if (booking.PersonalDataPseudonymizedAtUtc is not null)
            return true;

        var normalizedEmail = NormalizeEmail(booking.CustomerEmail);
        List<Features.Bookings.Models.Booking> group;
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            group = [booking];
        }
        else
        {
            group = await db.Bookings
                .Where(item => item.CustomerEmail.Trim().ToLower() == normalizedEmail)
                .ToListAsync(cancellationToken);
        }
        if (!group.Contains(booking))
            group.Add(booking);

        var pseudonymizedCustomerId = GetOrCreateCustomerLinkId(group);
        var now = clock.GetUtcNow();
        foreach (var member in group)
            Pseudonymize(member, now, pseudonymizedCustomerId);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RetentionPreviewResponse> GetRetentionPreviewAsync(
        CancellationToken cancellationToken)
    {
        var cutoff = GetCutoff();
        var query = db.Bookings.AsNoTracking()
            .Where(item => item.CreatedAt <= cutoff.UtcDateTime
                && item.PersonalDataPseudonymizedAtUtc == null);
        var totalCount = await query.CountAsync(cancellationToken);
        var candidates = await query
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.BookingId)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        return new RetentionPreviewResponse(
            cutoff,
            totalCount,
            candidates.Select(item => new RetentionCandidateResponse(
                item.BookingId,
                item.Status,
                new DateTimeOffset(DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc)))).ToArray());
    }

    public async Task<int> PseudonymizeExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = GetCutoff();
        var candidates = await db.Bookings
            .Where(item => item.CreatedAt <= cutoff.UtcDateTime
                && item.PersonalDataPseudonymizedAtUtc == null)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.BookingId)
            .Take(RetentionBatchSize)
            .ToListAsync(cancellationToken);

        var normalizedEmails = candidates
            .Select(item => NormalizeEmail(item.CustomerEmail))
            .Where(email => !string.IsNullOrEmpty(email))
            .Distinct()
            .ToArray();
        var relatedBookings = normalizedEmails.Length == 0
            ? []
            : await db.Bookings
                .Where(item => normalizedEmails.Contains(item.CustomerEmail.Trim().ToLower()))
                .ToListAsync(cancellationToken);

        var groups = relatedBookings
            .GroupBy(item => NormalizeEmail(item.CustomerEmail))
            .ToDictionary(group => group.Key, group => group.ToList());
        var now = clock.GetUtcNow();
        var processed = 0;
        foreach (var candidate in candidates)
        {
            if (candidate.PersonalDataPseudonymizedAtUtc is not null)
                continue;

            var normalizedEmail = NormalizeEmail(candidate.CustomerEmail);
            List<Features.Bookings.Models.Booking> group;
            if (!string.IsNullOrEmpty(normalizedEmail)
                && groups.TryGetValue(normalizedEmail, out var matches))
                group = matches;
            else
                group = [candidate];
            if (!group.Contains(candidate))
                group.Add(candidate);

            var pseudonymizedCustomerId = GetOrCreateCustomerLinkId(group);
            foreach (var member in group)
            {
                if (member.PersonalDataPseudonymizedAtUtc is null)
                    processed++;
                Pseudonymize(member, now, pseudonymizedCustomerId);
            }
        }

        if (processed > 0)
            await db.SaveChangesAsync(cancellationToken);

        return processed;
    }

    private DateTimeOffset GetCutoff() => clock.GetUtcNow().AddYears(-2);

    private static Guid GetOrCreateCustomerLinkId(
        IReadOnlyList<Features.Bookings.Models.Booking> group) =>
        group.Select(item => item.PseudonymizedCustomerId)
            .FirstOrDefault(id => id.HasValue) ?? Guid.NewGuid();

    private static string NormalizeEmail(string email) => email?.Trim().ToLowerInvariant();

    private static void Pseudonymize(
        Features.Bookings.Models.Booking booking,
        DateTimeOffset atUtc,
        Guid pseudonymizedCustomerId)
    {
        if (booking.PersonalDataPseudonymizedAtUtc is null)
        {
            booking.CustomerName = string.Empty;
            booking.CustomerEmail = string.Empty;
            booking.CustomerId = null;
            booking.PersonalDataPseudonymizedAtUtc = atUtc;
        }
        booking.PseudonymizedCustomerId = pseudonymizedCustomerId;
    }
}

/// <summary>Contains booking-linked personal data and processing records.</summary>
public sealed record PersonalDataSummaryResponse(
    Guid BookingId,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    Guid? PseudonymizedCustomerId,
    string Status,
    decimal TotalPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PseudonymizedAtUtc,
    IReadOnlyList<PersonalDataBookingItemResponse> Items,
    PaymentResult Payment,
    IReadOnlyList<PaymentResultOutboxSummaryResponse> PaymentResultOutbox,
    IReadOnlyList<TicketServiceTicketResponse> Tickets,
    IReadOnlyList<PersonalDataProcessingRecordResponse> ProcessingRecords,
    IReadOnlyList<string> ManualReviewSources);

/// <summary>Describes one event item associated with a booking.</summary>
public sealed record PersonalDataBookingItemResponse(
    Guid BookingItemId,
    string EventName,
    DateTimeOffset EventStartsAt,
    string VenueName,
    string SectionName,
    int Quantity,
    decimal UnitPrice,
    string Currency);

/// <summary>Describes a persisted inbox or outbox record.</summary>
public sealed record PersonalDataProcessingRecordResponse(
    Guid MessageId,
    string Type,
    string State,
    DateTimeOffset CreatedAtUtc,
    int AttemptCount);

/// <summary>Previews records eligible for the two-year pseudonymization job.</summary>
public sealed record RetentionPreviewResponse(
    DateTimeOffset CutoffUtc,
    int TotalCount,
    IReadOnlyList<RetentionCandidateResponse> Candidates);

/// <summary>Identifies one booking that is eligible for pseudonymization.</summary>
public sealed record RetentionCandidateResponse(
    Guid BookingId,
    string Status,
    DateTimeOffset CreatedAt);
