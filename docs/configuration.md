# Lokal Compose-konfiguration

Skapa en Git-ignorerad `.env` i projektroten med `NAMN=värde` för varje variabel
nedan. Ingen miljömall ingår. Värdena avser lokal utveckling med loopback-portar.

## Generera hemligheter

Generera ett separat värde för varje `*_PASSWORD` och för
`TICKET_SERVICE_INTERNAL_API_KEY`. Ersätt **eget genererat värde** i tabellen.
Den interna nyckeln delas automatiskt av Booking och TicketService.

PowerShell (även version 5):

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
[BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
```

macOS/Linux:

```sh
openssl rand -hex 32
```

Behåll hemligheterna mellan starter och håll dem utanför Git och delade loggar.
Hexvärden behöver ingen extra escapning i anslutningssträngarna.

## Variabler

| Variabel | Värde för lokal utveckling |
| --- | --- |
| `COMPOSE_PATH_SEPARATOR` | `,` |
| `COMPOSE_FILE` | `src/Compose.yaml,src/Compose.dev.yaml` (Vite/hot reload); enbart `src/Compose.yaml` för Nginx |
| `ASPNETCORE_ENVIRONMENT` | `Development` |
| `INITIALIZE_DATABASE` | `true` (aktiverar migrationer i EventCatalog, Booking och TicketService; PaymentSimulator migrerar alltid) |
| `API_GATEWAY_HTTP_PORTS` | `8080` (intern containerport; ändra inte till värdporten) |
| `POSTGRES_IMAGE` | `postgres:18-alpine` |
| `REDIS_IMAGE` | `redis:7-alpine` |
| `RABBITMQ_IMAGE` | `rabbitmq:4-management-alpine` |
| `KEYCLOAK_IMAGE` | `quay.io/keycloak/keycloak:26.8.2` |
| `EVENTCATALOG_BASE_URL` | `http://eventcatalog:8080/` |
| `BOOKING_BASE_URL` | `http://booking:8080/` |
| `TICKET_SERVICE_BASE_URL` | `http://ticketservice:8080/` |
| `PAYMENT_SIMULATOR_BASE_URL` | `http://payment-simulator:8080/` |
| `API_GATEWAY_PROXY_TARGET` | `http://api-gateway:8080` |
| `EVENTCATALOG_TIMEOUT_SECONDS` | `5` |
| `BOOKING_TIMEOUT_SECONDS` | `5` |
| `TICKET_SERVICE_TIMEOUT_SECONDS` | `5` |
| `PAYMENT_SIMULATOR_TIMEOUT_SECONDS` | `5` |
| `TICKET_SERVICE_INTERNAL_API_KEY` | **eget genererat värde** |
| `API_GATEWAY_PUBLISHED_PORT` | `127.0.0.1:5212` |
| `ADMIN_WEB_PUBLISHED_PORT` | `127.0.0.1:5173` |
| `CUSTOMER_WEB_PUBLISHED_PORT` | `127.0.0.1:5174` |
| `KEYCLOAK_PUBLISHED_PORT` | `127.0.0.1:8180` |
| `BOOKING_DB_HOST` | `booking-db` |
| `BOOKING_DB_PORT` | `5432` |
| `BOOKING_DB_NAME` | `booking_dev` |
| `BOOKING_DB_USER` | `postgres` |
| `BOOKING_DB_PASSWORD` | **eget genererat värde** |
| `BOOKING_DB_PUBLISHED_PORT` | `127.0.0.1:5433` |
| `TICKETSERVICE_DB_HOST` | `ticketservice-db` |
| `TICKETSERVICE_DB_PORT` | `5432` |
| `TICKETSERVICE_DB_NAME` | `ticketservice_dev` |
| `TICKETSERVICE_DB_USER` | `postgres` |
| `TICKETSERVICE_DB_PASSWORD` | **eget genererat värde** |
| `TICKETSERVICE_DB_PUBLISHED_PORT` | `127.0.0.1:5435` |
| `EVENTCATALOG_DB_HOST` | `eventcatalog-db` |
| `EVENTCATALOG_DB_PORT` | `5432` |
| `EVENTCATALOG_DB_NAME` | `eventcatalog_dev` |
| `EVENTCATALOG_DB_USER` | `postgres` |
| `EVENTCATALOG_DB_PASSWORD` | **eget genererat värde** |
| `EVENTCATALOG_DB_PUBLISHED_PORT` | `127.0.0.1:5432` |
| `PAYMENT_SIMULATOR_DB_HOST` | `payment-simulator-db` |
| `PAYMENT_SIMULATOR_DB_PORT` | `5432` |
| `PAYMENT_SIMULATOR_DB_NAME` | `payment_simulator_dev` |
| `PAYMENT_SIMULATOR_DB_USER` | `postgres` |
| `PAYMENT_SIMULATOR_DB_PASSWORD` | **eget genererat värde** |
| `PAYMENT_SIMULATOR_DB_PUBLISHED_PORT` | `127.0.0.1:5434` |
| `REDIS_CONNECTION_STRING` | `booking-redis:6379` |
| `REDIS_PUBLISHED_PORT` | `127.0.0.1:6379` |
| `RABBITMQ_HOST` | `rabbitmq` |
| `RABBITMQ_PORT` | `5672` |
| `RABBITMQ_VIRTUAL_HOST` | `/` |
| `RABBITMQ_USER` | `halsingelive` (skapas automatiskt i en tom broker) |
| `RABBITMQ_PASSWORD` | **eget genererat värde** |
| `RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS` | `+S 2:2` |
| `RABBITMQ_PUBLISHED_PORT` | `127.0.0.1:5672` |
| `RABBITMQ_MANAGEMENT_PUBLISHED_PORT` | `127.0.0.1:15672` |
| `AUTHENTICATION_AUTHORITY` | `http://localhost:8180/realms/halsingelive` |
| `AUTHENTICATION_METADATA_ADDRESS` | `http://keycloak:8080/realms/halsingelive/.well-known/openid-configuration` |
| `KC_BOOTSTRAP_ADMIN_USERNAME` | `keycloak-admin` |
| `KC_BOOTSTRAP_ADMIN_PASSWORD` | **eget genererat värde** |
| `KEYCLOAK_APP_ADMIN_PASSWORD` | **eget genererat värde** (för appkontot `testadmin`) |
| `KC_HOSTNAME` | `http://localhost:8180` |
| `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` | `true` |
| `VENUE_MAP_STORAGE_ROOT_PATH` | `/app/uploads/venue-maps` |
| `EVENT_IMAGE_STORAGE_ROOT_PATH` | `/app/uploads/event-images` |
| `PAYMENT_SIMULATION_OUTCOME` | `Succeeded` |
| `PAYMENT_SIMULATION_DELAY_MS` | `500` |
| `PAYMENT_SIMULATION_TIMEOUT_MS` | `5000` |

## Filval och kontroll

`COMPOSE_PATH_SEPARATOR=,` låter samma `COMPOSE_FILE` användas på alla OS.
Utan den används `;` på Windows och `:` på macOS/Linux. Filvalet är relativt
projektroten; YAML-filernas byggkontexter är relativa till `src`.
Vite-läget har seedknapp och hot reload; enbart `src/Compose.yaml` kör Nginx.

Validera vid behov med `docker compose config --quiet`. Utan `--quiet` skrivs
hemligheter ut. Terminalens miljövariabler har företräde framför `.env`.
Startkommandot finns i [snabbstarten](../README.md).

## Portar och befintlig data

- `*_PUBLISHED_PORT` avser värddatorn; interna adresser och portar behålls vid
  portkonflikt. Byter du admin-/Keycloak-port, följ [Keycloak-guiden](keycloak.md).
- Projektnamnet `services` behåller befintliga utvecklingsvolymer. Använd ett
  unikt `COMPOSE_PROJECT_NAME` för [isolerad verifiering](quickstart-verification.md).
- `down` behåller data. Ändrade bootstrap-lösenord uppdaterar inte befintliga
  PostgreSQL-, RabbitMQ- eller Keycloak-konton; en befintlig realm importeras inte om.
- Lokal `dotnet run` och Vite på värddatorn läser inte rotens `.env` automatiskt.
