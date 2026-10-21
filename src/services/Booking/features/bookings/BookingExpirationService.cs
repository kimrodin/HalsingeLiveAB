using Booking.Data;
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
                var now = DateTime.UtcNow;
                var expiredCount = await dbContext.Bookings
                    .Where(booking => booking.Status == BookingStatuses.Reserved
                        && booking.ExpiresAt != null
                        && booking.ExpiresAt <= now)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(booking => booking.Status, BookingStatuses.Expired), stoppingToken);

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
