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

    [Theory]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    [InlineData("ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("67b2fb0a-ee23-59f3-6fd2-9158d203f4ea")]
    [InlineData("35e400e0-2cee-f375-e353-92955d754c0c")]
    public void GenerateQrCodePng_EncodesOnlyTheTicketId(string ticketIdText)
    {
        var ticketId = Guid.Parse(ticketIdText);
        var png = documentService.GenerateQrCodePng(ticketId);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        var image = ImageResult.FromMemory(png, ColorComponents.RedGreenBlue);
        // This fixture is an unrotated monochrome PNG, not a camera image.
        // Skip scene detection, which can miss valid patterns for some ticket IDs.
        var reader = new BarcodeReaderGeneric
        {
            Options = new ZXing.Common.DecodingOptions
            {
                PureBarcode = true,
                PossibleFormats = [BarcodeFormat.QR_CODE]
            }
        };
        var decoded = reader.Decode(
            image.Data, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.RGB24);
        Assert.NotNull(decoded);
        Assert.Equal(BarcodeFormat.QR_CODE, decoded.BarcodeFormat);
        Assert.Equal(ticketId.ToString("D"), decoded.Text);
    }

    [Theory]
    [InlineData("2026-01-15T18:00:00Z", "januari", "19:00")]
    [InlineData("2026-07-15T18:00:00Z", "juli", "20:00")]
    public void GeneratePdf_IncludesSnapshotAndSwedishLocalTime(
        string startsAt, string expectedMonth, string expectedTime)
    {
        var ticket = TicketTestData.CreateTicket();
        ticket.EventStartsAt = DateTimeOffset.Parse(startsAt);

        using var pdf = PdfDocument.Open(documentService.GeneratePdf(ticket));
        // Extract words independently of line breaks, spacing, and reading order.
        var words = pdf.GetPages().SelectMany(page => page.GetWords())
            .Select(word => word.Text).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[]
        {
            ticket.EventName, ticket.VenueName, ticket.SectionName,
            ticket.TicketId.ToString("D"), expectedMonth, expectedTime
        })
            Assert.Contains(expected, words);
    }

    [Fact]
    public void GeneratePdf_RejectsNullTicket() =>
        Assert.Throws<ArgumentNullException>(() => documentService.GeneratePdf(null!));
}
