using PaymentSimulator.Features.Payments.Models;

namespace PaymentSimulator.Messaging.Results;

public sealed class PaymentResultOutboxMessage
{
    public Guid MessageId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    public static PaymentResultOutboxMessage FromPayment(Payment payment) => new()
    {
        MessageId = Guid.NewGuid(), PaymentId = payment.Id, BookingId = payment.BookingId,
        Amount = payment.Amount, Currency = payment.Currency, Outcome = payment.Status.ToString(),
        PaidAtUtc = payment.PaidAt, OccurredAtUtc = payment.CompletedAt!.Value,
        NextAttemptAtUtc = payment.CompletedAt.Value
    };
}
