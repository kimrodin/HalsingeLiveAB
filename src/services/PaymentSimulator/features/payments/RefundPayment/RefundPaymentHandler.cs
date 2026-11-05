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
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            WITH cancelled AS (
                UPDATE "Payments"
                SET "RefundedAt" = {now}, "CompletedAt" = COALESCE("CompletedAt", {now}),
                    "Status" = CASE WHEN "Status" = 'Pending' THEN 'TimedOut' ELSE "Status" END
                WHERE "Id" = {paymentId} AND "RefundedAt" IS NULL AND "Status" IN ('Pending', 'Succeeded')
                RETURNING *
            )
            INSERT INTO "PaymentResultOutboxMessages"
                ("MessageId", "PaymentId", "BookingId", "Amount", "Currency", "Outcome",
                 "PaidAtUtc", "OccurredAtUtc", "NextAttemptAtUtc", "AttemptCount")
            SELECT gen_random_uuid(), "Id", "BookingId", "Amount", "Currency", "Status",
                   "PaidAt", "CompletedAt", "CompletedAt", 0 FROM cancelled WHERE "Status" = 'TimedOut'
            ON CONFLICT ("PaymentId") DO NOTHING
            """, cancellationToken);
        var payment = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        return payment is null ? null : ProcessPaymentHandler.ToResponse(payment);
    }
}
