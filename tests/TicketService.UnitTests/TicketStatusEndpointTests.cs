using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TicketService.Api;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Features.Tickets.Models;

namespace TicketService.UnitTests;

public sealed class TicketStatusEndpointTests
{
    [Fact]
    public async Task CheckInTicket_ChangesValidTicketAndRecordsTime()
    {
        await using var db = CreateDbContext();
        var ticket = CreateTicket();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var before = DateTimeOffset.UtcNow;

        var result = await CreateController(db).CheckInTicket(ticket.TicketId, "test-key", default, ticket.EventId);
        db.ChangeTracker.Clear();
        ticket = await db.Tickets.SingleAsync();

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<TicketStatusChangeResponse>(response.Value);
        Assert.Equal(TicketStatus.CheckedIn.ToString(), body.Status);
        Assert.Equal(ticket.TicketId, body.TicketId);
        Assert.InRange(body.ChangedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(TicketStatus.CheckedIn, ticket.Status);
        Assert.Equal(body.ChangedAt, ticket.CheckedInAt);
        Assert.Null(ticket.CancelledAt);
    }

    [Fact]
    public async Task CancelTicket_ChangesValidTicketAndRecordsTime()
    {
        await using var db = CreateDbContext();
        var ticket = CreateTicket();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var before = DateTimeOffset.UtcNow;

        var result = await CreateController(db).CancelTicket(ticket.TicketId, "test-key", default);
        db.ChangeTracker.Clear();
        ticket = await db.Tickets.SingleAsync();

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<TicketStatusChangeResponse>(response.Value);
        Assert.Equal(TicketStatus.Cancelled.ToString(), body.Status);
        Assert.Equal(ticket.TicketId, body.TicketId);
        Assert.InRange(body.ChangedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
        Assert.Equal(body.ChangedAt, ticket.CancelledAt);
        Assert.Null(ticket.CheckedInAt);
    }

    [Fact]
    public async Task CheckInTicket_RejectsCancelledTicket()
    {
        await using var db = CreateDbContext();
        var ticket = CreateTicket();
        ticket.Status = TicketStatus.Cancelled;
        ticket.CancelledAt = DateTimeOffset.UtcNow;
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var result = await CreateController(db).CheckInTicket(ticket.TicketId, "test-key", default, ticket.EventId);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(TicketStatus.Cancelled, ticket.Status);
    }

    [Fact]
    public async Task CancelTicket_RejectsCheckedInTicket()
    {
        await using var db = CreateDbContext();
        var ticket = CreateTicket();
        ticket.Status = TicketStatus.CheckedIn;
        ticket.CheckedInAt = DateTimeOffset.UtcNow;
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var result = await CreateController(db).CancelTicket(ticket.TicketId, "test-key", default);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(TicketStatus.CheckedIn, ticket.Status);
        Assert.Null(ticket.CancelledAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusEndpoints_ReturnNotFoundForUnknownTicket(bool checkIn)
    {
        await using var db = CreateDbContext();
        var result = await ChangeStatus(CreateController(db), Guid.NewGuid(), checkIn);
        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusEndpoints_RejectEmptyTicketId(bool checkIn)
    {
        await using var db = CreateDbContext();
        var result = await ChangeStatus(CreateController(db), Guid.Empty, checkIn);
        var response = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, Assert.IsType<ProblemDetails>(response.Value).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusEndpoints_RejectRepeatedOperationAndPreserveTimestamp(bool checkIn)
    {
        await using var db = CreateDbContext();
        var ticket = CreateTicket();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var controller = CreateController(db);
        var first = Assert.IsType<OkObjectResult>(
            await ChangeStatus(controller, ticket.TicketId, checkIn, ticket.EventId));
        var body = Assert.IsType<TicketStatusChangeResponse>(first.Value);
        db.ChangeTracker.Clear();

        var retry = Assert.IsType<ConflictObjectResult>(
            await ChangeStatus(controller, ticket.TicketId, checkIn, ticket.EventId));

        var problem = Assert.IsType<ProblemDetails>(retry.Value);
        Assert.Equal(409, problem.Status);
        Assert.Contains(body.Status, problem.Detail);
        db.ChangeTracker.Clear();
        var saved = await db.Tickets.SingleAsync();
        Assert.Equal(body.Status, saved.Status.ToString());
        Assert.Equal(body.ChangedAt, checkIn ? saved.CheckedInAt : saved.CancelledAt);
        Assert.Null(checkIn ? saved.CancelledAt : saved.CheckedInAt);
    }

    private static Task<IActionResult> ChangeStatus(
        TicketsController controller, Guid ticketId, bool checkIn, Guid? eventId = null) =>
        checkIn ? controller.CheckInTicket(ticketId, "test-key", default, eventId ?? Guid.NewGuid())
            : controller.CancelTicket(ticketId, "test-key", default);

    private static TicketsController CreateController(TicketDbContext db) => new(
        db,
        new IssueTicketsHandler(db, TimeProvider.System),
        new TicketDocumentService(),
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TicketService:InternalApiKey"] = "test-key"
            })
            .Build(),
        NullLogger<TicketsController>.Instance);

    private static TicketDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TicketDbContext(options);
    }

    private static Ticket CreateTicket() => new()
    {
        TicketId = Guid.NewGuid(),
        BookingId = Guid.NewGuid(),
        BookingItemId = Guid.NewGuid(),
        TicketNumberInBookingItem = 1,
        EventId = Guid.NewGuid(),
        EventName = "Test event",
        EventStartsAt = DateTimeOffset.UtcNow.AddDays(1),
        VenueName = "Main hall",
        SectionName = "Standing",
        UnitPrice = 100m,
        Currency = "SEK",
        Status = TicketStatus.Valid,
        IssuedAt = DateTimeOffset.UtcNow
    };
}
