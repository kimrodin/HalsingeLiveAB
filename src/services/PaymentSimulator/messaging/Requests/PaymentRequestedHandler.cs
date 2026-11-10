using HalsingeLive.Contracts.Messaging;
using PaymentSimulator.Features.Payments.ProcessPayment;

namespace PaymentSimulator.Messaging.Requests;

public sealed class PaymentRequestedHandler(ProcessPaymentHandler paymentHandler)
{
    public Task<ProcessPaymentResponse> HandleAsync(PaymentRequestedV1 message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.MessageId == Guid.Empty)
            throw new ArgumentException("MessageId is required.", nameof(message));
        if (message.CorrelationId == Guid.Empty)
            throw new ArgumentException("CorrelationId is required.", nameof(message));
        if (message.OccurredAtUtc == default)
            throw new ArgumentException("OccurredAtUtc is required.", nameof(message));

        var request = new ProcessPaymentRequest(message.BookingId, message.Amount, message.Currency);
        var currency = PaymentValidation.ValidateAndNormalizeCurrency(request);
        if (currency != message.Currency)
            throw new ArgumentException("Currency must be a three-letter uppercase currency code.", nameof(message));

        return paymentHandler.AcceptAsync(request, cancellationToken);
    }
}
