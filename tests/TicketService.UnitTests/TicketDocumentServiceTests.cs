using TicketService.Features.Tickets.DownloadTicket;
using TicketService.Features.Tickets.Models;
using QuestPDF.Infrastructure;

namespace TicketService.UnitTests;

public sealed class TicketDocumentServiceTests
{
    static TicketDocumentServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly TicketDocumentService documentService = new();

    [Fact]
    public void GetQrPayload_ReturnsOnlyTicketId()
    {
        var ticketId = Guid.NewGuid();

        var payload = TicketDocumentService.GetQrPayload(ticketId);

        Assert.Equal(ticketId.ToString("D"), payload);
        Assert.DoesNotContain(":", payload);
    }

    [Fact]
    public void GetQrPayload_RejectsEmptyTicketId()
    {
        Assert.Throws<ArgumentException>(() => TicketDocumentService.GetQrPayload(Guid.Empty));
    }

    [Fact]
    public void GenerateQrCodePng_ReturnsPngImage()
    {
        var png = documentService.GenerateQrCodePng(Guid.NewGuid());

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
    }

    [Fact]
    public void GeneratePdf_ReturnsValidPdf()
    {
        var ticket = new Ticket
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

        var pdf = documentService.GeneratePdf(ticket);

        Assert.True(pdf.Length > 1000);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }
}
