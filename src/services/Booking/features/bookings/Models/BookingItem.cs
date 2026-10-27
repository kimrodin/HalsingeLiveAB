using EventInventoryModel = Booking.Features.EventInventory.Models.EventInventory;

namespace Booking.Features.Bookings.Models;

public class BookingItem
{
    public Guid BookingItemId { get; set; }
    public Guid BookingId { get; set; }
    public Guid InventoryId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "SEK";

    public string EventName { get; set; } = string.Empty;
    public DateTimeOffset EventStartsAt { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;

    public Booking Booking { get; set; } = null!;
    public EventInventoryModel EventInventory { get; set; } = null!;
}
