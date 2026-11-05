using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketService.Api;
using static TicketService.UnitTests.TicketTestData;

namespace TicketService.UnitTests;

public sealed class IssueTicketsEndpointTests
{
    [Fact]
    public async Task IssueFromBooking_ReturnsSnapshotAndSameTicketsOnRetry()
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db);
        var message = CreateMessage();

        var response = Assert.IsType<OkObjectResult>(await controller.IssueFromBooking(message, "test-key", default));
        var body = Assert.IsType<IssueTicketsResponse>(response.Value);

        Assert.False(body.AlreadyIssued);
        Assert.Equal(2, body.Tickets.Count);
        db.ChangeTracker.Clear();
        var saved = await db.Tickets.OrderBy(t => t.TicketNumberInBookingItem).ToArrayAsync();
        Assert.Equal(saved.Select(t => new TicketResponse(
            t.TicketId, t.BookingId, t.BookingItemId, t.TicketNumberInBookingItem, t.EventId,
            t.EventName, t.EventStartsAt, t.VenueName, t.SectionName, t.UnitPrice, t.Currency,
            t.Status.ToString(), t.IssuedAt)), body.Tickets);

        var retry = Assert.IsType<OkObjectResult>(await controller.IssueFromBooking(message, "test-key", default));
        var retriedBody = Assert.IsType<IssueTicketsResponse>(retry.Value);
        Assert.True(retriedBody.AlreadyIssued);
        Assert.Equal(body.Tickets, retriedBody.Tickets);
        Assert.Equal(2, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task IssueFromBooking_InvalidConfirmationReturnsBadRequestWithoutSaving()
    {
        await using var db = CreateDbContext();
        var result = await CreateController(db).IssueFromBooking(CreateMessage(0), "test-key", default);
        var response = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(400, problem.Status);
        Assert.Contains("Quantity", problem.Detail);
        Assert.Empty(await db.Tickets.ToArrayAsync());
    }

    [Theory]
    [InlineData("MessageId")]
    [InlineData("CorrelationId")]
    [InlineData("OccurredAtUtc")]
    public async Task IssueFromBooking_InvalidMetadataReturnsBadRequestWithoutSaving(string field)
    {
        await using var db = CreateDbContext();
        var message = CreateMessage();
        message = field switch
        {
            "MessageId" => message with { MessageId = Guid.Empty },
            "CorrelationId" => message with { CorrelationId = Guid.Empty },
            "OccurredAtUtc" => message with { OccurredAtUtc = default },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var result = await CreateController(db).IssueFromBooking(message, "test-key", default);

        var response = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(400, problem.Status);
        Assert.Contains(field, problem.Detail);
        Assert.Empty(await db.Tickets.ToArrayAsync());
    }

    [Fact]
    public async Task IssueFromBooking_ChangedSnapshotReturnsConflictWithoutReplacingTickets()
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db);
        var message = CreateMessage();
        var first = Assert.IsType<OkObjectResult>(await controller.IssueFromBooking(message, "test-key", default));
        var original = Assert.IsType<IssueTicketsResponse>(first.Value);

        var result = await controller.IssueFromBooking(message with { UnitPrice = 1m }, "test-key", default);

        var response = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, Assert.IsType<ProblemDetails>(response.Value).Status);
        db.ChangeTracker.Clear();
        var saved = await db.Tickets.OrderBy(t => t.TicketNumberInBookingItem).ToArrayAsync();
        Assert.Equal(original.Tickets.Select(t => t.TicketId), saved.Select(t => t.TicketId));
        Assert.All(saved, t => Assert.Equal(message.UnitPrice, t.UnitPrice));
    }
}
