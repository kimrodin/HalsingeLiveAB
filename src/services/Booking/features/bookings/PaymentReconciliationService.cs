using Booking.Data;
using Microsoft.EntityFrameworkCore;

namespace Booking.Features.Bookings;

public sealed class PaymentReconciliationService(
    IServiceScopeFactory scopeFactory,
    ILogger<PaymentReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var ids = await db.Bookings.AsNoTracking()
                    .Where(booking => booking.PaymentRequested && !booking.PaymentSettled)
                    .Select(booking => booking.BookingId).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    try
                    {
                        await using var bookingScope = scopeFactory.CreateAsyncScope();
                        var result = await bookingScope.ServiceProvider.GetRequiredService<BookingService>()
                            .StartPaymentAsync(id, stoppingToken);
                        if (result.PaymentStatus == "Invalid")
                            logger.LogError("Payment contract error for booking {BookingId}: {Reason} Reconciliation remains open.",
                                id, result.Message);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        logger.LogError(exception, "Could not reconcile payment for booking {BookingId}; will retry.", id);
                    }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Could not load pending booking payments; will retry.");
            }
        }
    }
}
