# Compose configuration

Docker Compose reads configuration from `.env` in the repository root. A fresh
checkout needs a complete local development `.env`, provisioned separately from
Git. Required variable names are listed in `src/Compose.yaml` and summarized
below. Set `TICKET_SERVICE_INTERNAL_API_KEY` to a generated key, for example with
`openssl rand -hex 32`. `.env` is ignored by Git and excluded from the backend
Docker build context.

Run Compose from the repository root:

```sh
docker compose up -d
```

To select both Compose files portably, set `COMPOSE_PATH_SEPARATOR=,` and
`COMPOSE_FILE=src/Compose.yaml,src/Compose.dev.yaml` in `.env`. Existing files that
use the platform's default separator also work without `COMPOSE_PATH_SEPARATOR`:
semicolon on Windows, colon on Unix.

To select the base configuration explicitly, including from another directory:

```sh
docker compose --env-file /path/to/repository/.env -f /path/to/repository/src/Compose.yaml up -d
```

## Settings

| Variables | Configure |
| --- | --- |
| `TICKET_SERVICE_INTERNAL_API_KEY` | Shared key used by Booking and TicketService. |
| `ASPNETCORE_ENVIRONMENT`, `API_GATEWAY_HTTP_PORTS`, `INITIALIZE_DATABASE` | Current application environment, gateway listener, and development initialization flag. |
| `POSTGRES_IMAGE`, `REDIS_IMAGE`, `RABBITMQ_IMAGE`, `KEYCLOAK_IMAGE` | Existing dependency image versions. |
| `*_BASE_URL`, `API_GATEWAY_PROXY_TARGET`, `*_TIMEOUT_SECONDS` | Container-to-container HTTP addresses and request timeouts. |
| `*_PUBLISHED_PORT` | Published host port, optionally prefixed by a bind address such as `127.0.0.1:`. Frontend values apply to both Compose files. |
| `BOOKING_DB_*`, `TICKETSERVICE_DB_*`, `EVENTCATALOG_DB_*`, `PAYMENT_SIMULATOR_DB_*` | Database hosts, internal ports, names, users, passwords, and published ports. Each database's name, user, and password feed both its PostgreSQL container and application connection string. |
| `REDIS_CONNECTION_STRING` | Booking's Redis connection. |
| `RABBITMQ_HOST`, `RABBITMQ_PORT`, `RABBITMQ_VIRTUAL_HOST`, `RABBITMQ_USER`, `RABBITMQ_PASSWORD` | RabbitMQ client connections. |
| `RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS` | Existing broker runtime arguments. |
| `AUTHENTICATION_AUTHORITY`, `AUTHENTICATION_METADATA_ADDRESS` | Token issuer and authentication discovery address. |
| `KC_BOOTSTRAP_ADMIN_USERNAME`, `KC_BOOTSTRAP_ADMIN_PASSWORD` | Keycloak console bootstrap account. |
| `KEYCLOAK_APP_ADMIN_PASSWORD` | Password for the existing `testadmin` application account when importing a new realm. |
| `KC_HOSTNAME`, `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` | Keycloak hostname settings. |
| `VENUE_MAP_STORAGE_ROOT_PATH`, `EVENT_IMAGE_STORAGE_ROOT_PATH` | Service storage paths and corresponding volume mount targets. |
| `PAYMENT_SIMULATION_*` | Existing payment simulation outcome and timing controls. |

Compose passes only the settings listed for each service, rather than the whole
`.env` file. Shell environment variables still take precedence. Required values
use `${VARIABLE:?Set VARIABLE}` so a missing configuration fails before startup.
CI checks the Compose structure using `config --no-interpolate --no-normalize
--quiet`, without needing local environment values. To validate the resolved
values in your own `.env`, run `docker compose config --quiet` from the repository
root. This local check includes interpolation and normalization.

## Existing behavior and scope

The initial `.env` values preserve the previous development setup: database
credentials, API key, host bindings, image versions, login accounts, startup
commands, volume names, and initialization behavior stay the same.

Changing PostgreSQL initialization variables does not change users or passwords
inside an existing data volume. RabbitMQ client variables do not create broker
accounts; provision a matching user separately before changing them. Keycloak
bootstrap settings do not reset existing console accounts, and realm import
skips an existing realm. The realm JSON resolves `KEYCLOAK_APP_ADMIN_PASSWORD`
using Keycloak's supported environment-variable placeholders during import.

Application `appsettings.json`, IDE launch profiles, and the host-side migration
scripts retain their local development defaults. Direct `dotnet run` and EF
commands do not automatically read Compose's `.env`; supply the usual .NET
environment variables when overriding those defaults. Build definitions,
container ports, commands, health-check timing, and service/volume names remain
in Compose.

These are local development defaults. A production `.env` needs its own secrets
and protected file permissions; the current development commands and settings
still require a separate production deployment configuration.

References: [Compose interpolation](https://docs.docker.com/compose/how-tos/environment-variables/variable-interpolation/),
[Keycloak realm import](https://www.keycloak.org/server/importExport).
