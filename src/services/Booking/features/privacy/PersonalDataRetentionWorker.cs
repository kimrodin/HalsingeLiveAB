namespace Booking.Features.Privacy;

public sealed class PersonalDataRetentionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PersonalDataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = 0;
                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<IPrivacyOperationsService>();
                    var batchCount = await service.PseudonymizeExpiredAsync(stoppingToken);
                    processed += batchCount;
                    if (batchCount < 250)
                        break;
                } while (!stoppingToken.IsCancellationRequested);

                if (processed > 0)
                    logger.LogInformation("Pseudonymized personal data for {BookingCount} expired bookings.", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Personal-data retention processing failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}
