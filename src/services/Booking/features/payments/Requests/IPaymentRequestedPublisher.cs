using HalsingeLive.Contracts.Messaging;

namespace Booking.Features.Payments.Requests;

public interface IPaymentRequestedPublisher
{
    Task PublishAsync(PaymentRequested message, CancellationToken cancellationToken = default);
}
