# Service messaging

Booking publishes `BookingConfirmedV1` messages for confirmed booking items to RabbitMQ. TicketService consumes those messages and issues tickets. The message contract lives in `src/Contracts/Messaging` and is shared by both services.

Every `BookingConfirmedV1` field must be explicitly supplied, including `UnitPrice` and `Currency`. An explicit zero price is valid; omitted prices and currencies are rejected rather than defaulted. `MessageId` and `CorrelationId` must be non-empty GUIDs, and `OccurredAtUtc` must be non-default. Booking uses the outbox message ID, the booking ID as the correlation ID, and the outbox creation time as the occurrence time, preserving those values across delivery retries. TicketService validates these metadata fields for both RabbitMQ and HTTP issuance.

Booking writes the message to its database outbox as part of booking confirmation. Its background dispatcher publishes with persistent delivery and publisher confirms, then marks the outbox row delivered. If RabbitMQ is unavailable, the row stays pending and the dispatcher retries it. A broker confirmation means RabbitMQ accepted the message; ticket issuance can still be pending afterward.

TicketService acknowledges a message only after ticket issuance has committed. It retries processing a few times, then rejects the message to the durable dead-letter queue. Ticket issuance is idempotent by booking item, so a message redelivered after a crash does not create duplicate tickets.

Messages with missing fields or invalid contract values are rejected directly to the dead-letter queue without processing retries.

The topology is declared by both services:

| Purpose | Name |
| --- | --- |
| Topic exchange | `halsingelive.events` |
| Routing key | `booking.confirmed.v1` |
| Ticket issuance queue | `tickets.booking-confirmed.v1` |
| Dead-letter exchange | `halsingelive.dead` |
| Dead-letter queue | `tickets.booking-confirmed.v1.dead` |

## Payment contracts

The shared messaging project also defines these contracts for payment messaging:

| Contract | Purpose | Outcome-specific fields |
| --- | --- | --- |
| `PaymentRequestedV1` | Booking requests payment processing; `BookingId` is the idempotency key. | PaymentSimulator assigns the payment ID when creating the attempt. |
| `PaymentSucceededV1` | PaymentSimulator reports a successful payment. | `PaymentId`, `PaidAtUtc` |
| `PaymentFailedV1` | PaymentSimulator reports a terminal failure. | `PaymentId`, `FailureCode` (`Failed` or `TimedOut`) |

All three contracts require `MessageId`, `CorrelationId`, `BookingId`, `Amount`, `Currency`, and `OccurredAtUtc`, plus their outcome-specific fields. No fields have generated or monetary defaults. `Amount` is the booking's total payment amount, and `Currency` is an explicit uppercase three-letter code. The booking ID is used as the correlation ID throughout the payment workflow. Message IDs and occurrence timestamps must be preserved when retrying delivery.

`OccurredAtUtc` records when the request or terminal outcome occurred; `PaidAtUtc` records when payment succeeded. A later refund does not change the historical success event. A network timeout or unavailable broker leaves the outcome unknown and must not produce `PaymentFailedV1`. Consumers still need to validate field values; `required` enforces that fields are supplied, but does not reject empty IDs or invalid amounts.

## Payment request publishing

Booking registers `IPaymentRequestedPublisher` with the `RabbitMqPaymentRequestedPublisher` implementation. It reuses the existing RabbitMQ connection and configuration, publishes `PaymentRequestedV1` as persistent JSON with publisher confirms and mandatory routing, and preserves the supplied message ID, correlation ID, and occurrence time. Broker or routing failures are returned to the caller so it can retry the same message.

The publisher declares the following durable topology so requests can queue before PaymentSimulator starts consuming:

| Purpose | Name |
| --- | --- |
| Topic exchange | `halsingelive.events` |
| Routing key and message type | `payment.requested.v1` |
| Payment processing queue | `payments.payment-requested.v1` |
| Dead-letter exchange | `halsingelive.dead` |
| Dead-letter routing key | `payment-request.dead` |
| Dead-letter queue | `payments.payment-requested.v1.dead` |

PaymentSimulator runs `PaymentRequestedConsumer` against `payments.payment-requested.v1` and declares the same topology as Booking. It validates required fields, message metadata, amount, and uppercase currency, then delegates to the existing payment handler. Processing is idempotent by booking ID; an existing attempt with a different amount or currency is rejected. The consumer acknowledges after the handler returns a durably saved payment. `Failed` and `TimedOut` are valid processed outcomes and are acknowledged normally. Invalid or conflicting requests go directly to the dead-letter queue; processing exceptions are retried up to three attempts before dead-lettering. The consumer reconnects after broker interruptions, and shutdown leaves unfinished deliveries unacknowledged for redelivery. Persisted pending attempts can also finish through the existing payment recovery worker.

Booking's payment initiation and result handling currently use HTTP. Connecting its publisher to a durable payment request outbox and adding payment result publishers and consumers remain to be implemented. The publisher itself does not store requests or retry failed deliveries.

### Payment consumer tests

Unit tests run without external services. Payment messaging integration tests require PostgreSQL and a dedicated RabbitMQ instance, since they use the payment processing and dead-letter queue names above. They create and remove their own randomly named PostgreSQL databases. Set the test connections and run:

```powershell
$env:PAYMENT_TEST_DB = 'Host=localhost;Port=5434;Database=postgres;Username=postgres;Password=postgres'
$env:PAYMENT_TEST_RABBITMQ = 'amqp://payment-tests:payment-tests@localhost:5673/'
dotnet test tests/PaymentSimulator.UnitTests/PaymentSimulator.UnitTests.csproj
dotnet test tests/PaymentSimulator.IntegrationTests/PaymentSimulator.IntegrationTests.csproj
```

The RabbitMQ URI must point to an isolated test broker or virtual host, rather than the broker used by running development services. Messaging tests are skipped when `PAYMENT_TEST_RABBITMQ` is unset. CI supplies PostgreSQL and a dedicated RabbitMQ service and runs these tests automatically.

## Local development

The Compose file starts RabbitMQ with its management UI at `http://localhost:15672` and AMQP on `localhost:5672`. The local Compose setup uses the RabbitMQ development `guest` credentials. The broker permits that account from the Compose network for local development; use dedicated credentials and normal RabbitMQ access controls outside local development.

Set `TICKET_SERVICE_INTERNAL_API_KEY` as described in the payment/Compose setup, then start the broker and services with:

```powershell
docker compose -f src/Compose.yaml up -d rabbitmq booking ticketservice payment-simulator
```

Booking, TicketService, and PaymentSimulator connect to the Compose DNS name `rabbitmq`. When running from an IDE on the host, their defaults connect to `localhost:5672`. Set `RabbitMq__HostName`, `RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__UserName`, and `RabbitMq__Password` to override those settings.

Ticket lookup and PDF retrieval remain HTTP calls from Booking to TicketService. RabbitMQ is used for asynchronous ticket issuance.
