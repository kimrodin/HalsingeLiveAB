using Microsoft.AspNetCore.Mvc;
using TicketService.Api;
using static TicketService.UnitTests.TicketTestData;

namespace TicketService.UnitTests;

public sealed class TicketLookupEndpointTests
{
    [Fact]
    public async Task GetTicketsByBooking_FiltersBookingAndOrdersByItemThenTicketNumber()
    {
        await using var db = CreateDbContext();
        var bookingId = Guid.NewGuid();
        var firstItem = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondItem = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var expected = new[] { CreateTicket(), CreateTicket(), CreateTicket(), CreateTicket() };
        for (var index = 0; index < expected.Length; index++)
        {
            expected[index].BookingId = bookingId;
            expected[index].BookingItemId = index < 2 ? firstItem : secondItem;
            expected[index].TicketNumberInBookingItem = index % 2 + 1;
        }
        db.Tickets.AddRange(expected.Reverse());
        db.Tickets.Add(CreateTicket());
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTicketsByBooking(bookingId, "test-key", default);

        var response = Assert.IsType<OkObjectResult>(result);
        var tickets = Assert.IsAssignableFrom<IReadOnlyList<TicketResponse>>(response.Value);
        Assert.Equal(expected.Select(t => t.TicketId), tickets.Select(t => t.TicketId));
        Assert.All(tickets, ticket => Assert.Equal(bookingId, ticket.BookingId));
    }

    [Fact]
    public async Task GetTicketsByBooking_ReturnsEmptyListForUnknownBooking()
    {
        await using var db = CreateDbContext();
        db.Tickets.Add(CreateTicket());
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTicketsByBooking(Guid.NewGuid(), "test-key", default);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<TicketResponse>>(response.Value));
    }
}
