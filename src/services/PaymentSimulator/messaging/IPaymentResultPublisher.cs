using HalsingeLive.Contracts.Messaging;

namespace PaymentSimulator.Messaging;

public interface IPaymentResultPublisher
{
    Task PublishAsync(PaymentSucceededV1 message, CancellationToken cancellationToken = default);
    Task PublishAsync(PaymentFailedV1 message, CancellationToken cancellationToken = default);
}
