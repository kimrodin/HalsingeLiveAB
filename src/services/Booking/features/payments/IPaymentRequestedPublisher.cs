using HalsingeLive.Contracts.Messaging;

namespace Booking.Features.Payments;

public interface IPaymentRequestedPublisher
{
    Task PublishAsync(PaymentRequestedV1 message, CancellationToken cancellationToken = default);
}
