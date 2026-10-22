namespace PaymentSimulator.Features.Payments.ProcessPayment;

public sealed class PaymentRecoveryWorker(
    IServiceScopeFactory scopeFactory, ILogger<PaymentRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ProcessPaymentHandler>()
                    .CompleteDuePayments(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Payment recovery failed; retrying."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
