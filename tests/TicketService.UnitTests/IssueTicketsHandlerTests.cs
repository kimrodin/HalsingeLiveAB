using Microsoft.EntityFrameworkCore;
using HalsingeLive.Contracts.Messaging;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Features.Tickets.Models;
using static TicketService.UnitTests.TicketTestData;

namespace TicketService.UnitTests;

public sealed class IssueTicketsHandlerTests
{
    [Fact]
    public async Task HandleAsync_IssuesOneTicketPerQuantityWithSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var handler = new IssueTicketsHandler(dbContext, TicketTestData.Clock);
        var message = CreateMessage(quantity: 2);

        var result = await handler.HandleAsync(message);
        dbContext.ChangeTracker.Clear();
        var saved = await dbContext.Tickets.OrderBy(ticket => ticket.TicketNumberInBookingItem).ToArrayAsync();

        Assert.False(result.AlreadyIssued);
        Assert.Equal(2, result.Tickets.Length);
        Assert.Equal(result.Tickets.Select(ticket => ticket.TicketId), saved.Select(ticket => ticket.TicketId));
        Assert.Equal(2, saved.Select(ticket => ticket.TicketId).Distinct().Count());
        Assert.Equal(new[] { 1, 2 }, result.Tickets.Select(ticket => ticket.TicketNumberInBookingItem));
        Assert.All(saved, ticket =>
        {
            Assert.NotEqual(Guid.Empty, ticket.TicketId);
            Assert.Equal(message.BookingId, ticket.BookingId);
            Assert.Equal(message.BookingItemId, ticket.BookingItemId);
            Assert.Equal(message.EventId, ticket.EventId);
            Assert.Equal(message.EventStartsAtUtc, ticket.EventStartsAt);
            Assert.Equal(TicketTestData.Now, ticket.IssuedAt);
            Assert.Null(ticket.CheckedInAt);
            Assert.Null(ticket.CancelledAt);
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

    [Theory]
    [InlineData("BookingId")]
    [InlineData("EventId")]
    [InlineData("EventName")]
    [InlineData("EventStartsAtUtc")]
    [InlineData("VenueName")]
    [InlineData("SectionName")]
    [InlineData("Quantity")]
    [InlineData("UnitPrice")]
    [InlineData("Currency")]
    public async Task HandleAsync_RejectsConfirmationWithChangedSnapshot(string field)
    {
        await using var dbContext = CreateDbContext();
        var handler = new IssueTicketsHandler(dbContext, TimeProvider.System);
        var message = CreateMessage(quantity: 1);
        await handler.HandleAsync(message);

        var changedMessage = field switch
        {
            "BookingId" => message with { BookingId = Guid.NewGuid() },
            "EventId" => message with { EventId = Guid.NewGuid() },
            "EventName" => message with { EventName = "Renamed event" },
            "EventStartsAtUtc" => message with { EventStartsAtUtc = message.EventStartsAtUtc.AddHours(1) },
            "VenueName" => message with { VenueName = "Other venue" },
            "SectionName" => message with { SectionName = "Other section" },
            "Quantity" => message with { Quantity = 2 },
            "UnitPrice" => message with { UnitPrice = 100m },
            "Currency" => message with { Currency = "EUR" },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        await Assert.ThrowsAsync<TicketIssueConflictException>(
            () => handler.HandleAsync(changedMessage));
        dbContext.ChangeTracker.Clear();
        var saved = await dbContext.Tickets.SingleAsync();
        Assert.Equal(message.EventName, saved.EventName);
        Assert.Equal(message.UnitPrice, saved.UnitPrice);
        Assert.Equal(message.BookingId, saved.BookingId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(11)]
    public async Task HandleAsync_RejectsQuantityOutsideBookingLimit(int quantity)
    {
        await using var dbContext = CreateDbContext();
        var handler = new IssueTicketsHandler(dbContext, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<TicketIssueValidationException>(
            () => handler.HandleAsync(CreateMessage(quantity)));

        Assert.Contains(exception.Errors, error => error.Contains("Quantity"));
        Assert.Empty(await dbContext.Tickets.ToArrayAsync());
    }

    public static IEnumerable<object[]> InvalidMessages()
    {
        var message = CreateMessage(1);
        yield return [message with { BookingId = Guid.Empty }, "BookingId"];
        yield return [message with { BookingItemId = Guid.Empty }, "BookingItemId"];
        yield return [message with { EventId = Guid.Empty }, "EventId"];
        yield return [message with { EventStartsAtUtc = default }, "EventStartsAtUtc"];
        foreach (var name in new string?[] { null, "", "   ", new('x', 201) })
        {
            yield return [message with { EventName = name! }, "EventName"];
            yield return [message with { VenueName = name! }, "VenueName"];
        }
        foreach (var name in new string?[] { null, "", "   ", new('x', 101) })
            yield return [message with { SectionName = name! }, "SectionName"];
        foreach (var price in new[] { -0.01m, 1.001m, 10000000000m })
            yield return [message with { UnitPrice = price }, "UnitPrice"];
        foreach (var currency in new string?[] { null, "", "SE", "SEKK", "sek", "S3K", "ÅÄÖ" })
            yield return [message with { Currency = currency! }, "Currency"];
    }

    [Theory]
    [MemberData(nameof(InvalidMessages))]
    public async Task HandleAsync_RejectsInvalidSnapshotWithoutSaving(BookingConfirmedV1 message, string field)
    {
        await using var db = CreateDbContext();
        var exception = await Assert.ThrowsAsync<TicketIssueValidationException>(
            () => new IssueTicketsHandler(db, TicketTestData.Clock).HandleAsync(message));
        Assert.Contains(exception.Errors, error => error.StartsWith(field, StringComparison.Ordinal));
        Assert.Empty(await db.Tickets.ToArrayAsync());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task HandleAsync_RejectsNullConfirmation()
    {
        await using var db = CreateDbContext();
        await Assert.ThrowsAsync<TicketIssueValidationException>(
            () => new IssueTicketsHandler(db, TicketTestData.Clock).HandleAsync(null!));
        Assert.Empty(await db.Tickets.ToArrayAsync());
    }

    [Theory]
    [InlineData(1, "0")]
    [InlineData(10, "9999999999.99")]
    public async Task HandleAsync_AcceptsBoundariesAndNormalizesSnapshot(int quantity, string price)
    {
        await using var db = CreateDbContext();
        var message = CreateMessage(quantity) with
        {
            EventName = "  " + new string('e', 200) + "  ",
            VenueName = "  " + new string('v', 200) + "  ",
            SectionName = "  " + new string('s', 100) + "  ",
            UnitPrice = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture),
            EventStartsAtUtc = DateTimeOffset.Parse("2026-11-01T20:00:00+01:00")
        };
        var handler = new IssueTicketsHandler(db, TicketTestData.Clock);
        var result = await handler.HandleAsync(message);
        Assert.Equal(quantity, result.Tickets.Length);
        Assert.All(result.Tickets, ticket =>
        {
            Assert.Equal(message.EventName.Trim(), ticket.EventName);
            Assert.Equal(message.VenueName.Trim(), ticket.VenueName);
            Assert.Equal(message.SectionName.Trim(), ticket.SectionName);
            Assert.Equal(message.UnitPrice, ticket.UnitPrice);
            Assert.Equal(TimeSpan.Zero, ticket.EventStartsAt.Offset);
            Assert.Equal(message.EventStartsAtUtc.UtcDateTime, ticket.EventStartsAt.UtcDateTime);
        });
        var retry = await handler.HandleAsync(message with
        {
            EventName = message.EventName.Trim(), VenueName = message.VenueName.Trim(),
            SectionName = message.SectionName.Trim(), EventStartsAtUtc = message.EventStartsAtUtc.ToUniversalTime(),
            CorrelationId = Guid.NewGuid(), OccurredAtUtc = message.OccurredAtUtc.AddMinutes(1)
        });
        Assert.True(retry.AlreadyIssued);
        Assert.Equal(result.Tickets.Select(t => t.TicketId), retry.Tickets.Select(t => t.TicketId));
    }

    [Theory]
    [InlineData(TicketStatus.CheckedIn)]
    [InlineData(TicketStatus.Cancelled)]
    public async Task HandleAsync_RetryPreservesStatusAndOriginalIssueTime(TicketStatus status)
    {
        await using var db = CreateDbContext();
        var handler = new IssueTicketsHandler(db, TicketTestData.Clock);
        var message = CreateMessage(1);
        var ticket = Assert.Single((await handler.HandleAsync(message)).Tickets);
        ticket.Status = status;
        ticket.CheckedInAt = status == TicketStatus.CheckedIn ? TicketTestData.Now.AddHours(1) : null;
        ticket.CancelledAt = status == TicketStatus.Cancelled ? TicketTestData.Now.AddHours(1) : null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var retry = await new IssueTicketsHandler(db, TimeProvider.System).HandleAsync(message);

        Assert.True(retry.AlreadyIssued);
        var returned = Assert.Single(retry.Tickets);
        Assert.Equal(ticket.TicketId, returned.TicketId);
        Assert.Equal(status, returned.Status);
        Assert.Equal(ticket.IssuedAt, returned.IssuedAt);
        Assert.Equal(ticket.CheckedInAt, returned.CheckedInAt);
        Assert.Equal(ticket.CancelledAt, returned.CancelledAt);
        Assert.Equal(1, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_IssuesSeparateTicketsForAnotherItemInSameBooking()
    {
        await using var db = CreateDbContext();
        var handler = new IssueTicketsHandler(db, TicketTestData.Clock);
        var message = CreateMessage(1);
        var first = await handler.HandleAsync(message);
        var second = await handler.HandleAsync(message with { BookingItemId = Guid.NewGuid() });
        Assert.False(second.AlreadyIssued);
        Assert.NotEqual(first.Tickets[0].TicketId, second.Tickets[0].TicketId);
        Assert.Equal(2, await db.Tickets.CountAsync());
    }

}
