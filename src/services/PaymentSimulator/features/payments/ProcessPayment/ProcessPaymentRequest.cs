using System.Text.Json.Serialization;

namespace PaymentSimulator.Features.Payments.ProcessPayment;

/// <summary>Describes a payment to simulate for a booking.</summary>
public sealed record ProcessPaymentRequest(
    Guid BookingId,
    [property: JsonRequired] decimal Amount,
    string Currency
);
