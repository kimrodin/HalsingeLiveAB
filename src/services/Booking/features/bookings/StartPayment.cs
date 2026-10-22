using System.Text.Json;
using Booking.Features.Payments;
using Microsoft.EntityFrameworkCore;
using BookingModel = Booking.Features.Bookings.Models.Booking;

namespace Booking.Features.Bookings;

public sealed partial class BookingService
{
    public async Task<StartPaymentResult> StartPaymentAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        BookingValidation.ValidateId(bookingId, nameof(bookingId));
        // Session advisory lock serializes payment reconciliation across processes without
        // keeping a database transaction open over HTTP. Connection loss releases the lock.
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        var lockId = BitConverter.ToInt64(bookingId.ToByteArray());
        var acquired = false;
        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(@key)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "key";
            parameter.Value = lockId;
            command.Parameters.Add(parameter);
            acquired = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
            if (!acquired)
                return new(bookingId, "Processing", "Pending", "Payment reconciliation is already in progress.");

            return await ProcessBookingPaymentAsync(bookingId, cancellationToken);
        }
        finally
        {
            try
            {
                if (acquired)
                {
                    await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                    command.CommandText = "SELECT pg_advisory_unlock(@key)";
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = "key";
                    parameter.Value = lockId;
                    command.Parameters.Add(parameter);
                    await command.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<StartPaymentResult> ProcessBookingPaymentAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await GetBookingAsync(bookingId, cancellationToken);
        if (booking is null)
            return new(bookingId, "NotFound", "NotStarted", "Booking not found.");
        if (booking.Status == BookingStatuses.Confirmed)
        {
            if (booking.PaymentRequested && !booking.PaymentSettled)
                await SetPaymentSettledAsync(bookingId, cancellationToken);
            return new(bookingId, booking.Status, "Succeeded", "The paid booking is already confirmed.");
        }
        if (!booking.PaymentRequested)
        {
            if (!IsPayable(booking))
            {
                await CancelAsync(bookingId, cancellationToken);
                return await PaymentResponseAsync(bookingId, "NotStarted", "The reservation is not valid for payment.", cancellationToken);
            }
            // Persist intent before HTTP: even a lost first response will be reconciled.
            await dbContext.Bookings.Where(row => row.BookingId == bookingId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.PaymentRequested, true), cancellationToken);
        }

        try
        {
            // Always query an existing attempt before considering POST. POST uses the same
            // idempotency key if a previously sent request is still in flight.
            var payment = await payments.GetByBookingAsync(bookingId, cancellationToken);
            booking = await GetBookingAsync(bookingId, cancellationToken);
            if (payment is null)
            {
                payment = IsPayable(booking)
                    ? await payments.ProcessAsync(bookingId, booking.TotalPrice, "SEK", cancellationToken)
                    // Fence a delayed original POST without charging an expired reservation.
                    : await payments.CancelAsync(bookingId, booking.TotalPrice, "SEK", cancellationToken);
            }

            if (!Matches(payment, booking))
                return new(bookingId, booking.Status, "Invalid", "Payment identity, amount or currency does not match the booking. Reconciliation remains open.");

            if (payment.Status == "Succeeded" && payment.RefundedAt is null)
            {
                BookingActionResult confirmed;
                try
                {
                    confirmed = await ConfirmAsync(bookingId, payment, cancellationToken);
                }
                catch (ReservationConfirmationException)
                {
                    confirmed = new(BookingActionStatus.InvalidState, booking.Status);
                }
                if (confirmed.Status == BookingActionStatus.Completed)
                    return new(bookingId, BookingStatuses.Confirmed, "Succeeded", "Payment verified and booking confirmed.");

                // A late success or unavailable stock is explicitly refunded, never ignored.
                var refund = await payments.RefundAsync(payment.PaymentId, cancellationToken);
                if (!Matches(refund, booking) || refund.RefundedAt is null)
                    return new(bookingId, booking.Status, "Unknown", "Refund could not be verified; reconciliation will retry.");
                await CancelAsync(bookingId, cancellationToken);
                await SetPaymentSettledAsync(bookingId, cancellationToken);
                return await PaymentResponseAsync(bookingId, "Refunded", "Payment refunded because the booking could not be confirmed.", cancellationToken);
            }

            if (payment.Status is "Failed" or "TimedOut" || payment.RefundedAt != null)
            {
                await CancelAsync(bookingId, cancellationToken);
                await SetPaymentSettledAsync(bookingId, cancellationToken);
                return await PaymentResponseAsync(bookingId, payment.RefundedAt != null ? "Refunded" : payment.Status,
                    "Payment did not complete; the reservation has been released.", cancellationToken);
            }

            if (payment.Status != "Pending")
                return new(bookingId, booking.Status, "Invalid", "Unrecognized payment status; reconciliation remains open.");

            if (!IsPayable(booking))
            {
                // Cancelling Pending establishes a durable fence against later success.
                var cancelled = await payments.RefundAsync(payment.PaymentId, cancellationToken);
                if (!Matches(cancelled, booking) || cancelled.RefundedAt is null)
                    return new(bookingId, booking.Status, "Unknown", "Cancellation is not yet verified.");
                await CancelAsync(bookingId, cancellationToken);
                await SetPaymentSettledAsync(bookingId, cancellationToken);
                return await PaymentResponseAsync(bookingId, "Refunded", "Expired payment attempt cancelled.", cancellationToken);
            }
            return new(bookingId, booking.Status, "Pending", "Payment is pending. Status will be checked automatically; the reservation deadline is unchanged.");
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            // Transport errors never mean PaymentFailed. The durable intent remains open.
            if (cancellationToken.IsCancellationRequested)
                throw;
            return await PaymentResponseAsync(bookingId, "Unknown", "Payment outcome is unknown. Status will be checked automatically.", cancellationToken);
        }
    }

    private static bool IsPayable(BookingModel booking) =>
        booking.Status == BookingStatuses.Reserved && booking.ExpiresAt > DateTime.UtcNow;

    private static bool Matches(PaymentResult payment, BookingModel booking) =>
        payment.PaymentId != Guid.Empty && payment.BookingId == booking.BookingId
        && payment.Amount == booking.TotalPrice && payment.Currency == "SEK";

    private Task<int> SetPaymentSettledAsync(Guid bookingId, CancellationToken cancellationToken) =>
        dbContext.Bookings.Where(row => row.BookingId == bookingId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.PaymentSettled, true), cancellationToken);

    private async Task<StartPaymentResult> PaymentResponseAsync(Guid bookingId, string paymentStatus, string message, CancellationToken cancellationToken)
    {
        var booking = await GetBookingAsync(bookingId, cancellationToken);
        return new(bookingId, booking.Status, paymentStatus, message);
    }
}

public sealed record StartPaymentResult(Guid BookingId, string BookingStatus, string PaymentStatus, string Message);
