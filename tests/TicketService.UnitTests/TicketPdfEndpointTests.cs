using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using TicketService.Api;
using TicketService.Data;
using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.IssueTickets;
using TicketService.Features.Tickets.Models;

namespace TicketService.UnitTests;

public sealed class TicketPdfEndpointTests
{
    static TicketPdfEndpointTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public async Task DownloadTicketPdf_ReturnsPdfAttachmentForExistingTicket()
    {
        await using var dbContext = CreateDbContext();
        var ticket = CreateTicket();
        dbContext.Tickets.Add(ticket);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.DownloadTicketPdf(ticket.TicketId, download: true, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal($"ticket-{ticket.TicketId:N}.pdf", file.FileDownloadName);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 5));
    }

    [Fact]
    public async Task DownloadTicketPdf_ReturnsNotFoundForUnknownTicket()
    {
        await using var dbContext = CreateDbContext();
        var controller = CreateController(dbContext);

        var result = await controller.DownloadTicketPdf(Guid.NewGuid(), download: false, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetTicketsByBooking_ReturnsOnlyTicketsForTheBookingInTicketOrder()
    {
        await using var dbContext = CreateDbContext();
        var bookingId = Guid.NewGuid();
        var firstTicket = CreateTicket();
        var secondTicket = CreateTicket();
        firstTicket.BookingId = bookingId;
        secondTicket.BookingItemId = firstTicket.BookingItemId;
        firstTicket.TicketNumberInBookingItem = 1;
        secondTicket.BookingId = bookingId;
        secondTicket.TicketNumberInBookingItem = 2;
        var unrelatedTicket = CreateTicket();
        dbContext.Tickets.AddRange(secondTicket, unrelatedTicket, firstTicket);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext);

        var result = await controller.GetTicketsByBooking(bookingId, CancellationToken.None);

        var response = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);
        var tickets = Assert.IsAssignableFrom<IReadOnlyList<TicketResponse>>(response.Value);
        Assert.Equal(new[] { 1, 2 }, tickets.Select(ticket => ticket.TicketNumberInBookingItem));
        Assert.All(tickets, ticket => Assert.Equal(bookingId, ticket.BookingId));
    }

    private static TicketsController CreateController(TicketDbContext dbContext) =>
        new(
            dbContext,
            new IssueTicketsHandler(dbContext, TimeProvider.System),
            new TicketDocumentService(),
            new ConfigurationBuilder().Build(),
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
        EventName = "Testkonsert",
        EventStartsAt = DateTimeOffset.Parse("2026-11-01T18:00:00Z"),
        VenueName = "Hälsingehallen",
        SectionName = "Ståplats",
        UnitPrice = 249.50m,
        Currency = "SEK",
        Status = TicketStatus.Valid,
        IssuedAt = DateTimeOffset.UtcNow
    };
}
