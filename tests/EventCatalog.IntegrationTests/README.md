# EventCatalog tests

The focused suite contains 45 test cases: 18 unit tests and 27 PostgreSQL
integration tests. It covers validation boundaries, the default-date regression,
capacity limits, persisted create/update flows, deletion of referenced data, and
event image validation, file storage, upload/replacement/download and deletion.
Repetitive input variants and simple missing-object cases are intentionally omitted.

Run the unit tests without a database:

```powershell
dotnet test tests/EventCatalog.UnitTests/EventCatalog.UnitTests.csproj
```

The integration tests use real PostgreSQL, including migrations, foreign keys,
cascading deletes, transactions and row locks. Start the isolated test server
from the repository root with Docker running:

```powershell
docker compose -p eventcatalog-tests -f tests/EventCatalog.IntegrationTests/compose.yaml up -d --wait
$env:EVENTCATALOG_TEST_POSTGRES = 'Host=127.0.0.1;Port=55486;Database=postgres;Username=postgres;Password=postgres'
dotnet test tests/EventCatalog.IntegrationTests/EventCatalog.IntegrationTests.csproj
docker compose -p eventcatalog-tests -f tests/EventCatalog.IntegrationTests/compose.yaml down
```

Alternatively, set `EVENTCATALOG_TEST_POSTGRES` to a dedicated PostgreSQL test
server. If unset, the fixture connects to localhost:5433 with user/password
postgres/postgres. The account must be able to create databases. Each test class
creates and migrates a uniquely named database, and drops only that database
afterwards. Each test creates its own venues and events. The fixture never resets
an existing application database. The Docker server stores its data in memory;
stopping it discards all test data.

Integration tests fail if the database is unavailable; they are not silently
skipped. HTTP routing, authentication middleware and other services are outside
these handler integration tests.

You can also run the integration tests entirely in Linux containers, without a
local .NET SDK. The source is mounted read-only and build artifacts stay in the
temporary test container:

```powershell
docker compose -p eventcatalog-tests -f tests/EventCatalog.IntegrationTests/compose.yaml run --rm tests
docker compose -p eventcatalog-tests -f tests/EventCatalog.IntegrationTests/compose.yaml down
```
