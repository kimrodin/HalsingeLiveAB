# Bokning och betalning

Booking äger priset och reservationstiden (10 minuter). Klienten skickar ett boknings-ID till `POST /StartPayment/{bookingId}` med bokningens gästtoken i `X-Guest-Access-Token`. Booking sparar betalningsavsikten och en request-outbox med databasens `TotalPrice` och valutan `SEK` i samma transaktion. Den första accepterade starten returnerar `202 Accepted` med `Pending`; betalningen startas därefter via RabbitMQ. Bekräftelse kräver ett verifierat betalningsresultat med rätt bokning, belopp, valuta och status `Succeeded`, samt en fortfarande giltig reservation. PostgreSQLs villkorade lageruppdatering är slutlig kontroll mot dubbelförsäljning. Se [meddelandeflöden och kodreferenser](messaging.md).

`POST /ConfirmBooking/{bookingId}` returnerar `403 Forbidden` utan att ändra bokningen. Använd `StartPayment` för hela flödet.

Svaret från `StartPayment` innehåller `bookingId`, `bookingStatus`, `paymentStatus` och `message`:

| HTTP-status | Betydelse |
| --- | --- |
| `200` | Bokningen är bekräftad. |
| `202` | Begäran är beständigt accepterad för asynkron behandling, bearbetning pågår eller betalningsresultatet är ännu okänt. |
| `404` | Bokningen saknas. |
| `409` | Betalningen misslyckades, fick terminal timeout, återbetalades eller bokningen kan inte längre bekräftas. |
| `502` | Simulatorns svar klarade inte kontraktskontrollen eller simulatorn avvisade begäran, exempelvis med `409`. |

Detta är HTTP-statusarna från **Booking till kundwebben**. De är skilda från svaren mellan Booking och PaymentSimulator:

| Svar från PaymentSimulator | Bookings hantering |
| --- | --- |
| `200` med betalningsstatus `Failed` eller `TimedOut` | Bokningen avbryts och reservationen släpps. `StartPayment` returnerar `409` med betalningsresultatet. |
| `404` vid uppslag via boknings-ID | Ingen betalning hittades. Booking väntar på RabbitMQ-flödet om reservationen är giltig, annars spärras ett tidigare begärt försök. Avstämningen startar aldrig ett försök via HTTP. |
| `409`, eller annat avvisat `4xx` utom `408`/`429` | Kontraktsfel, exempelvis motstridiga belopp. `StartPayment` returnerar `502` med `paymentStatus: Invalid`. `404` vid återbetalning är också ett kontraktsfel, inte en saknad bokning. |
| `200` med felaktig JSON eller betalningsuppgifter som inte matchar | `502` med `paymentStatus: Invalid`. |
| `408`, `429`, `5xx`, HTTP-timeout eller nätverksfel | Utfallet är osäkert. `202` med `paymentStatus: Unknown`; bakgrundstjänsten följer upp försöket. |

Ett kontraktsfel innebär inte att betalningen misslyckats. Booking bekräftar eller avbryter därför inte bokningen på den grunden och markerar inte betalningsflödet som färdighanterat. Bakgrundstjänsten behåller uppföljningen och loggar kontraktsfelet; reservationens ursprungliga sluttid gäller fortfarande.

Controllerns `[Route("/[action]")]` ingår i routningen. `ConfirmBooking`, `StartPayment` och `CancelBooking` får därför olika POST-adresser trots samma `"{bookingId:guid}"`-mall på metoderna. HTTP-integrationstesterna kontrollerar routningen och betalningsklientens statushantering tillsammans.

Betalningsbegäran och betalningsresultat går via RabbitMQ med outbox och inbox. HTTP används fortfarande för kundens anrop till Booking, betalningsuppslag och kompensation genom spärrning/återbetalning. Simulatorns betalningslogik ligger kvar i `ProcessPaymentHandler`. RabbitMQ behöver köras för att nya betalningsbegäranden ska behandlas; ett brokeravbrott lämnar dem väntande i Bookings outbox.

