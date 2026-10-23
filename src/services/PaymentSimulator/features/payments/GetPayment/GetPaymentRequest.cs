namespace PaymentSimulator.Features.Payments.GetPayment;

/// <summary>Identifies the payment to retrieve.</summary>
public sealed record GetPaymentRequest(Guid PaymentId);