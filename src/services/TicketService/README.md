# TicketService

## Ticket PDF

Ticket lookup and PDF endpoints for guest bookings are internal to BookingService and require `X-Internal-Api-Key`. BookingService exposes guest-scoped endpoints after validating the booking's guest access token. The separate admin lookup and PDF endpoints require a JWT with the `admin` role. PDFs are generated on demand and are not stored in the database.

The QR code contains only the ticket ID. It does not contain customer data, event snapshots, or payment information.

QuestPDF is configured with its Community license. This configuration is appropriate only while the organization continues to meet the Community License eligibility rules. See the [QuestPDF license selection guide](https://www.questpdf.com/license/guide.html).

## Booking integration

BookingService sends `BookingConfirmed` messages for paid booking items through a durable outbox and RabbitMQ. [BookingConfirmedConsumer](messaging/Issuance/BookingConfirmedConsumer.cs) receives the messages and calls [IssueTicketsHandler](Features/Tickets/IssueTickets/IssueTicketsHandler.cs) to issue tickets. [TicketMessagingTopology](messaging/Issuance/TicketMessagingTopology.cs) declares the exchanges, queues and bindings; [RabbitMqConnectionProvider](Infrastructure/Messaging/RabbitMqConnectionProvider.cs) manages the shared connection.

Development seed bookings use `POST /tickets/issue-from-booking` through the same outbox dispatcher instead of RabbitMQ. This lets seed deletion wait for ticket issuance and prevents queued seed messages from recreating deleted tickets. The HTTP endpoint requires `X-Internal-Api-Key` and uses the same issuance handler.

Follow the [repository quickstart](../../../README.md) for local setup. Set `TICKET_SERVICE_INTERNAL_API_KEY` in the repository root's Git-ignored `.env`; Compose passes the same value to BookingService and TicketService.

## Code layout

- [Api/TicketsController.cs](Api/TicketsController.cs) contains the HTTP endpoints for lookup, PDF download, issuance, check-in, cancellation and development-only seed deletion.
- [Features/Tickets/IssueTickets](Features/Tickets/IssueTickets) contains the issuance handler shared by RabbitMQ and HTTP.
- [messaging/Issuance](messaging/Issuance) contains the booking confirmation consumer and ticket messaging topology.
- [Infrastructure/Messaging](Infrastructure/Messaging) contains the RabbitMQ connection provider.
- [Features/Tickets/DownloadTicket](Features/Tickets/DownloadTicket) generates ticket PDFs and QR codes.
- [Features/Tickets/Models](Features/Tickets/Models) contains the ticket entity and its status enum.
- [Data](Data) contains the database context and migrations.
