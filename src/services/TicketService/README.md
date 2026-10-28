# TicketService

## Ticket PDF

`GET /tickets/{ticketId}/pdf` returns a generated PDF for an issued ticket. The PDF is generated on demand and is not stored in the database.

The QR code contains only the ticket ID. It does not contain customer data, event snapshots, or payment information.

QuestPDF is configured with its Community license. This configuration is appropriate only while the organization continues to meet the Community License eligibility rules. See the [QuestPDF license selection guide](https://www.questpdf.com/license/guide.html).

## Booking integration

BookingService sends paid booking snapshots to `POST /tickets/issue-from-booking` through a durable outbox. For local Docker Compose, set `TICKET_SERVICE_INTERNAL_API_KEY` in the environment before starting the services. The same value is configured for BookingService and TicketService; do not commit the key to source control.
