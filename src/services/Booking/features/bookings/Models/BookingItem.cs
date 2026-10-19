using Booking.Features.EventInventory.Models;

namespace Booking.Features.Bookings.Models;

public class BookingItem
{
    public Guid BookingItemId { get; set; }
    public Guid BookingId { get; set; }
    public Guid InventoryId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public Booking Booking { get; set; } = null!;
    public EventInventory EventInventory { get; set; } = null!;
}
