# Seq

Start the services as usual with `docker compose -f src/Compose.yaml up --build`.
Open [http://localhost:5341](http://localhost:5341). Local Seq runs without
authentication; do not expose it outside a trusted development environment.

The five .NET services send structured logs to `http://seq:5341` and also keep
writing to their container console. Each event includes `ServiceName` and
`Environment`. The published UI port defaults to `5341` and can be changed with
`SEQ_PUBLISHED_PORT`. Configure the ingestion URL with
`Serilog__SeqServerUrl` (Compose sets it to `http://seq:5341`).

In Seq, search for a service with `ServiceName = 'Booking'`, or filter by a
booking identifier with `BookingId = '...'`. Payment and ticket messages also
log their message and correlation identifiers when available.
