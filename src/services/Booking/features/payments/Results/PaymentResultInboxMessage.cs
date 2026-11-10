namespace Booking.Features.Payments.Results;

public sealed class PaymentResultInboxMessage
{
    public Guid MessageId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public DateTimeOffset? RejectedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string LastError { get; set; }
}
