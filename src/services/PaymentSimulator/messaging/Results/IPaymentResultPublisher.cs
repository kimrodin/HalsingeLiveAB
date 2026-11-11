using HalsingeLive.Contracts.Messaging;

namespace PaymentSimulator.Messaging.Results;

public interface IPaymentResultPublisher
{
    Task PublishAsync(PaymentSucceeded message, CancellationToken cancellationToken = default);
    Task PublishAsync(PaymentFailed message, CancellationToken cancellationToken = default);
}
