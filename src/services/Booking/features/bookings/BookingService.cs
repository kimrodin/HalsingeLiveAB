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
        BookingValidation.ValidateCreateBooking(request);
        var customerName = request.CustomerName.Trim();
        var customerEmail = request.CustomerEmail.Trim();

        var inventory = await dbContext.EventInventory
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.InventoryId == request.InventoryId, cancellationToken);

        if (inventory is null)
            return new ReservationResult(ReservationStatus.InventoryNotFound, null, null);

        BookingValidation.ValidatePriceAndTotal(inventory.Price, request.Quantity);

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
            expiresAt);

        if (!reserved)
            return new ReservationResult(ReservationStatus.InsufficientInventory, null, null);

        var booking = new BookingModel
        {
            BookingId = bookingId,
            CustomerName = customerName,
            CustomerEmail = customerEmail,
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

    public async Task<BookingModel> GetBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookings.AsNoTracking()
            .Include(booking => booking.Items)
            .FirstOrDefaultAsync(booking => booking.BookingId == bookingId, cancellationToken);
    }

    public Task<List<BookingModel>> ListBookingsAsync(CancellationToken cancellationToken = default) =>
        dbContext.Bookings.AsNoTracking()
            .Include(booking => booking.Items)
            .OrderByDescending(booking => booking.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<BookingActionResult> ConfirmAsync(
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
        if (booking.Status == BookingStatuses.Confirmed)
            return new BookingActionResult(BookingActionStatus.Completed, booking.Status);
        if (booking.Status is BookingStatuses.Cancelled or BookingStatuses.Expired)
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
                    .SetProperty(item => item.Status, BookingStatuses.Confirming), cancellationToken);
            if (started == 0)
            {
                var currentStatus = await dbContext.Bookings.AsNoTracking()
                    .Where(item => item.BookingId == bookingId)
                    .Select(item => item.Status)
                    .SingleAsync(cancellationToken);
                if (currentStatus == BookingStatuses.Confirmed)
                    return new BookingActionResult(BookingActionStatus.Completed, currentStatus);
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
        else
        {
            // Another request already owns this confirmation attempt.
            return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var finalizedItems = new List<BookingItem>();
        try
        {
            foreach (var item in booking.Items)
            {
                // This conditional write is the final stock authority. PostgreSQL serializes
                // competing updates to this row and will reject an update that would go negative.
                var stockUpdated = await dbContext.EventInventory
                    .Where(inventory => inventory.InventoryId == item.InventoryId
                        && inventory.AvailableQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(inventory => inventory.AvailableQuantity,
                            inventory => inventory.AvailableQuantity - item.Quantity), cancellationToken);
                if (stockUpdated == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    await reservations.ReleaseReservationAsync(item.EventInventory.EventId,
                        item.EventInventory.SectionId, booking.BookingId);
                    await dbContext.Bookings
                        .Where(row => row.BookingId == bookingId && row.Status == BookingStatuses.Confirming)
                        .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, BookingStatuses.Failed), cancellationToken);
                    return new BookingActionResult(BookingActionStatus.InvalidState, BookingStatuses.Failed);
                }

                var result = await reservations.ConfirmReservationAsync(
                    item.EventInventory.EventId,
                    item.EventInventory.SectionId,
                    booking.BookingId);

                if (result != ReservationFinalizationResult.Completed)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    var expired = result == ReservationFinalizationResult.Expired;
                    await dbContext.Bookings
                        .Where(row => row.BookingId == bookingId && row.Status == BookingStatuses.Confirming)
                        .ExecuteUpdateAsync(update => update
                            .SetProperty(row => row.Status, expired ? BookingStatuses.Expired : BookingStatuses.Failed), cancellationToken);
                    return new BookingActionResult(
                        expired ? BookingActionStatus.Expired : BookingActionStatus.ReservationNotFound,
                        expired ? BookingStatuses.Expired : BookingStatuses.Failed);
                }

                finalizedItems.Add(item);
            }

            var transitioned = await dbContext.Bookings
                .Where(row => row.BookingId == bookingId && row.Status == BookingStatuses.Confirming)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(row => row.Status, BookingStatuses.Confirmed), cancellationToken);
            if (transitioned != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                foreach (var item in finalizedItems)
                    await reservations.RestoreConfirmedReservationAsync(item.EventInventory.EventId,
                        item.EventInventory.SectionId, booking.BookingId);
                return new BookingActionResult(BookingActionStatus.InvalidState, booking.Status);
            }

            await transaction.CommitAsync(cancellationToken);
            return new BookingActionResult(BookingActionStatus.Completed, BookingStatuses.Confirmed);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var item in booking.Items)
                await reservations.RestoreConfirmedReservationAsync(item.EventInventory.EventId,
                    item.EventInventory.SectionId, booking.BookingId);
            await dbContext.Bookings
                .Where(row => row.BookingId == bookingId && row.Status == BookingStatuses.Confirming)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, BookingStatuses.Failed),
                    CancellationToken.None);
            throw;
        }
    }

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
            await reservations.ReleaseReservationAsync(item.EventInventory.EventId,
                item.EventInventory.SectionId, booking.BookingId);

        await dbContext.Bookings
            .Where(row => row.BookingId == booking.BookingId && row.Status == BookingStatuses.Expiring)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, BookingStatuses.Expired), cancellationToken);
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
    Guid? BookingId,
    DateTimeOffset? ExpiresAt);

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
