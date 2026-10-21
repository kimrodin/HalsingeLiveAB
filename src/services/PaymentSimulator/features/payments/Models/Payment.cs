namespace PaymentSimulator.Payments.Models;
public class Payment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SEK";

    public PaymentStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}