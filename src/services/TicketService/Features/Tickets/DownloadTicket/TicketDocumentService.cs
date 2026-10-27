using System.Globalization;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TicketService.Features.Tickets.Models;

namespace TicketService.Features.Tickets.DownloadTicket;

public sealed class TicketDocumentService
{
    private static readonly TimeZoneInfo SwedishTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

    public static string GetQrPayload(Guid ticketId)
    {
        if (ticketId == Guid.Empty)
            throw new ArgumentException("Ticket ID is required.", nameof(ticketId));

        return ticketId.ToString("D");
    }

    public byte[] GenerateQrCodePng(Guid ticketId)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(GetQrPayload(ticketId), QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule: 8);
    }

    public byte[] GeneratePdf(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var qrCodePng = GenerateQrCodePng(ticket.TicketId);
        var eventStartInSweden = TimeZoneInfo.ConvertTime(ticket.EventStartsAt, SwedishTimeZone);
        var eventDateText = eventStartInSweden.ToString(
            "dddd d MMMM yyyy 'kl.' HH:mm",
            CultureInfo.GetCultureInfo("sv-SE"));

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontSize(12).FontColor(Colors.Grey.Darken4));
                page.Content().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(24).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("HÄLSINGE LIVE  |  BILJETT")
                        .FontSize(10)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);
                    column.Item().Text(ticket.EventName).FontSize(23).Bold();
                    column.Item().Text(eventDateText).FontSize(15);
                    column.Item().Text(ticket.VenueName).FontSize(15).Bold();
                    column.Item().Text($"Sektion: {ticket.SectionName}");
                    column.Item().Text($"Biljett {ticket.TicketNumberInBookingItem}");
                    column.Item().Text($"Biljett-ID: {ticket.TicketId:D}").FontSize(9);
                    column.Item().Text($"Pris: {ticket.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)} {ticket.Currency}");
                    column.Item().AlignCenter().Width(190).Height(190).Image(qrCodePng).FitArea();
                    column.Item().AlignCenter().Text("Visa QR-koden vid entrén.")
                        .FontSize(10)
                        .FontColor(Colors.Grey.Darken1);
                });
            });
        }).GeneratePdf();
    }
}
