using Booking.Features.Bookings.Models;

namespace Booking.Features.EventInventory.Models;

public class EventInventory
{
    public Guid InventoryId { get; set; }
    public Guid EventId { get; set; }
    public Guid SectionId { get; set; }
    public int TotalQuantity { get; set; }
    // Persistent stock baseline; temporary reservations are deducted atomically in Redis.
    public int AvailableQuantity { get; set; }
    public decimal Price { get; set; }

    public List<BookingItem> BookingItems { get; set; } = [];
}
