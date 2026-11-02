# TicketService integration tests

Run from the repository root with Docker and .NET 10 installed:

```powershell
docker compose -p ticket-tests -f tests/TicketService.IntegrationTests/compose.yaml up -d --wait
$env:TICKET_TEST_POSTGRES = 'Host=127.0.0.1;Port=55488;Database=postgres;Username=postgres;Password=postgres'
dotnet test tests/TicketService.IntegrationTests/TicketService.IntegrationTests.csproj
docker compose -p ticket-tests -f tests/TicketService.IntegrationTests/compose.yaml down
```

The fixture creates and migrates a randomly named database and removes it afterwards.
The account must be allowed to create databases. Tests skip when
`TICKET_TEST_POSTGRES` is missing; CI supplies it using its PostgreSQL service.

Separate EF contexts wait at a bounded save barrier so both requests read the original
state before competing. Tests cover identical and conflicting concurrent issuance,
double check-in, double cancellation, and check-in versus cancellation. They verify
the PostgreSQL unique-index recovery and optimistic-concurrency paths, including
the persisted winning ticket IDs, status, and timestamps.

Issuance races cover one and two tickets. Multi-ticket inserts can encounter a
PostgreSQL deadlock because EF may insert random ticket IDs in different orders.
The test replays only a deadlock victim after both transactions finish, matching
the durable outbox's retry behavior. Other unexpected database errors fail the test.
Single-ticket races exercise the unique-index recovery path without this deadlock.
