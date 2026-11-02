using TicketService.Features.Tickets.DownloadTicket;
using QuestPDF.Infrastructure;
using StbImageSharp;
using UglyToad.PdfPig;
using ZXing;

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
    }

    [Fact]
    public void GetQrPayload_RejectsEmptyTicketId()
    {
        Assert.Throws<ArgumentException>(() => TicketDocumentService.GetQrPayload(Guid.Empty));
    }

    [Fact]
    public void GenerateQrCodePng_EncodesOnlyTheTicketId()
    {
        var ticketId = Guid.NewGuid();
        var png = documentService.GenerateQrCodePng(ticketId);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
        var decoded = new BarcodeReaderGeneric().Decode(
            image.Data, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGB24);
        Assert.NotNull(decoded);
        Assert.Equal(BarcodeFormat.QR_CODE, decoded.BarcodeFormat);
        Assert.Equal(ticketId.ToString("D"), decoded.Text);
    }

    [Theory]
    [InlineData("2026-01-15T18:00:00Z", "torsdag 15 januari 2026 kl. 19:00")]
    [InlineData("2026-07-15T18:00:00Z", "onsdag 15 juli 2026 kl. 20:00")]
    public void GeneratePdf_IncludesSnapshotAndSwedishLocalTime(string startsAt, string expectedDate)
    {
        var ticket = TicketTestData.CreateTicket();
        ticket.EventStartsAt = DateTimeOffset.Parse(startsAt);
        ticket.TicketNumberInBookingItem = 7;

        using var pdf = PdfDocument.Open(documentService.GeneratePdf(ticket));
        // PDF text can contain layout-dependent whitespace and line breaks.
        var text = string.Concat(string.Join("", pdf.GetPages().Select(page => page.Text))
            .Where(character => !char.IsWhiteSpace(character)));
        foreach (var expected in new[]
        {
            ticket.EventName, ticket.VenueName, $"Sektion: {ticket.SectionName}",
            "Biljett 7", $"Biljett-ID: {ticket.TicketId:D}", "Pris: 249.50 SEK", expectedDate
        })
            Assert.Contains(string.Concat(expected.Where(character => !char.IsWhiteSpace(character))), text);
    }

    [Fact]
    public void GeneratePdf_RejectsNullTicket() =>
        Assert.Throws<ArgumentNullException>(() => documentService.GeneratePdf(null!));
}