## Starta med Docker Compose

Kör från repositoryts rot med Docker Desktop igång:

```powershell
docker compose up -d --build
docker compose ps
```

Beroenden, inklusive PostgreSQL, Redis och RabbitMQ, startas också. Ange `TICKET_SERVICE_INTERNAL_API_KEY` för Booking och TicketService innan Compose-kommandot körs. Tjänsterna initierar sina databaser vid uppstart i utvecklingsmiljön. Kontrollera loggarna om databasen eller API:t ännu inte är redo:

```powershell
docker compose logs booking payment-simulator
```

| Anslutning | Från värddatorn | Mellan containrar |
| --- | --- | --- |
| Booking API via gateway | `http://localhost:5212/booking-api` | `http://booking:8080` |
| PaymentSimulator API via gateway | `http://localhost:5212/payment-api` | `http://payment-simulator:8080` |
| Booking PostgreSQL | `localhost:5433` | `booking-db:5432` |
| PaymentSimulator PostgreSQL | `localhost:5434` | `payment-simulator-db:5432` |
| Redis | `localhost:6379` | `booking-redis:6379` |

Skapa först rotens lokala `.env` enligt [konfigurationsguiden](configuration.md).
Tabellens värdportar följer den guidens värden; API-portarna publiceras inte direkt.
Compose läser `PAYMENT_SIMULATOR_BASE_URL` och `PAYMENT_SIMULATOR_TIMEOUT_SECONDS`
för Bookings interna HTTP-uppslag. Databaserna använder port 5432 internt.
Frontendens `/payment-api/api/payments/...` går genom gatewayen, som tar bort
`/payment-api` men behåller simulatorns eget `/api`.

### Befintlig PaymentSimulator-databas

Tidigare versioner använde `EnsureCreated`. En sådan databas kan sakna `__EFMigrationsHistory` trots att tabellerna redan finns. Vid uppstart kontrollerar simulatorn att det gamla schemat exakt motsvarar initialmigrationens sju kolumner, primärnyckel och unika index för `BookingId`. Endast då registreras initialmigrationen som redan applicerad, och nya migrationer körs utan att betalningarna raderas. Kontrollen skyddas av ett PostgreSQL-lås mot samtidiga starter. Ett okänt schema stoppar uppstarten med ett tydligt fel och behöver granskas manuellt. `docker compose down -v` raderar även de andra tjänsternas data och behövs inte för detta flöde.

## Testa en lyckad betalning

Logga först in i adminappen och kör **Skapa seeddata** enligt [snabbstarten](../README.md).
Det skapar även katalogdata och matchande lager. Exemplet nedan gör ett vanligt
kundköp via gatewayen i PowerShell och hämtar bokningens status med gästtoken:

```powershell
$gateway = 'http://localhost:5212'
$bookingApi = "$gateway/booking-api"
$events = (Invoke-RestMethod -Uri "$gateway/api/ListEvents?PageSize=100").events
$event = $events | Where-Object {
    [DateTimeOffset]$_.startDate -gt [DateTimeOffset]::UtcNow -and
    [DateTimeOffset]$_.onSaleFrom -le [DateTimeOffset]::UtcNow
} | Select-Object -First 1
if (-not $event) { throw 'Skapa ett framtida evenemang som är till försäljning i adminappen.' }
$inventory = Invoke-RestMethod -Uri "$bookingApi/ListEventInventory/$($event.eventId)"
$inventory = $inventory | Where-Object { $_.availableQuantity -gt 0 } | Select-Object -First 1
if (-not $inventory) { throw 'Välj ett annat evenemang med lediga biljetter.' }
$request = @{
    inventoryId = $inventory.inventoryId
    quantity = 1
    customerName = 'Betalningstest'
    customerEmail = 'betalningstest@example.test'
} | ConvertTo-Json
$reservation = Invoke-RestMethod -Method Post -Uri "$bookingApi/CreateBooking" -ContentType 'application/json' -Body $request
$bookingId = $reservation.bookingId
$guestHeaders = @{ 'X-Guest-Access-Token' = $reservation.guestAccessToken }
Invoke-RestMethod -Method Post -Uri "$bookingApi/StartPayment/$bookingId" -Headers $guestHeaders
Invoke-RestMethod -Uri "$bookingApi/GetBooking/$bookingId" -Headers $guestHeaders
# När status har blivit Confirmed, hämta biljetterna (upprepa vid pågående utfärdande):
Invoke-RestMethod -Uri "$bookingApi/GetTicketsForBooking/$bookingId/tickets" -Headers $guestHeaders
```

