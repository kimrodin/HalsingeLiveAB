namespace PaymentSimulator.Features.Payments.Models;

public class Payment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SEK";

    public PaymentStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CompleteAfter { get; set; }
    public PaymentStatus PlannedOutcome { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
}
