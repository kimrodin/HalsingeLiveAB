using Booking.Data;
using Booking.Features.BookingHolds;
using Microsoft.EntityFrameworkCore;

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
            inventory.TotalQuantity);

        var expiresAt = DateTimeOffset.UtcNow.Add(ReservationDuration);
        var reservationId = await reservations.TryReserveAsync(
            inventory.EventId,
            inventory.SectionId,
            request.Quantity,
            request.CustomerId.Trim(),
            expiresAt);

        return reservationId is null
            ? new ReservationResult(ReservationStatus.InsufficientInventory, null, null)
            : new ReservationResult(
                ReservationStatus.Reserved,
                reservationId,
                expiresAt);
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
