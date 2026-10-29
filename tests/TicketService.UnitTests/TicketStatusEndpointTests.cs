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

        var result = await CreateController(db).CheckInTicket(ticket.TicketId, "test-key", default);

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

        var response = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<TicketStatusChangeResponse>(response.Value);
        Assert.Equal(TicketStatus.Cancelled.ToString(), body.Status);
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

        var result = await CreateController(db).CheckInTicket(ticket.TicketId, "test-key", default);

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

    [Fact]
    public async Task StatusEndpoints_RequireInternalApiKey()
    {
        await using var db = CreateDbContext();
        var ticket = CreateTicket();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var checkIn = await controller.CheckInTicket(ticket.TicketId, string.Empty, default);
        var cancel = await controller.CancelTicket(ticket.TicketId, string.Empty, default);

        Assert.IsType<UnauthorizedResult>(checkIn);
        Assert.IsType<UnauthorizedResult>(cancel);
        Assert.Equal(TicketStatus.Valid, ticket.Status);
    }

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
