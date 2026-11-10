using HalsingeLive.Contracts.Messaging;

namespace Booking.Features.Payments.Requests;

public interface IPaymentRequestedPublisher
{
    Task PublishAsync(PaymentRequestedV1 message, CancellationToken cancellationToken = default);
}
