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