Spara gästtoken privat för testet; lägg den inte i verifieringsrapporten.
Betalningsuppslag i admin görs genom adminappen med dess Bearer-token.

Med standardinställningarna ska betalningen efter bakgrundsbehandlingen bli `Succeeded` och bokningen `Confirmed`. Läs `GetBooking` igen om status fortfarande är väntande; det första startsvarvet väntar inte på betalningen. Ett upprepat `StartPayment` för samma bokning får inte skapa en extra betalning eller minska lagret igen. För att kontrollera spärren för direkt bekräftelse: skapa en ny reservation och anropa `POST /ConfirmBooking/{bookingId}` före betalningen. Bokningen ska förbli obetald och obekräftad.

## Simulera fel och timeout

Simulatorn kan nu styras vid körning via ett skyddat admin-API. Se
[Payment simulation admin API](payment-simulation-admin.md). Sparade API-inställningar
har företräde framför miljövariablerna nedan och finns kvar efter omstart.
Återställ i så fall också inställningarna via API:t.

Compose läser följande miljövariabler när simulatorcontainern skapas. Inställningarna gäller nya betalningar; ett redan skapat betalningsförsöks utfall ska inte ändras av ändrad konfiguration.

```powershell
$env:PAYMENT_SIMULATION_OUTCOME = 'Failed'
$env:PAYMENT_SIMULATION_DELAY_MS = '500'
$env:PAYMENT_SIMULATION_TIMEOUT_MS = '5000'
docker compose up -d --no-deps --force-recreate payment-simulator
```

Skapa en ny reservation och kör `StartPayment` igen. Vid `Failed` ska Booking avbryta bokningen och frigöra reservationen. För simulatorns terminala timeout, ändra `PAYMENT_SIMULATION_OUTCOME` till `TimedOut`, återskapa simulatorcontainern och använd en ny bokning. Bokningen ska inte bekräftas.

För att testa väntan på betalning, använd `Succeeded`, fördröjning `10000` och simulatortimeout `30000`. Starten svarar fortfarande direkt med `202` och `Pending`, och betalningen slutförs senare via meddelandeflödet. Ett nätverksfel vid uppslag eller ett brokeravbrott får inte tolkas som `Failed`. Outbox-arbetaren försöker publicera väntande begäranden igen, och avstämningen kontrollerar oavslutade flöden var 15:e sekund. Läs bokningen med `GetBooking` och gästtoken för att följa återhämtningen. Det går också att upprepa `StartPayment` med samma boknings-ID för att återanvända det befintliga flödet. Reservationens sluttid förlängs inte. Skicka inte ett nytt slumpmässigt boknings-ID för samma köp.

Reservationens ursprungliga `ExpiresAt` gäller hela tiden. Betalningsfel, polling och återförsök förlänger inte tiden. För att testa en sen betalning: starta en fördröjd lyckad betalning nära reservationens slut och kontrollera att bokningen inte blir `Confirmed` efter utgångstiden. Återbetalningen ska synas som `refundedAt` på betalningen.

Återställ simulatorn till standardinställningarna efter testet:

