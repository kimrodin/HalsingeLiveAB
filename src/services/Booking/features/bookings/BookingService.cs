using Booking.Data;
using Booking.Features.BookingHolds;
using Booking.Features.Bookings.Models;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;

namespace Booking.Features.Bookings;

public sealed class BookingService(
    BookingDbContext dbContext,
    RedisReservationService reservations)
{
    public static readonly TimeSpan ReservationDuration = TimeSpan.FromMinutes(10);

    public async Task<ReservationResult> CreateReservationAsync(
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty)
            throw new ArgumentException("EventId must be a non-empty UUID.", nameof(request));
        if (request.SectionId == Guid.Empty)
            throw new ArgumentException("SectionId must be a non-empty UUID.", nameof(request));
        if (request.Quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Quantity must be positive.");
        if (string.IsNullOrWhiteSpace(request.CustomerId))
            throw new ArgumentException("CustomerId is required.", nameof(request));

        var inventory = await dbContext.EventInventory
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.EventId == request.EventId && item.SectionId == request.SectionId,
                cancellationToken);

        if (inventory is null)
            return new ReservationResult(ReservationStatus.InventoryNotFound, null, null);

        // Populate Redis lazily for inventory rows created before Redis was wired in.
        await reservations.InitializeInventoryAsync(
            inventory.EventId,
            inventory.SectionId,
            inventory.AvailableQuantity);

        var bookingId = Guid.NewGuid();
        var expiresAt = DateTimeOffset.UtcNow.Add(ReservationDuration);
        var reserved = await reservations.TryReserveAsync(
            bookingId,
            inventory.EventId,
            inventory.SectionId,
            request.Quantity,
            request.CustomerId.Trim(),
            expiresAt);

        if (!reserved)
            return new ReservationResult(ReservationStatus.InsufficientInventory, null, null);

        var booking = new BookingModel
        {
            BookingId = bookingId,
            CustomerId = request.CustomerId.Trim(),
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
                    UnitPrice = inventory.Price
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

        return new ReservationResult(ReservationStatus.Reserved, bookingId, expiresAt);
    }

    public Task<BookingModel> GetBookingAsync(Guid bookingId, CancellationToken cancellationToken = default) =>
        dbContext.Bookings.AsNoTracking()
            .Include(booking => booking.Items)
            .FirstOrDefaultAsync(booking => booking.BookingId == bookingId, cancellationToken)!;

    public async Task<BookingActionResult> ConfirmAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .Include(item => item.Items)
            .ThenInclude(item => item.EventInventory)
            .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);
        if (booking is null)
            return new BookingActionResult(BookingActionStatus.NotFound, null);
        if (booking.Status == BookingStatuses.Confirmed)
            return new BookingActionResult(BookingActionStatus.Completed, booking.Status);
        if (booking.Status is BookingStatuses.Cancelled or BookingStatuses.Expired)
            return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);

        if (booking.Status == BookingStatuses.Reserved)
        {
            if (booking.ExpiresAt <= DateTime.UtcNow)
            {
                await dbContext.Bookings
                    .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Reserved)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.Status, BookingStatuses.Expired), cancellationToken);
                return new BookingActionResult(BookingActionStatus.Expired, BookingStatuses.Expired);
            }

            var started = await dbContext.Bookings
                .Where(item => item.BookingId == bookingId
                    && item.Status == BookingStatuses.Reserved
                    && item.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(item => item.Status, BookingStatuses.Confirming), cancellationToken);
            if (started == 0)
            {
                var currentStatus = await dbContext.Bookings.AsNoTracking()
                    .Where(item => item.BookingId == bookingId)
                    .Select(item => item.Status)
                    .SingleAsync(cancellationToken);
                if (currentStatus == BookingStatuses.Confirmed)
                    return new BookingActionResult(BookingActionStatus.Completed, currentStatus);
                if (currentStatus != BookingStatuses.Confirming)
                    return new BookingActionResult(
                        currentStatus == BookingStatuses.Expired ? BookingActionStatus.Expired : BookingActionStatus.InvalidState,
                        currentStatus);
            }
            booking.Status = BookingStatuses.Confirming;
        }
        else if (booking.Status != BookingStatuses.Confirming)
        {
            return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);
        }

        foreach (var item in booking.Items)
        {
            var result = await reservations.ConfirmReservationAsync(
                item.EventInventory.EventId,
                item.EventInventory.SectionId,
                booking.BookingId);

            if (result == ReservationFinalizationResult.Expired)
            {
                await dbContext.Bookings
                    .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Confirming)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.Status, BookingStatuses.Expired), cancellationToken);
                return new BookingActionResult(BookingActionStatus.Expired, BookingStatuses.Expired);
            }

            if (result == ReservationFinalizationResult.NotFound)
            {
                await dbContext.Bookings
                    .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Confirming)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.Status, BookingStatuses.Failed), cancellationToken);
                return new BookingActionResult(BookingActionStatus.ReservationNotFound, BookingStatuses.Failed);
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var transitioned = await dbContext.Bookings
            .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Confirming)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, BookingStatuses.Confirmed), cancellationToken);

        if (transitioned == 1)
        {
            foreach (var item in booking.Items)
            {
                await dbContext.EventInventory
                    .Where(inventory => inventory.InventoryId == item.InventoryId)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(inventory => inventory.AvailableQuantity,
                            inventory => inventory.AvailableQuantity - item.Quantity), cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new BookingActionResult(BookingActionStatus.Completed, BookingStatuses.Confirmed);
    }

    public async Task<BookingActionResult> CancelAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
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
                await dbContext.Bookings
                    .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Reserved)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(item => item.Status, BookingStatuses.Expired), cancellationToken);
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

        await dbContext.Bookings
            .Where(item => item.BookingId == bookingId && item.Status == BookingStatuses.Cancelling)
            .ExecuteUpdateAsync(update => update
                .SetProperty(item => item.Status, BookingStatuses.Cancelled), cancellationToken);

        return new BookingActionResult(BookingActionStatus.Completed, BookingStatuses.Cancelled);
    }
}

public enum ReservationStatus
{
    Reserved,
    InventoryNotFound,
    InsufficientInventory
}

public sealed record ReservationResult(
    ReservationStatus Status,
    Guid? ReservationId,
    DateTimeOffset? ExpiresAt);

public static class BookingStatuses
{
    public const string Reserved = "Reserved";
    public const string Confirming = "Confirming";
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
