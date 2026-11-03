using Microsoft.EntityFrameworkCore;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Features.Payments.GetPayment;

public class GetPaymentHandler(PaymentDbContext dbContext)
{
    public async Task<GetPaymentResponse?> Handle(
        GetPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PaymentId == Guid.Empty)
            throw new ArgumentException("PaymentId must be a non-empty UUID.");

        return await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.Id == request.PaymentId)
            .Select(payment => new GetPaymentResponse(
                payment.Id,
                payment.BookingId,
                payment.Amount,
                payment.Currency,
                payment.Status,
                payment.CreatedAt,
                payment.CompletedAt,
                payment.PaidAt,
                payment.RefundedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GetPaymentResponse?> GetByBooking(Guid bookingId, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.AsNoTracking()
            .SingleOrDefaultAsync(p => p.BookingId == bookingId, cancellationToken);
        return payment is null ? null : new GetPaymentResponse(payment.Id, payment.BookingId,
            payment.Amount, payment.Currency, payment.Status, payment.CreatedAt,
            payment.CompletedAt, payment.PaidAt, payment.RefundedAt);
    }
}
