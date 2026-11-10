# Seq

Start the services from the repository root with
`docker compose --env-file .env -f src/Compose.yaml up -d --build`.
Open [http://localhost:5341](http://localhost:5341). Local Seq runs without
authentication; do not expose it outside a trusted development environment.

The five .NET services send structured logs to `http://seq:5341` and also keep
writing to their container console. Each event includes `ServiceName` and
`Environment`. The published UI port defaults to `5341` and can be changed with
`SEQ_PUBLISHED_PORT`. Configure the ingestion URL with
`Serilog__SeqServerUrl` (Compose sets it to `http://seq:5341`).

Each successful reservation returns its ID in `X-Correlation-ID`; the gateway
and Booking request-completion events include it as `BookingId` and
`CorrelationId`. Payment and ticket messages use the same booking ID as
`CorrelationId`.

To follow a purchase, create a reservation and complete `StartPayment`, then
search `CorrelationId = 'booking-guid'`. For the failed path, set
`PAYMENT_SIMULATION_OUTCOME=Failed` in `.env`, recreate `payment-simulator`, and
use a new reservation. For detailed test-purchase steps, see [payments](payments.md).
