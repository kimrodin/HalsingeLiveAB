using Booking.Data;
using Booking.Features.Bookings;
using Booking.Features.Bookings.Models;
using Microsoft.EntityFrameworkCore;

namespace Booking.UnitTests;

public sealed class BookingListTests
{
    [Fact]
    public async Task ListBookings_FiltersSearchAndReturnsSortedPage()
    {
        await using var db = CreateDbContext();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstBooking = CreateBooking(firstId, "Sara Andersson", "sara@example.com", BookingStatuses.Reserved,
            new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc));
        var inventoryId = Guid.NewGuid();
        firstBooking.Items.Add(new BookingItem
        {
            BookingItemId = Guid.NewGuid(),
            InventoryId = inventoryId,
            EventName = "Summer Festival",
            EventInventory = new Booking.Features.EventInventory.Models.EventInventory
            {
                InventoryId = inventoryId,
                EventId = Guid.NewGuid(),
                SectionId = Guid.NewGuid()
            }
        });
        db.Bookings.AddRange(
            firstBooking,
            CreateBooking(secondId, "Other Customer", "sara.alt@example.com", BookingStatuses.Reserved,
                new DateTime(2026, 10, 21, 10, 0, 0, DateTimeKind.Utc)),
            CreateBooking(Guid.NewGuid(), "Unrelated Customer", "other@example.com", BookingStatuses.Confirmed,
                new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var service = new BookingService(db, null!, null!, null!);
        var request = new ListBookingsRequest(
            Search: "sara",
            Status: BookingStatuses.Reserved,
            SortBy: "createdAt",
            SortDirection: "asc",
            Page: 2,
            PageSize: 1);

        var result = await service.ListBookingsAsync(request);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(secondId, Assert.Single(result.Bookings).BookingId);
        Assert.Equal(
            new[] { BookingStatuses.Confirmed, BookingStatuses.Reserved },
            result.Statuses);

        var eventResult = await service.ListBookingsAsync(
            new ListBookingsRequest(Search: "summer"));

        Assert.Equal(firstId, Assert.Single(eventResult.Bookings).BookingId);
    }

    [Fact]
    public async Task UpdateBookingCustomer_TrimsAndSavesCustomerDetails()
    {
        await using var db = CreateDbContext();
        var bookingId = Guid.NewGuid();
        db.Bookings.Add(CreateBooking(
            bookingId, "Old Name", "old@example.com", BookingStatuses.Reserved, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var service = new BookingService(db, null!, null!, null!);
        var updated = await service.UpdateBookingCustomerAsync(
            bookingId, " New Name ", " new@example.com ");
        var booking = await db.Bookings.SingleAsync();

        Assert.True(updated);
        Assert.Equal("New Name", booking.CustomerName);
        Assert.Equal("new@example.com", booking.CustomerEmail);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BookingDbContext(options);
    }

    private static Booking.Features.Bookings.Models.Booking CreateBooking(
        Guid bookingId,
        string customerName,
        string customerEmail,
        string status,
        DateTime createdAt) =>
        new()
        {
            BookingId = bookingId,
            CustomerId = "",
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            Status = status,
            TotalPrice = 100,
            CreatedAt = createdAt,
            GuestAccessTokenHash = []
        };
}
