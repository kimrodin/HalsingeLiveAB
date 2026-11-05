using Booking.Features.Payments;

namespace Booking.Features.Bookings;

public sealed partial class BookingService
{
    public Task<StartPaymentResult> ApplyPaymentResultAsync(PaymentResultInboxMessage message, CancellationToken token = default) =>
        WithPaymentLockAsync(message.BookingId, async () =>
        {
            var booking = await GetBookingAsync(message.BookingId, token);
            if (booking is null || !booking.PaymentRequested)
                throw new InvalidDataException("Payment result does not belong to a requested booking payment.");
            if (message.Amount != booking.TotalPrice || message.Currency != "SEK")
                throw new InvalidDataException("Payment result amount or currency does not match the booking.");

            // A success event is historical. Read the current attempt to verify its identity
            // and detect refunds that happened while the event was waiting for delivery.
            var payment = await payments.GetByBookingAsync(message.BookingId, token)
                ?? throw new HttpRequestException("Payment lookup is not yet available; reconciliation will retry.");
            if (!Matches(payment, booking) || payment.PaymentId != message.PaymentId)
                throw new InvalidDataException("Payment result identity does not match the current payment attempt.");
            if (payment.RefundedAt is null && payment.Status != message.Outcome)
                throw new InvalidDataException("Payment result conflicts with the current terminal payment outcome.");
            if (message.Outcome == "Succeeded" && (payment.PaidAt is null || message.PaidAtUtc is null
                || payment.PaidAt.Value.UtcTicks / 10 != message.PaidAtUtc.Value.UtcTicks / 10))
                throw new InvalidDataException("Payment result PaidAtUtc does not match the recorded payment.");
            if (booking.Status == BookingStatuses.Confirmed && (message.Outcome != "Succeeded" || payment.RefundedAt is not null))
                throw new InvalidDataException("A conflicting payment result cannot change a confirmed booking.");

            return await ApplyVerifiedPaymentAsync(booking, payment, token);
        }, token);
}
