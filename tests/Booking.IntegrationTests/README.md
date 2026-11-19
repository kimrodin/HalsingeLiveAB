# Booking integration tests

Run from the repository root with Docker and .NET 10 installed:

```powershell
docker compose -p booking-tests -f tests/Booking.IntegrationTests/compose.yaml up -d --wait
$env:BOOKING_TEST_POSTGRES = 'Host=127.0.0.1;Port=55487;Database=postgres;Username=postgres;Password=postgres'
$env:BOOKING_TEST_REDIS = '127.0.0.1:56387'
dotnet test tests/Booking.IntegrationTests/Booking.IntegrationTests.csproj
docker compose -p booking-tests -f tests/Booking.IntegrationTests/compose.yaml down
```

The servers are separate from the application's databases and store test data in
memory. Each test class creates and migrates its own database, uses unique Redis
inventory IDs, and cleans up afterwards. The PostgreSQL account must be allowed
to create databases. Both environment variables are required; tests are skipped
if either is missing. CI configures both services and runs this suite.

For issues #25 and #52, the concurrency tests cover:

- Two customers and 200 customers submitting HTTP bookings for the last place.
  A barrier waits until every request has read the same inventory before letting
  them compete for the Redis reservation. Exactly one receives HTTP 202; every
  other customer receives HTTP 409 with an explanation. Only one booking is saved.
- Two distinct reservations competing for payment confirmation after Redis stock
  is deliberately overstated. Both payments reach a barrier before confirmation.
  PostgreSQL permits exactly one sale and ends with zero stock. The other booking
  is cancelled and refunded, and only the winner creates a ticket outbox message.

These tests use real PostgreSQL and Redis and separate request contexts. Barriers
have bounded waits and require no sleeps. Inventory currently represents ticket
quantities per event section; a stock of one models the last available place.

For issue #151, `ReservationLimitTests` covers the shared IP attempt window and
active reservation/ticket quotas across event sections. It uses real HTTP source
addresses (not a trusted test header), checks forged forwarding headers, and
sends 20 concurrent requests across two hosts with independent Redis connections
for each policy. It also checks confirmation, idempotent release, orphan expiry,
failed-save compensation, bounded client-key TTLs and a Redis outage returning
503 without a new booking or stock deduction.

The 3-attempt/60-second test waits for the real window to expire, so this suite
takes at least one minute. Other payment/stock suites intentionally configure
high IP limits so their existing 200-request stock race still tests inventory.
Defaults, NAT considerations and rollout are in
[the reservation limit guide](../../docs/reservation-limits.md).

The Redis outage test starts with an existing three-ticket hold and checks its
status, deadline, quantity, active quota, attempt count and both stock values
after the rejected request. Real Booking requests through both frontend proxies
and the gateway are covered by [the proxy-chain suite](../reservation-chain/README.md).
