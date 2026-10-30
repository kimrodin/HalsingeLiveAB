namespace Booking.Features.Bookings.Models;

public class Booking
{
    public Guid BookingId { get; set; }
    // Retained as an optional future account link; guest bookings leave it null.
    public string CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    // Written before sending HTTP so reconciliation survives lost responses and restarts.
    public bool PaymentRequested { get; set; }
    public bool PaymentSettled { get; set; }
    public byte[] GuestAccessTokenHash { get; set; }

    public List<BookingItem> Items { get; set; } = [];
}
