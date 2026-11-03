using Booking.Data;
using Booking.Features.BookingHolds;
using Booking.Features.Bookings.Models;
using Booking.Features.EventCatalog;
using Booking.Features.Payments;
using Booking.Features.Tickets;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;

namespace Booking.Features.Bookings;

public sealed partial class BookingService(
    BookingDbContext dbContext,
    RedisReservationService reservations,
    IPaymentGateway payments,
    IEventSnapshotProvider eventCatalog)
{
    public static readonly TimeSpan ReservationDuration = TimeSpan.FromMinutes(10);

    public async Task<ReservationResult> CreateReservationAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        BookingValidation.ValidateCreateBooking(request);
        var customerName = request.CustomerName.Trim();
        var customerEmail = request.CustomerEmail.Trim();

        var inventory = await dbContext.EventInventory
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.InventoryId == request.InventoryId, cancellationToken);

        if (inventory is null)
            return new ReservationResult(ReservationStatus.InventoryNotFound, null, null);

        BookingValidation.ValidatePriceAndTotal(inventory.Price, request.Quantity);
        var eventSnapshot = await eventCatalog.GetSnapshotAsync(
            inventory.EventId,
            inventory.SectionId,
            cancellationToken);

        // Populate Redis lazily for inventory rows created before Redis was wired in.
        await reservations.InitializeInventoryAsync(
            inventory.EventId,
            inventory.SectionId,
            inventory.AvailableQuantity);

        var bookingId = Guid.NewGuid();
        var guestAccessToken = GuestAccessToken.Create();
        var expiresAt = DateTimeOffset.UtcNow.Add(ReservationDuration);
        var reserved = await reservations.TryReserveAsync(
            bookingId,
            inventory.EventId,
            inventory.SectionId,
            request.Quantity,
            expiresAt);

        if (!reserved)
            return new ReservationResult(ReservationStatus.InsufficientInventory, null, null);

        var booking = new BookingModel
        {
            BookingId = bookingId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            GuestAccessTokenHash = GuestAccessToken.Hash(guestAccessToken),
            Status = BookingStatuses.Reserved,
            TotalPrice = inventory.Price * request.Quantity,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt.UtcDateTime,
            Items =
            [
                new BookingItem
                {
                    BookingItemId = Guid.NewGuid(),
                    InventoryId = inventory.InventoryId,
                    Quantity = request.Quantity,
                    UnitPrice = inventory.Price,
                    Currency = "SEK",
                    EventName = eventSnapshot.EventName,
                    EventStartsAt = eventSnapshot.EventStartsAt,
                    VenueName = eventSnapshot.VenueName,
                    SectionName = eventSnapshot.SectionName
                }
            ]
        };

        dbContext.Bookings.Add(booking);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await reservations.ReleaseReservationAsync(inventory.EventId, inventory.SectionId, bookingId);
            throw;
        }

        return new ReservationResult(ReservationStatus.Reserved, bookingId, expiresAt, guestAccessToken);
    }

    public async Task<BookingModel> GetBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings.AsNoTracking()
            .Include(booking => booking.Items)
            .ThenInclude(item => item.EventInventory)
            .FirstOrDefaultAsync(booking => booking.BookingId == bookingId, cancellationToken);
    }

    public Task<List<BookingModel>> GetBookingHistoryAsync(
        Guid bookingId,
        string customerEmail,
        CancellationToken cancellationToken = default) =>
        dbContext.Bookings.AsNoTracking()
            .Where(booking => booking.BookingId != bookingId
                && booking.CustomerEmail.ToLower() == customerEmail.ToLower())
            .OrderByDescending(booking => booking.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

    public async Task<bool> UpdateBookingCustomerAsync(
        Guid bookingId,
        string customerName,
        string customerEmail,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (booking is null)
            return false;

        booking.CustomerName = customerName.Trim();
        booking.CustomerEmail = customerEmail.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BookingModel> GetBookingForGuestAsync(
        Guid bookingId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings.AsNoTracking()
            .Include(item => item.Items)
            .ThenInclude(item => item.EventInventory)
            .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);

        return booking is not null && GuestAccessToken.Matches(accessToken, booking.GuestAccessTokenHash)
            ? booking
            : null;
    }

    public async Task<bool> GuestHasAccessAsync(
        Guid bookingId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = await dbContext.Bookings.AsNoTracking()
            .Where(item => item.BookingId == bookingId)
            .Select(item => item.GuestAccessTokenHash)
            .SingleOrDefaultAsync(cancellationToken);

        return GuestAccessToken.Matches(accessToken, tokenHash);
    }

    public async Task<BookingPage> ListBookingsAsync(
        ListBookingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Bookings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(booking =>
                booking.BookingId.ToString().Contains(search)
                || booking.CustomerName.ToLower().Contains(search)
                || booking.CustomerEmail.ToLower().Contains(search)
                || booking.Items.Any(item => item.EventName.ToLower().Contains(search)));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(booking => booking.Status == request.Status);
        if (request.From is DateTime from)
        {
            var start = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
            query = query.Where(booking => booking.CreatedAt >= start);
        }
        if (request.To is DateTime to)
        {
            var end = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(booking => booking.CreatedAt < end);
        }

        var statuses = await dbContext.Bookings.AsNoTracking()
            .Select(booking => booking.Status)
            .Distinct()
            .OrderBy(status => status)
            .ToArrayAsync(cancellationToken);
        var totalCount = await query.CountAsync(cancellationToken);
        var sortBy = request.SortBy?.ToLowerInvariant();
        var ascending = request.SortDirection?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true;
        IOrderedQueryable<BookingModel> ordered = (sortBy, ascending) switch
        {
            ("bookingid", true) => query.OrderBy(booking => booking.BookingId),
            ("bookingid", false) => query.OrderByDescending(booking => booking.BookingId),
            ("customer", true) => query.OrderBy(booking => booking.CustomerName),
            ("customer", false) => query.OrderByDescending(booking => booking.CustomerName),
            ("status", true) => query.OrderBy(booking => booking.Status),
            ("status", false) => query.OrderByDescending(booking => booking.Status),
            ("totalprice", true) => query.OrderBy(booking => booking.TotalPrice),
            ("totalprice", false) => query.OrderByDescending(booking => booking.TotalPrice),
            ("createdat", true) => query.OrderBy(booking => booking.CreatedAt),
            _ => query.OrderByDescending(booking => booking.CreatedAt)
        };
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var bookings = await ordered
            .ThenBy(booking => booking.BookingId)
            .Skip(skip)
            .Take(pageSize)
            .Include(booking => booking.Items)
            .ThenInclude(item => item.EventInventory)
            .ToListAsync(cancellationToken);

        return new BookingPage(bookings, totalCount, page, pageSize, statuses);
    }

    // Only the controlled payment workflow can call confirmation.
    private async Task<BookingActionResult> ConfirmAsync(
        Guid bookingId,
        PaymentResult payment,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // A row lock and transaction avoid a permanent Confirming state after a crash.
        await dbContext.Bookings.FromSqlInterpolated(
            $"SELECT * FROM \"Bookings\" WHERE \"BookingId\" = {bookingId} FOR UPDATE")
            .AsNoTracking().ToListAsync(cancellationToken);
        var booking = await dbContext.Bookings.AsNoTracking()
            .Include(item => item.Items).ThenInclude(item => item.EventInventory)
            .SingleOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (booking is null)
            return new BookingActionResult(BookingActionStatus.NotFound, null);
        if (!Matches(payment, booking) || payment.Status != "Succeeded" || payment.RefundedAt != null)
            return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);
        if (booking.Status == BookingStatuses.Confirmed)
            return new BookingActionResult(BookingActionStatus.Completed, booking.Status);
        if (booking.Status != BookingStatuses.Reserved || booking.ExpiresAt is null || booking.ExpiresAt <= DateTime.UtcNow)
            return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);

        var finalizedItems = new List<BookingItem>();
        var committing = false;
        try
        {
            foreach (var item in booking.Items)
            {
                // PostgreSQL remains the final stock authority even if Redis loses data.
                var updated = await dbContext.EventInventory
                    .Where(inventory => inventory.InventoryId == item.InventoryId
                        && inventory.AvailableQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(update => update.SetProperty(inventory => inventory.AvailableQuantity,
                        inventory => inventory.AvailableQuantity - item.Quantity), cancellationToken);
                if (updated == 0)
                    throw new ReservationConfirmationException();

                var result = await reservations.ConfirmReservationAsync(
                    item.EventInventory.EventId, item.EventInventory.SectionId, bookingId);
                if (result != ReservationFinalizationResult.Completed)
                    throw new ReservationConfirmationException();
                finalizedItems.Add(item);
            }

            // Database wall clock at the final transition, rather than transaction-start now().
            var transitioned = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Bookings" SET "Status" = {BookingStatuses.Confirmed}, "PaymentSettled" = TRUE
                WHERE "BookingId" = {bookingId} AND "Status" = {BookingStatuses.Reserved}
                    AND "ExpiresAt" > clock_timestamp()
                """, cancellationToken);
            if (transitioned != 1)
                throw new ReservationConfirmationException();

            AddTicketIssueOutboxMessages(booking.Items);
            await dbContext.SaveChangesAsync(cancellationToken);
            committing = true;
            await transaction.CommitAsync(cancellationToken);
            return new BookingActionResult(BookingActionStatus.Completed, BookingStatuses.Confirmed);
        }
        catch
        {
            // A lost COMMIT acknowledgement has an unknown outcome. Retain Redis markers:
            // reconciliation will read PostgreSQL again, or expiry will restore them.
            if (committing)
                throw;
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var item in finalizedItems)
                await reservations.RestoreConfirmedReservationAsync(
                    item.EventInventory.EventId, item.EventInventory.SectionId, bookingId);
            throw;
        }
    }

    private void AddTicketIssueOutboxMessages(IEnumerable<BookingItem> items)
    {
        var createdAt = DateTimeOffset.UtcNow;
        dbContext.TicketIssueOutboxMessages.AddRange(items.Select(item => new TicketIssueOutboxMessage
        {
            MessageId = Guid.NewGuid(),
            BookingId = item.BookingId,
            BookingItemId = item.BookingItemId,
            EventId = item.EventInventory.EventId,
            EventName = item.EventName,
            EventStartsAtUtc = item.EventStartsAt.ToUniversalTime(),
            VenueName = item.VenueName,
            SectionName = item.SectionName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Currency = item.Currency,
            CreatedAtUtc = createdAt,
            NextAttemptAtUtc = createdAt
        }));
    }

    private sealed class ReservationConfirmationException : Exception;
    public async Task<BookingActionResult> CancelAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        BookingValidation.ValidateId(bookingId, nameof(bookingId));
        var booking = await dbContext.Bookings
            .Include(item => item.Items)
            .ThenInclude(item => item.EventInventory)
            .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (booking is null)
            return new BookingActionResult(BookingActionStatus.NotFound, null);
        if (booking.Status == BookingStatuses.Cancelled)
            return new BookingActionResult(BookingActionStatus.Completed, booking.Status);
        if (booking.Status is BookingStatuses.Confirmed or BookingStatuses.Expired)
            return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);

        if (booking.Status == BookingStatuses.Reserved)
        {
            if (booking.ExpiresAt <= DateTime.UtcNow)
            {
                await ExpireBookingAsync(booking, cancellationToken);
                return new BookingActionResult(BookingActionStatus.Expired, BookingStatuses.Expired);
            }

            var started = await dbContext.Bookings
                .Where(item => item.BookingId == bookingId
                    && item.Status == BookingStatuses.Reserved
                    && item.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, BookingStatuses.Cancelling), cancellationToken);
            if (started == 0)
            {
                var currentStatus = await dbContext.Bookings.AsNoTracking()
                    .Where(item => item.BookingId == bookingId)
                    .Select(item => item.Status)
                    .SingleAsync(cancellationToken);
                if (currentStatus == BookingStatuses.Cancelled)
                    return new BookingActionResult(BookingActionStatus.Completed, currentStatus);
                if (currentStatus != BookingStatuses.Cancelling)
                    return new BookingActionResult(
                        currentStatus == BookingStatuses.Expired ? BookingActionStatus.Expired : BookingActionStatus.InvalidState,
                        currentStatus);
            }
            booking.Status = BookingStatuses.Cancelling;
        }
        else if (booking.Status != BookingStatuses.Cancelling)
        {
            return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);
        }

        foreach (var item in booking.Items)
        {
            await reservations.RestoreConfirmedReservationAsync(
                item.EventInventory.EventId, item.EventInventory.SectionId, bookingId);
            var result = await reservations.ReleaseReservationAsync(
                item.EventInventory.EventId,
                item.EventInventory.SectionId,
                booking.BookingId);

            if (result == ReservationFinalizationResult.Expired)
            {
                await dbContext.Bookings
                    .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Cancelling)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.Status, BookingStatuses.Expired), cancellationToken);
                return new BookingActionResult(BookingActionStatus.Expired, BookingStatuses.Expired);
            }

            if (result == ReservationFinalizationResult.NotFound)
                break;
        }

        var cancelledAt = DateTime.UtcNow;
        await dbContext.Bookings
            .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Cancelling)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, BookingStatuses.Cancelled)
                .SetProperty(item => item.CancelledAt, cancelledAt), cancellationToken);

        return new BookingActionResult(BookingActionStatus.Completed, BookingStatuses.Cancelled);
    }

    private async Task ExpireBookingAsync(BookingModel booking, CancellationToken cancellationToken)
    {
        var claimed = await dbContext.Bookings
            .Where(row => row.BookingId == booking.BookingId
                && row.Status == BookingStatuses.Reserved
                && row.ExpiresAt <= DateTime.UtcNow)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, BookingStatuses.Expiring), cancellationToken);

        if (claimed == 0 && booking.Status != BookingStatuses.Expiring)
            return;

        foreach (var item in booking.Items)
        {
            await reservations.RestoreConfirmedReservationAsync(item.EventInventory.EventId,
                item.EventInventory.SectionId, booking.BookingId);
            await reservations.ReleaseReservationAsync(item.EventInventory.EventId,
                item.EventInventory.SectionId, booking.BookingId);
        }

        await dbContext.Bookings
            .Where(row => row.BookingId == booking.BookingId && row.Status == BookingStatuses.Expiring)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, BookingStatuses.Expired), cancellationToken);
    }
}

public sealed record BookingPage(
    IReadOnlyList<BookingModel> Bookings,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<string> Statuses);

public enum ReservationStatus
{
    Reserved,
    InventoryNotFound,
    InsufficientInventory
}

public sealed record ReservationResult(
    ReservationStatus Status,
    Guid? BookingId,
    DateTimeOffset? ExpiresAt,
    string GuestAccessToken = "");

public static class BookingStatuses
{
    public const string Reserved = "Reserved";
    public const string Confirming = "Confirming";
    public const string Expiring = "Expiring";
    public const string Confirmed = "Confirmed";
    public const string Cancelling = "Cancelling";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string Failed = "Failed";
}

public enum BookingActionStatus
{
    Completed,
    Expired,
    InvalidState,
    NotFound,
    ReservationNotFound
}

public sealed record BookingActionResult(BookingActionStatus Status, string BookingStatus);
