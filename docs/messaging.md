# Service messaging

Booking publishes `BookingConfirmedV1` messages for confirmed booking items to RabbitMQ. TicketService consumes those messages and issues tickets. The message contract lives in `src/Contracts/Messaging` and is shared by both services.

Booking writes the message to its database outbox as part of booking confirmation. Its background dispatcher publishes with persistent delivery and publisher confirms, then marks the outbox row delivered. If RabbitMQ is unavailable, the row stays pending and the dispatcher retries it. A broker confirmation means RabbitMQ accepted the message; ticket issuance can still be pending afterward.

TicketService acknowledges a message only after ticket issuance has committed. It retries processing a few times, then rejects the message to the durable dead-letter queue. Ticket issuance is idempotent by booking item, so a message redelivered after a crash does not create duplicate tickets.

The topology is declared by both services:

| Purpose | Name |
| --- | --- |
| Topic exchange | `halsingelive.events` |
| Routing key | `booking.confirmed.v1` |
| Ticket issuance queue | `tickets.booking-confirmed.v1` |
| Dead-letter exchange | `halsingelive.dead` |
| Dead-letter queue | `tickets.booking-confirmed.v1.dead` |

## Local development

The Compose file starts RabbitMQ with its management UI at `http://localhost:15672` and AMQP on `localhost:5672`. The local Compose setup uses the RabbitMQ development `guest` credentials. The broker permits that account from the Compose network for local development; use dedicated credentials and normal RabbitMQ access controls outside local development.

Set `TICKET_SERVICE_INTERNAL_API_KEY` as described in the payment/Compose setup, then start the broker and services with:

```powershell
docker compose -f src/Compose.yaml up -d rabbitmq booking ticketservice
```

Booking and TicketService connect to the Compose DNS name `rabbitmq`. When running from an IDE on the host, their defaults connect to `localhost:5672`. Set `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__UserName`, and `RabbitMq__Password` to override those settings.

Ticket lookup and PDF retrieval remain HTTP calls from Booking to TicketService. RabbitMQ is used for asynchronous ticket issuance.
