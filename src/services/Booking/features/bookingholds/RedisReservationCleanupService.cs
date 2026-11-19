namespace Booking.Features.BookingHolds;

public sealed class RedisReservationCleanupService(
    RedisReservationService reservations, ILogger<RedisReservationCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                // Bound each pass; this also finds holds created before a crashed database save.
                for (var batch = 0; batch < 10 && !stoppingToken.IsCancellationRequested; batch++)
                    if (await reservations.CleanupExpiredReservationsAsync() < 100) break;
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Redis reservation cleanup is unavailable; it will be retried.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
