using Booking.Data;
using Booking.Features.BookingHolds;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public sealed class BookingExpirationService(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var reservations = scope.ServiceProvider.GetRequiredService<RedisReservationService>();
                var now = DateTime.UtcNow;
                var candidates = await dbContext.Bookings
                    .Where(booking => (booking.Status == BookingStatuses.Reserved
                            && booking.ExpiresAt != null && booking.ExpiresAt <= now)
                        || booking.Status == BookingStatuses.Expiring)
                    .Include(booking => booking.Items)
                    .ThenInclude(item => item.EventInventory)
                    .ToListAsync(stoppingToken);

                var expiredCount = 0;
                foreach (var booking in candidates)
                {
                    if (booking.Status == BookingStatuses.Reserved)
                    {
                        var claimed = await dbContext.Bookings
                            .Where(item => item.BookingId == booking.BookingId
                                && item.Status == BookingStatuses.Reserved
                                && item.ExpiresAt <= now)
                            .ExecuteUpdateAsync(update => update
                                .SetProperty(item => item.Status, BookingStatuses.Expiring), stoppingToken);
                        if (claimed == 0)
                            continue;
                    }

                    foreach (var item in booking.Items)
                    {
                        // Recover a Redis confirmation left behind by a rolled-back/crashed transaction.
                        await reservations.RestoreConfirmedReservationAsync(
                            item.EventInventory.EventId, item.EventInventory.SectionId, booking.BookingId);
                        await reservations.ReleaseReservationAsync(
                            item.EventInventory.EventId,
                            item.EventInventory.SectionId,
                            booking.BookingId);
                    }

                    expiredCount += await dbContext.Bookings
                        .Where(item => item.BookingId == booking.BookingId
                            && item.Status == BookingStatuses.Expiring)
                        .ExecuteUpdateAsync(update => update
                            .SetProperty(item => item.Status, BookingStatuses.Expired), stoppingToken);
                }

                if (expiredCount > 0)
                    logger.LogInformation("Marked {BookingCount} booking reservations as expired.", expiredCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to expire Booking reservations.");
            }
        }
    }
}
