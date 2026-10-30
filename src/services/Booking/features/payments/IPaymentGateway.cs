namespace Booking.Features.Payments;

// Transport boundary: a future message adapter can replace the HTTP implementation.
public interface IPaymentGateway
{
    Task<PaymentResult> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken);
    Task<PaymentResult> ProcessAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken);
    Task<PaymentResult> RefundAsync(Guid paymentId, CancellationToken cancellationToken);
    Task<PaymentResult> CancelAsync(Guid bookingId, decimal amount, string currency, CancellationToken cancellationToken);
}

public sealed record PaymentResult(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? RefundedAt);
