namespace Booking.Features.Payments.Requests;

public sealed class PaymentRequestOutboxMessage
{
    public Guid MessageId { get; set; }
    public Guid BookingId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SEK";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? SuppressedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string LastError { get; set; }
}
