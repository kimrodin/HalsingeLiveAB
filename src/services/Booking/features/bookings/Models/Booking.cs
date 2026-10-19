namespace Booking.Features.Bookings.Models;

public class Booking
{
    public Guid BookingId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public List<BookingItem> Items { get; set; } = [];
}