```powershell
Remove-Item Env:PAYMENT_SIMULATION_OUTCOME -ErrorAction SilentlyContinue
Remove-Item Env:PAYMENT_SIMULATION_DELAY_MS -ErrorAction SilentlyContinue
Remove-Item Env:PAYMENT_SIMULATION_TIMEOUT_MS -ErrorAction SilentlyContinue
docker compose up -d --no-deps --force-recreate payment-simulator
```

## Status, idempotens och kompensation

| Endpoint i PaymentSimulator | Användning |
| --- | --- |
| `POST /api/payments` | Starta eller återanvänd betalningen för ett `bookingId`; Booking skickar det betrodda beloppet. |
| `GET /api/payments/by-booking/{bookingId}` | Återfinn ett försök även om HTTP-svaret från starten förlorats. |
| `GET /api/payments/{paymentId}` | Läs en känd betalning. |
| `POST /api/payments/{paymentId}/refund` | Idempotent kompensation när bokningen inte kan bekräftas. |
| `POST /api/payments/cancel` | Kompensera via `bookingId`, `amount`, `currency`; skapar en beständig spärr även om det ursprungliga startanropet ännu inte hunnit fram. |

Simulatorn har ett unikt betalningsförsök per `BookingId`. Avslutade `Failed`/`TimedOut`-försök återanvänds som terminala resultat; för ett nytt köp behövs en ny reservation. Ett `Pending`-försök lagras med sitt planerade utfall och sin tidsgräns. Bakgrundsbearbetning kan avsluta försöket även om det ursprungliga HTTP-anropet bryts eller tjänsten startas om.

En återbetalad lyckad betalning behåller `Succeeded` som historiskt betalningsutfall och får `refundedAt`. Den får inte användas för att bekräfta bokningen igen. Om en ännu pågående betalning kompenseras spärras senare lyckat utfall. En sen lyckad betalning för en utgången eller avbruten reservation måste kompenseras i simulatorn; den ska inte ignoreras.

Booking-tjänstens bakgrundskontroll fortsätter även för utgångna och avbrutna bokningar med ett oavslutat betalningsflöde. Om simulatorn inte kan nås behålls flödet för senare återhämtning. En tillfällig nätverksstörning får därför inte avsluta en nödvändig återbetalning.

## Automatiserade tester

Med .NET 10 SDK installerat kan enhetstesterna köras utan databaser:

```powershell
dotnet test tests/Booking.UnitTests/Booking.UnitTests.csproj
```

Integrationstesterna använder riktiga PostgreSQL-databaser och Redis. Starta Compose-databaserna, sätt anslutningarna och kör projekten:

```powershell
docker compose up -d booking-db booking-redis payment-simulator-db
$env:BOOKING_TEST_POSTGRES = 'Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=<BOOKING_DB_PASSWORD från din .env>'
$env:BOOKING_TEST_REDIS = 'localhost:6379'
$env:PAYMENT_TEST_DB = 'Host=localhost;Port=5434;Database=postgres;Username=postgres;Password=<PAYMENT_SIMULATOR_DB_PASSWORD från din .env>'
dotnet test tests/Booking.IntegrationTests/Booking.IntegrationTests.csproj
dotnet test tests/PaymentSimulator.IntegrationTests/PaymentSimulator.IntegrationTests.csproj
```

Användaren behöver rätt att skapa databaser. Tester skapar separata databaser med slumpade namn, applicerar migrationerna och tar bort sina egna databaser vid städning. Booking-testerna använder egna inventarie- och reservations-ID:n i Redis. Booking-integrationstester hoppas över om deras två miljövariabler saknas.

Tester täcker lyckat köp, misslyckande, timeout, utgången reservation, parallella försök, verifiering av bokning/belopp/valuta, spärrad direktbekräftelse, förlorat HTTP-svar, återbetalning efter avbokning/utgång och PostgreSQLs lagerkontroll. Simulatorns tester kontrollerar även återhämtning efter avbrutna anrop, idempotent återbetalning och migrering av det äldre databasschemat.
