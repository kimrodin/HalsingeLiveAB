# TicketService

## Ticket PDF

`GET /tickets/{ticketId}/pdf` returns a generated PDF for an issued ticket. The PDF is generated on demand and is not stored in the database.

The QR code contains only the ticket ID. It does not contain customer data, event snapshots, or payment information.

QuestPDF is configured with its Community license. This configuration is appropriate only while the organization continues to meet the Community License eligibility rules. See the [QuestPDF license selection guide](https://www.questpdf.com/license/guide.html).
