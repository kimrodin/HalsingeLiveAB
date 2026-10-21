namespace PaymentSimulator.Features.Payments.ProcessPayment;

/// <summary>Describes a payment to simulate for a booking.</summary>
public sealed record ProcessPaymentRequest(
    Guid BookingId,
    decimal Amount,
    string Currency
);