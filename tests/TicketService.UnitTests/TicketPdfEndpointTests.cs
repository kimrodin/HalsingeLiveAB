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

        var result = await controller.DownloadTicketPdf(ticket.TicketId, CancellationToken.None);

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

        var result = await controller.DownloadTicketPdf(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
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
