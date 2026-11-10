# Hälsokontroller

EventCatalog, Booking, PaymentSimulator och TicketService har samma health-endpoints:

| Endpoint | Kontrollerar | Användning |
| --- | --- | --- |
| `GET /health/live` | Att API:t kan svara på HTTP | Liveness, exempelvis vid beslut om omstart |
| `GET /health/ready` | Tjänstens PostgreSQL-anslutning och Redis i Booking | Readiness, exempelvis innan trafik skickas till tjänsten |
| `GET /health` | Samma kontroller som `/health/ready` | Enkel statuskontroll |

Alla endpoints fungerar utan inloggning och svarar med JSON. En frisk tjänst ger
HTTP **200**. Om en readiness-kontroll misslyckas eller överskrider sin timeout
ger den endpointen HTTP **503**. Svaret kan exempelvis se ut så här i Booking:

```json
{
  "status": "Healthy",
  "checks": {
    "postgresql": "Healthy",
    "redis": "Healthy"
  }
}
```

Liveness anropar inga beroenden och ger en tom `checks`-samling. Svaren får
cacheförhindrande headers och innehåller inga anslutningssträngar eller felmeddelanden.
Detaljer om misslyckade kontroller loggas på servern.

## Implementation

Varje tjänst registrerar ASP.NET Cores `AddHealthChecks()` i sin `Program.cs` och
använder Microsofts paket
`Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` i version
`10.0.12`, samma version som projektets EF Core-paket:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BookingDbContext>("postgresql", tags: ["ready"])
    .AddCheck<Booking.Health.RedisHealthCheck>("redis", tags: ["ready"],
        timeout: TimeSpan.FromSeconds(3));
```

`AddDbContextCheck` använder den befintliga DbContext-registreringen och kör
`Database.CanConnectAsync()`. Bookings `RedisHealthCheck` använder den befintliga
`IConnectionMultiplexer` och kör `PING`. Databaskontrollerna konfigureras också med
tre sekunders timeout. Inga kontroller ändrar verksamhetsdata eller applicerar
migrationer.

Redis-anslutningen använder `AbortOnConnectFail = false` för att kunna återansluta
efter ett avbrott. Om anslutningen är bruten rapporteras den som `Unhealthy`.

`MapHealthChecks` kopplar kontrollerna till HTTP-endpoints. Taggen `ready` väljer
beroendekontrollerna; liveness använder `Predicate = _ => false` för att hoppa över
dem. Readiness ger även HTTP 503 om en kontroll rapporterar `Degraded`.
EventCatalog undantar `/health` från HTTPS-omdirigering så att Docker-proben kan
använda tjänstens interna HTTP-port.

Readiness anropar inte de andra API-tjänsterna eller Keycloak. Det undviker kedjor
av felstatus mellan tjänster och låter exempelvis Bookings outbox återförsöka
biljettutfärdande när TicketService återkommer. Kontrollerna verifierar anslutningar;
de garanterar inte att databasschemat är uppdaterat eller att hela bokningsflödet fungerar.

Tjänsten måste ha startat innan endpoints kan svara. PaymentSimulator kör sina
migrationer före HTTP-serverns start, så ett databasfel under uppstart kan hindra
även liveness från att bli tillgänglig.

## Docker Compose

Följ [snabbstarten](../README.md) och ange miljön enligt
[konfigurationsguiden](configuration.md). Gatewayens egen `GET /health` på
`http://localhost:5212/health` kontrollerar bara gatewayen. Kontrollera även de
fyra readiness-adresserna nedan. API-portarna publiceras inte direkt av Compose.

Keycloak har en intern readiness-kontroll på managementport 9000. EventCatalog
och gatewayen väntar på frisk Keycloak; gatewayen väntar även på de fyra API:erna.
`docker compose ps` visar containerstatus. För loggar, använd
`docker compose logs --tail=100`. Webbappar i Vite-läge kan installera beroenden
efter att `up -d --build` har avslutats; kontrollera även deras HTTP-svar.

De fyra API-tjänsterna har en gemensam `healthcheck` i `src/Compose.yaml` som anropar
`/health/ready` var femtonde sekund. Docker tillåter tre misslyckade kontroller och
har en startperiod på trettio sekunder. Runtime-avbildningarna installerar `curl`
för dessa anrop.

Booking väntar på friska PaymentSimulator och TicketService; EventCatalog väntar
på frisk Booking. PostgreSQL och Redis behåller sina egna befintliga kontroller.
Docker Compose visar en container som `unhealthy` vid bestående fel. En sådan
status startar inte automatiskt om containern.

Bygg om API-containrarna för att aktivera ändringen i den lokala Docker-miljön:

```powershell
docker compose up -d --build
```

| Tjänst | Readiness i lokal Compose |
| --- | --- |
| EventCatalog | `http://localhost:5212/api/health/ready` |
| Booking | `http://localhost:5212/booking-api/health/ready` |
| TicketService | `http://localhost:5212/ticket-api/health/ready` |
| PaymentSimulator | `http://localhost:5212/payment-api/health/ready` |

Se även [Microsofts dokumentation om health checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0).

## Kodreferenser

- [Bookings registrering och health-endpoints](../src/services/Booking/program.cs)
- [Bookings Redis-kontroll](../src/services/Booking/Health/RedisHealthCheck.cs)
- [EventCatalogs registrering och health-endpoints](../src/services/EventCatalog/program.cs)
- [PaymentSimulators registrering och health-endpoints](../src/services/PaymentSimulator/Program.cs)
- [TicketServices registrering och health-endpoints](../src/services/TicketService/Program.cs)
- [Gatewayens egen health-endpoint](../src/ApiGateway/Program.cs)
- [Containrarnas healthchecks och startberoenden](../src/Compose.yaml)
