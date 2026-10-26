using PaymentSimulator.Payments.Models;

namespace PaymentSimulator.Messaging;
public class PaymentConsumer :
    IConsumer<PaymentRequested>
{
    private readonly ProcessPaymentHandler _handler;

    public PaymentConsumer(ProcessPaymentHandler handler)
    {
        _handler = handler;
    }

    public async Task Consume(
        ConsumeContext<PaymentRequested> context)
    {
        // Anropa betalningslogiken
        // och publicera betalningsresultatet
    }
}