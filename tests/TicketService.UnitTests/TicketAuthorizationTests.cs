using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketService.Api;
using TicketService.Features.Tickets.Models;
using static TicketService.UnitTests.TicketTestData;

namespace TicketService.UnitTests;

public sealed class TicketAuthorizationTests
{
    public static IEnumerable<object?[]> UnauthorizedRequests()
    {
        foreach (var endpoint in new[] { "issue", "list", "pdf", "check-in", "cancel" })
            yield return [endpoint, "test-key", "", 401];

        // The endpoints share authorization logic; exercise its remaining cases through issuance.
        yield return ["issue", "test-key", "wrong-key", 401];
        foreach (var configured in new string?[] { null, "", "   " })
            yield return ["issue", configured, "test-key", 503];
    }

    [Theory]
    [MemberData(nameof(UnauthorizedRequests))]
    public async Task Endpoints_RejectUnauthorizedRequestsWithoutChangingTickets(
        string endpoint, string? configured, string? provided, int expectedStatus)
    {
        await using var db = CreateDbContext();
        var ticket = CreateTicket();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        var controller = CreateController(db, configured);

        var result = endpoint switch
        {
            "issue" => await controller.IssueFromBooking(CreateMessage(), provided!, default),
            "list" => await controller.GetTicketsByBooking(ticket.BookingId, provided!, default),
            "pdf" => await controller.DownloadTicketPdf(ticket.BookingId, ticket.TicketId, false, provided!, default),
            "check-in" => await controller.CheckInTicket(ticket.TicketId, provided!, default),
            "cancel" => await controller.CancelTicket(ticket.TicketId, provided!, default),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };

        if (expectedStatus == 401)
            Assert.IsType<UnauthorizedResult>(result);
        else
        {
            var response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, response.StatusCode);
            Assert.Equal(503, Assert.IsType<ProblemDetails>(response.Value).Status);
        }
        Assert.All(db.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Unchanged, entry.State));
        db.ChangeTracker.Clear();
        var saved = Assert.Single(await db.Tickets.ToArrayAsync());
        Assert.Equal(ticket.TicketId, saved.TicketId);
        Assert.Equal(TicketStatus.Valid, saved.Status);
        Assert.Null(saved.CheckedInAt);
        Assert.Null(saved.CancelledAt);
    }
}
