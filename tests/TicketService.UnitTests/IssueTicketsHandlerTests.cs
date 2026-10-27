using Microsoft.EntityFrameworkCore;
using TicketService.Data;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Features.Tickets.Models;

namespace TicketService.UnitTests;

public sealed class IssueTicketsHandlerTests
{
    [Fact]
    public async Task HandleAsync_IssuesOneTicketPerQuantityWithSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var handler = new IssueTicketsHandler(dbContext, TimeProvider.System);

        var result = await handler.HandleAsync(CreateMessage(quantity: 2));

        Assert.False(result.AlreadyIssued);
        Assert.Equal(2, result.Tickets.Length);
        Assert.Equal(new[] { 1, 2 }, result.Tickets.Select(ticket => ticket.TicketNumberInBookingItem));
        Assert.All(result.Tickets, ticket =>
        {
            Assert.Equal(TicketStatus.Valid, ticket.Status);
            Assert.Equal("Live event", ticket.EventName);
            Assert.Equal("Main Hall", ticket.VenueName);
            Assert.Equal("Standing", ticket.SectionName);
            Assert.Equal(249.50m, ticket.UnitPrice);
            Assert.Equal("SEK", ticket.Currency);
        });
    }

    [Fact]
    public async Task HandleAsync_RepeatedConfirmationReturnsSameTickets()
    {
        await using var dbContext = CreateDbContext();
        var handler = new IssueTicketsHandler(dbContext, TimeProvider.System);
        var message = CreateMessage(quantity: 2);

        var firstResult = await handler.HandleAsync(message);
        var retryResult = await handler.HandleAsync(message);

        Assert.False(firstResult.AlreadyIssued);
        Assert.True(retryResult.AlreadyIssued);
        Assert.Equal(
            firstResult.Tickets.Select(ticket => ticket.TicketId),
            retryResult.Tickets.Select(ticket => ticket.TicketId));
        Assert.Equal(2, await dbContext.Tickets.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_RejectsConfirmationWithChangedSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var handler = new IssueTicketsHandler(dbContext, TimeProvider.System);
        var message = CreateMessage(quantity: 1);
        await handler.HandleAsync(message);

        var changedMessage = message with { EventName = "Renamed event" };

        await Assert.ThrowsAsync<TicketIssueConflictException>(
            () => handler.HandleAsync(changedMessage));
    }

    [Fact]
    public async Task HandleAsync_RejectsQuantityOutsideBookingLimit()
    {
        await using var dbContext = CreateDbContext();
        var handler = new IssueTicketsHandler(dbContext, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<TicketIssueValidationException>(
            () => handler.HandleAsync(CreateMessage(quantity: 11)));

        Assert.Contains(exception.Errors, error => error.Contains("Quantity"));
    }

    private static TicketDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TicketDbContext(options);
    }

    private static BookingConfirmedMessage CreateMessage(int quantity) => new()
    {
        BookingId = Guid.NewGuid(),
        BookingItemId = Guid.NewGuid(),
        EventId = Guid.NewGuid(),
        EventName = "Live event",
        EventStartsAtUtc = DateTimeOffset.Parse("2026-11-01T19:00:00Z"),
        VenueName = "Main Hall",
        SectionName = "Standing",
        Quantity = quantity,
        UnitPrice = 249.50m,
        Currency = "SEK"
    };
}
