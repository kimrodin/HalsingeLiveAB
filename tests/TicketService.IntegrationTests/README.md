# TicketService integration tests

Local prerequisites:

- The .NET 10 SDK (the projects target `net10.0`; check with `dotnet --list-sdks`).
- Docker Engine or Docker Desktop running Linux containers, with Docker Compose v2
  (`docker compose version`). The Compose file starts PostgreSQL 18 and waits for
  it to accept connections. No separate PostgreSQL installation is needed.
- A free local TCP port `55488` for the Compose PostgreSQL service.

Run from the repository root in PowerShell. Set the environment variable and run
the tests in the same shell:

```powershell
docker compose -p ticket-tests -f tests/TicketService.IntegrationTests/compose.yaml up -d --wait
$env:TICKET_TEST_POSTGRES = 'Host=127.0.0.1;Port=55488;Database=postgres;Username=postgres;Password=postgres'
dotnet test tests/TicketService.IntegrationTests/TicketService.IntegrationTests.csproj
docker compose -p ticket-tests -f tests/TicketService.IntegrationTests/compose.yaml down
```

Alternatively, point `TICKET_TEST_POSTGRES` at a dedicated, running PostgreSQL 18
test server. The fixture connects to its `postgres` maintenance database, creates
and migrates a randomly named `ticket_tests_*` database, and drops it afterwards.
The configured account must be able to connect to `postgres`, have `CREATEDB`
permission, and be allowed to drop its own test databases. The Compose `postgres`
account already has these permissions. Use a dedicated test instance.

When `TICKET_TEST_POSTGRES` is missing, empty, or whitespace, database tests skip
with `Set TICKET_TEST_POSTGRES to run against PostgreSQL.` The skip-behavior smoke
tests still run without PostgreSQL. To see skip messages locally, run:

```powershell
dotnet test tests/TicketService.IntegrationTests/TicketService.IntegrationTests.csproj --logger 'console;verbosity=normal'
```

A configured but unreachable server or insufficient database permissions causes
a test failure. CI supplies the variable using its PostgreSQL service.

The consumer recovery tests also require `TICKET_TEST_RABBITMQ`, an AMQP URI for
a dedicated RabbitMQ test broker or isolated virtual host. They recreate the
ticket queue and close the consumer connection, then verify that ticket issuance
resumes. See [the messaging test instructions](../../docs/messaging.md#integrationstester-med-rabbitmq)
for broker setup. These tests skip if either connection variable is missing.
When sharing a test broker across projects, run the solution with
`dotnet test HalsingeLiveAB.sln --maxcpucount:1`, as CI does.

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
