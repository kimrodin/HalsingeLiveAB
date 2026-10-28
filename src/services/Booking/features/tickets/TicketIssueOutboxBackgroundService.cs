namespace Booking.Features.Tickets;

public sealed class TicketIssueOutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<TicketIssueOutboxBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<TicketIssueOutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Could not dispatch ticket issuance messages; will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
