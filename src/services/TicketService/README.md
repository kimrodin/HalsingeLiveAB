# TicketService

## Ticket PDF

Ticket lookup and PDF endpoints are internal to BookingService and require `X-Internal-Api-Key`. BookingService exposes guest-scoped endpoints after validating the booking's guest access token. PDFs are generated on demand and are not stored in the database.

The QR code contains only the ticket ID. It does not contain customer data, event snapshots, or payment information.

QuestPDF is configured with its Community license. This configuration is appropriate only while the organization continues to meet the Community License eligibility rules. See the [QuestPDF license selection guide](https://www.questpdf.com/license/guide.html).

## Booking integration

BookingService sends paid booking snapshots to `POST /tickets/issue-from-booking` through a durable outbox. For local Docker Compose, set `TICKET_SERVICE_INTERNAL_API_KEY` in the environment before starting the services. The same value is configured for BookingService and TicketService; do not commit the key to source control.
