using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.ProcessPayment;

namespace PaymentSimulator.Features.Payments.RefundPayment;

public sealed class RefundPaymentHandler(PaymentDbContext dbContext)
{
    public async Task<ProcessPaymentResponse?> Handle(Guid paymentId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        // A refund racing completion either refunds Succeeded or fences Pending. The original
        // outcome of a completed payment is immutable; RefundedAt records compensation separately.
        await dbContext.Payments.Where(p => p.Id == paymentId && p.RefundedAt == null
            && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Succeeded))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.RefundedAt, now)
                .SetProperty(p => p.CompletedAt, p => p.CompletedAt ?? now)
                .SetProperty(p => p.Status, p => p.Status == PaymentStatus.Pending
                    ? PaymentStatus.TimedOut : p.Status), cancellationToken);
        var payment = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        return payment is null ? null : ProcessPaymentHandler.ToResponse(payment);
    }
}
