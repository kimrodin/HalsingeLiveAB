# Granskning mot projektbeskrivning version 2

Uppdaterad: 2026-11-05. Granskad revision: `bf46b12` (tidigare granskning: `5f8bb91`).

Underlag: den tidigare granskningens kravgenomgång av **Projektbeskrivning Biljettplattform för evenemang Ver 2.docx**, inklusive arkitekturbilden. Uppdateringen jämför den med aktuell kod, ändringar sedan `5f8bb91`, dokumentation, lokal körmiljö samt GitHub-issues och CI. Kravdokumentet finns inte i den aktuella arbetskopian och har inte lästs om. Applikationskoden har inte ändrats.

**Bedömning:** Projektet har ett implementerat och testat köpflöde för biljettantal per sektion. Health-endpoints är nu implementerade i samtliga fyra domäntjänster. YARP-gatewayen finns och frontendtrafiken är omkopplad, men gatewayens egen tokenkontroll återstår. Kvarvarande krav omfattar numrerat stolval, e-postleverans, SignalR, k6 med lasttestrapport, Testcontainers, Serilog/Seq och en verifierad snabbstart. OpenAPI har tagits bort och behöver återinföras för att uppfylla det tidigare identifierade kravet.

Statusen **Finns** betyder att implementationen har hittats och granskats; genomförda körningar redovisas separat. **Delvis** betyder att en del av kravet finns. **Saknas** avser den aktuella arbetskopian. **Ej verifierat** används när underlaget kan finnas utanför repot eller kräver en demonstration.

## Slutfört och förändrat sedan föregående granskning

| Område | Aktuell bedömning |
| --- | --- |
| Health i alla fyra domäntjänster | **Slutfört i koden och verifierat i lokal körmiljö.** `/health`, `/health/live` och `/health/ready` finns. Readiness kontrollerar PostgreSQL samt Redis i Booking. Compose använder readiness och API-avbildningarna installerar `curl`. Se [health-guiden](health-checks.md). |
| YARP och frontendrouting | **Routing slutförd i konfigurationen; tokenkontroll återstår.** [ApiGateway](../src/ApiGateway/Program.cs) finns i lösningen och Compose. Båda webbapparnas Vite- och Nginx-proxyer skickar API-trafik dit. Backend-API:erna publiceras inte längre direkt i aktuell Compose. Den körande lokala miljön är äldre och saknar gatewaycontainern. |
| Dokumentation | **Utökad.** Guider för [Redis](redis.md), [frontend](frontend-guide.md), [TicketService](ticketservice-guide.md), [entréskanning](entreskanner-guide.md) och health har tillkommit. README beskriver gatewayen och rätt sökväg till Compose, men miljömall och flera rättelser återstår. |
| Meddelandevalidering | **Kompletterad.** [PaymentResultInbox](../src/services/Booking/features/payments/Results/PaymentResultInbox.cs) avvisar resultat där `CorrelationId` inte matchar `BookingId`. Två nya testfall täcker lyckat och misslyckat betalningsresultat. |
| Frontendvarning | **Åtgärdad.** `useEffect` i kundappens `HomePage.tsx` har rätt beroenden. Båda apparnas lint passerar utan varningar vid uppdateringen. |
| CI | **Verifierad externt.** [CI för `bf46b12`](https://github.com/kimrodin/HalsingeLiveAB/actions/runs/42013029687) är godkänd för .NET, båda frontendapparna och containerjobbet. Gatewayens Docker-image ingår ännu inte i containerjobbet. |
| OpenAPI | **Ny kravlucka.** Swashbuckle och Swagger-registreringar har tagits bort från alla fyra domän-API:er utan en ersättande OpenAPI-implementation. Äldre körande containrar svarar fortfarande på Swagger, vilket inte verifierar den aktuella koden. |

## Viktigaste luckorna

### 1. Kunden kan inte välja en bestämd numrerad stol

**Krav:** Kundens platsval och skydd mot att samma plats säljs två gånger, med Kulturhusets 600 numrerade platser i tre sektioner.

**Nuvarande lösning:** Kunden väljer sektion och antal. [CreateBookingRequest](../src/services/Booking/features/bookings/CreateBookingRequest.cs) innehåller `InventoryId` och `Quantity`. [BookingItem](../src/services/Booking/features/bookings/Models/BookingItem.cs), [BookingConfirmed](../src/Contracts/Messaging/BookingConfirmed.cs) och [Ticket](../src/services/TicketService/Features/Tickets/Models/Ticket.cs) saknar stol-ID och rad/stolnummer. Kundens [biljettval](../src/frontend/customer-web/src/features/bookings/components/EventTicketInventory.tsx) och [platskarta](../src/frontend/customer-web/src/features/venues/components/VenueSeatingMapViewer.tsx) ger inget val av enskild stol; kartan visar uppladdade bild- eller PDF-filer.

Katalogen har redan en `Seat`-modell och ett unikt index på sektion, rad och nummer i [EventCatalogDbContext](../src/services/EventCatalog/data/EventCatalogDbContext.cs). Den modellen är inte kopplad till köpflödet. `TicketNumberInBookingItem` är ett löpnummer inom bokningsraden och kan därför inte användas som stolnummer.

**Återstående arbete:** För stol-ID genom platsval, reservation, databasbekräftelse, meddelande och biljett. Skydda försäljning av samma stol för samma evenemang i PostgreSQL och testa samtidiga köp av just den stolen. Befintligt sektionsflöde kan fortsatt användas för fri placering.

### 2. YARP-routing finns, men gatewayens tokenkontroll återstår

**Krav:** Alla tre användargränssnitt ska anropa en gemensam YARP-gateway som routar till tjänsterna och kontrollerar tokens.

**Nuvarande lösning:** [ApiGateway](../src/ApiGateway/Program.cs) använder YARP 2.3.0. [Ruttkonfigurationen](../src/ApiGateway/appsettings.json) mappar `/api`, `/booking-api`, `/ticket-api` och `/payment-api` till respektive tjänst och tar bort prefixet. Båda webbapparnas Vite- och Nginx-proxyer använder `API_GATEWAY_PROXY_TARGET`. Gatewayen publiceras på `127.0.0.1:5212`, medan domän-API:ernas värdportar har tagits bort i aktuell [Compose](../src/Compose.yaml).

Gatewayen saknar `AddAuthentication`, `AddAuthorization` och motsvarande middleware/ruttpolicies. JWT- och rollkontroller finns fortfarande i tjänsterna. Att gatewayen införts skyddar därför inte de sedan tidigare oskyddade lager- och betalningsoperationerna. [Keycloak-guiden](keycloak.md) och [frontendarkitekturen](frontend-architecture.md) beskriver fortfarande den äldre proxyarkitekturen.

**Återstående arbete:** Komplettera gatewayen med tokenvalidering och policies för publika, gästauktoriserade, administrativa och interna anrop. Skydda interna skrivoperationer även vid tjänsteanrop, bevara kundens gästflöde och verifiera kundköp och skanning genom gatewayen i båda frontendlägena. Bygg även gatewayens Docker-image i CI.

### 3. Biljetterna skickas inte med e-post

**Krav:** Biljettjänsten skapar en PDF med QR-kod och skickar biljetten via e-post. Arkitekturbilden visar även Mailpit.

**Nuvarande lösning:** [TicketDocumentService](../src/services/TicketService/Features/Tickets/DownloadTicket/TicketDocumentService.cs) skapar PDF och QR-kod. Kunden hämtar biljetten på [bokningssidan](../src/frontend/customer-web/src/features/bookings/pages/BookingPage.tsx). Det finns ingen e-postavsändare, SMTP-konfiguration eller Mailpit-tjänst. Kundens e-postadress sparas i Booking, men ingår inte i [bekräftelsemeddelandet](../src/Contracts/Messaging/BookingConfirmed.cs) som TicketService tar emot.

Kundens åtkomsttoken sparas i `sessionStorage`. Bokningslänken ensam räcker därför inte för att återöppna bokningen på en annan enhet eller i en ny webbläsarsession. Detta gör den uteblivna e-postleveransen särskilt märkbar.

**Återstående arbete:** Ordna mottagaruppgifter och en tillförlitlig leveransväg med återförsök och skydd mot dubbla utskick. Lägg till Mailpit för lokal demonstration och verifiera hela leveransen.

### 4. SignalR för liveförsäljning saknas

**Krav:** SignalR för liveuppdateringar anges både i arkitekturen och bland de tekniska kraven.

**Nuvarande lösning:** Ingen SignalR-hubb eller klient finns. [Bokningssidan](../src/frontend/customer-web/src/features/bookings/pages/BookingPage.tsx) hämtar status med ungefär 1–2 sekunders intervall. Dashboardens [försäljning](../src/frontend/admin-web/src/features/dashboard/components/SalesStatisticsSection.tsx) och [beläggning](../src/frontend/admin-web/src/features/dashboard/components/OccupancySection.tsx) uppdateras med 60 sekunders intervall. Biljettlagret på evenemangssidan har inget motsvarande kontinuerligt uppdateringsintervall.

**Återstående arbete:** Publicera relevanta lager- och försäljningsändringar till en SignalR-hubb, anslut klienterna och hantera återanslutning och ny hämtning av aktuell status.

### 5. k6-container och lasttestrapport saknas

**Krav:** k6-lasttest i egen container och en levererad lasttestrapport.

**Nuvarande lösning:** Inga k6-skript, någon k6-tjänst eller någon lasttestrapport hittades. Däremot finns relevanta samtidighetstester: [BookingPaymentHttpTests](../tests/Booking.IntegrationTests/BookingPaymentHttpTests.cs) tävlar med två respektive 200 HTTP-anrop om sista platsen. [BookingPaymentTests](../tests/Booking.IntegrationTests/BookingPaymentTests.cs) testar även att PostgreSQL stoppar överförsäljning när Redis avsiktligt visar för stort saldo. Dessa tester gick igenom vid den tidigare granskningen; de har inte körts om lokalt i denna uppdatering.

De bevisar bestämda korrekthetsegenskaper i testmiljön. De mäter inte hela den containeriserade plattformens svarstider, kapacitet eller felandel under ett biljettsläpp.

**Återstående arbete:** Skapa k6-scenarier för det externa köpflödet och sista tillgängliga platsen, kör dem i container och dokumentera belastning, miljö, svarstider, felutfall och kontroll av sålda platser mot kapaciteten.

### 6. Health är slutfört; Serilog och Seq saknas

**Krav:** `/health` per tjänst och strukturerad loggning med Serilog till Seq.

**Nuvarande lösning:** Alla fyra tjänster exponerar `/health`, `/health/live` och `/health/ready` med JSON och statuskoder för hälsa. Readiness använder `AddDbContextCheck`; Booking kontrollerar även Redis med `PING`. Liveness kontrollerar att API:t kan svara utan beroendekontroller. Compose kontrollerar `/health/ready` var femtonde sekund. Gatewayen har dessutom en enkel `/health` som inte testar backendtjänsterna. Lokala läsanrop vid uppdateringen gav:

| API i den befintliga körmiljön | `/health` | `/health/live` | `/health/ready` |
| --- | --- | --- | --- |
| EventCatalog, port 8080 | 200 | 200 | 200 |
| Booking, port 8081 | 200 | 200 | 200 |
| TicketService, port 8082 | 200 | 200 | 200 |
| PaymentSimulator, port 5111 | 200 | 200 | 200 |

Portarna i tabellen hör till äldre körande containrar; aktuell Compose exponerar API:erna genom gatewayen. Databaskontrollerna, och Redis i Booking, rapporterade `Healthy`. Avbrott och HTTP 503 har inte provats i denna uppdatering. Readiness kontrollerar inte RabbitMQ, Keycloak eller andra API:er. Kod använder `ILogger` med namngivna parametrar, men Serilog-paket, Seq-konfiguration och Seq-container saknas.

**Återstående arbete:** Inför Serilog och Seq med tjänstenamn och korrelationsuppgifter som strukturerade fält. Health-kravet räknas som implementerat; verifiering av felstatus kan kompletteras med isolerade beroendeavbrott.

### 7. Startinstruktionerna fungerar inte för en ren klon som de är skrivna

**Krav:** Hela systemet ska starta med ett `docker compose up`, och README ska förklara starten på under fem minuter.

**Nuvarande lösning:** [Startguiden](README.MD) instruerar läsaren att kopiera `.env.example` till `.env` och förlitar sig på att `COMPOSE_FILE` då väljer rätt filer. `.env.example` saknas i arbetskopian och bland de spårade filerna. Det finns inte heller någon Compose-fil eller README i projektets rot; Compose-filerna ligger under `src`. Grundkonfigurationen kräver dessutom `TICKET_SERVICE_INTERNAL_API_KEY`.

Dokumentationen innehåller flera kvarvarande uppgifter från tidigare implementationer:

- README säger att EventCatalog normalt kräver manuell migration och föreslår att lägga till migrationskod. Compose sätter redan `InitializeDatabase=true`, och API:t har motsvarande migrationskod.
- README hänvisar till `seed-seating-maps.sql`, som saknas.
- README säger att platskartor måste placeras manuellt tills fillagring finns. Uppladdning och lokal fillagring finns redan.
- [Beläggningsdokumentationen](dashboard-occupancy.md) beskriver `.env` under `src`, medan README beskriver projektroten.
- [Betalningssimulatorns guide](payment-simulation-admin.md), [frontendarkitekturen](frontend-architecture.md), [frontendguiden](frontend-guide.md) och [Keycloak-guiden](keycloak.md) beskriver direktproxyer eller gamla miljövariabler. YARP ansvarar nu för routning och prefixborttagning; adminwebbens Nginx har betalningsproxy.
- [Health-guidens](health-checks.md) porttabell använder fortfarande de borttagna direkta API-portarna från den tidigare Compose-konfigurationen.

`docs/redis.md` finns nu och är inte längre en dokumentationslucka. README:s nya gatewayavsnitt är användbart, men löser inte den saknade miljömallen eller motsägelserna i första-start-instruktionerna.

**Återstående arbete:** Lägg till en ofarlig miljömall utan riktiga hemligheter, bestäm en enda dokumenterad startväg och rätta den inaktuella dokumentationen. Verifiera därefter instruktionerna i en ren klon. Den befintliga körmiljön visar inte att en ny installation klarar femminuterskravet.

### 8. OpenAPI saknas i aktuell kod

**Krav:** OpenAPI för varje domän-API enligt den tidigare kravgenomgången.

**Nuvarande lösning:** `Swashbuckle.AspNetCore`, `AddSwaggerGen`, `UseSwagger` och `UseSwaggerUI` har tagits bort från samtliga fyra API:er. Ingen ersättande `AddOpenApi`/`MapOpenApi` hittades. HTTP 200 från Swagger i de äldre lokala containrarna avser därför en tidigare implementation.

**Återstående arbete:** Tillhandahåll ett maskinläsbart OpenAPI-dokument per domäntjänst som beskriver aktuella kontrakt, svarskoder och autentisering. Ett särskilt Swagger-UI är inget separat krav i denna bedömning.

## Kravöversikt

| Krav i projektbeskrivningen | Status | Bedömning och belägg |
| --- | --- | --- |
| .NET 8 eller senare och ASP.NET Core Web API | Finns | Alla fyra domän-API:er samt gatewayen använder .NET 10. |
| Blazor eller React för gränssnitten | Finns | Två separata React-appar för kund och administration. React är ett tillåtet val. |
| Kundwebb för att hitta evenemang | Finns | Sökning, evenemangslista, detaljsida och information om scenen i [kundappen](../src/frontend/customer-web/src/features/events/pages/HomePage.tsx). |
| Administration av evenemang, scener och sektioner | Finns | API:er och formulär för att skapa, uppdatera och radera. Kapacitetsregler och skydd för använda sektioner finns. |
| Kulturhuset 600/tre sektioner, Studion 150, festival 2 000 | Finns | Motsvarande definitioner finns i [SeedVenuesHandler](../src/services/EventCatalog/features/Seed/SeedVenues/SeedVenuesHandler.cs). Detta innebär inte att stolval är implementerat. |
| Platskartor | Delvis | Uppladdning och visning av bild/PDF finns. Interaktivt val av numrerad stol saknas. |
| Priskategorier | Delvis | Pris per evenemang och sektion finns. Ingen fristående modell för exempelvis vuxen/barn/student har hittats; dokumentet preciserar inte om sådana kategorier krävs. Behöver stämmas av med produktägaren. |
| Reservera platser i tio minuter | Finns för sektionsantal | [BookingService](../src/services/Booking/features/bookings/BookingService.cs) använder tio minuter. Redis hanterar antalet och tidsgränsen. |
| Redis-reservationer med utgångstid | Finns med annan teknikdetalj | [RedisReservationService](../src/services/Booking/features/bookingholds/RedisReservationService.cs) använder Lua och ett sorterat tidsindex. Frisläppning sker vid tillgänglighetsanrop eller genom ett bakgrundsjobb med 15 sekunders intervall. Själva reservationshashen använder inte Redis-TTL. |
| Skydd i databasen mot dubbelförsäljning | Finns för sektionsantal | Villkorad lageruppdatering, transaktion och bokningslås i PostgreSQL. Ett stolbundet skydd kan inte verifieras eftersom köpet saknar stol-ID. |
| Val av bestämda numrerade platser | Saknas | Bokning och biljett hanterar sektion och antal. Se lucka 1. |
| Saga-liknande reservera/betala/bekräfta eller släpp | Finns | Meddelandeflöde, kompensation, återbetalning och avstämning. Se [StartPayment](../src/services/Booking/features/bookings/Payments/StartPayment.cs). |
| Betalsimulator med lyckat, misslyckat och timeout | Finns | Konfigurerbara och lagrade utfall. Även slumpat utfall och admininställningar finns. |
| RabbitMQ mellan tjänsterna | Finns | `PaymentRequested`, `PaymentSucceeded`, `PaymentFailed` och `BookingConfirmed` är inkopplade. Se [meddelandedokumentationen](messaging.md). |
| `SeatsHeld` vid reservation | Saknas | Ingen sådan kontraktstyp, publicering eller konsumtion hittades. Publiceringen anges uttryckligen i dokumentets bokningsflöde. |
| `TicketScanned` i arkitekturbilden | Saknas | Skanning ändrar biljettstatus via HTTP. Ingen sådan händelse hittades. Detta är en avvikelse från bilden; flödestexten kräver framför allt att biljetten markeras använd. |
| Biljetter skapas vid `BookingConfirmed` | Finns | [BookingConfirmedConsumer](../src/services/TicketService/messaging/Issuance/BookingConfirmedConsumer.cs) och idempotent utfärdande med unikt databasindex finns. |
| PDF med QR-kod, QuestPDF och QRCoder | Finns | [TicketDocumentService](../src/services/TicketService/Features/Tickets/DownloadTicket/TicketDocumentService.cs), verifierat av tester. |
| Biljett skickas via e-post | Saknas | PDF hämtas i kundwebben. Mailpit från arkitekturbilden saknas också. |
| Entréskanner och engångsanvändning | Finns, integrerad i adminappen | Kamera och manuell inmatning i [TicketScannerPage](../src/frontend/admin-web/src/features/check-in/pages/TicketScannerPage.tsx). Eventkontroll och optimistisk samtidighetskontroll skyddar statusändringen i TicketService. |
| YARP som gemensam ingång och tokenkontroll | Delvis | Gateway, fyra rutter, frontendproxyer och intern åtkomst till API:erna finns i Compose. Tokenkontroll i gatewayen saknas. Se lucka 2. |
| Keycloak med OIDC | Finns | Realm, adminroll, PKCE och JWT-validering finns. Kundflödet använder gästtoken. |
| SignalR för liveuppdateringar | Saknas | Polling används i befintliga vyer. |
| Egen PostgreSQL-databas per domäntjänst | Finns | Fyra separata databastjänster och EF Core-kontexter. Keycloaks lokala utvecklingsdatabas är separat från detta krav. |
| Multi-stage Dockerfile per tjänst | Finns | Alla fyra domän-API:er, gatewayen och båda webbapparna har separata bygg- och körsteg. |
| Start av hela systemet med Docker Compose | Delvis | Samlad konfiguration finns och validerar med en tillförd testnyckel. Den dokumenterade starten från en ren rotkatalog är ofullständig. |
| Konfiguration via `.env`, inga hemligheter i repot | Delvis | Biljettjänstens interna nyckel krävs från miljön och `.env` ignoreras. Miljömall saknas; Compose och realm innehåller fasta utvecklingsuppgifter. Ingen fullständig granskning av historiska hemligheter har gjorts. |
| xUnit-enhetstester för bokningsregler | Finns | Bokningsvalidering och flera övriga tjänsteregler är testade. Resultat nedan. |
| Integrationstest med Testcontainers | Annorlunda | Integrationstester använder verklig PostgreSQL, Redis och RabbitMQ, men infrastrukturen startas med Compose/CI-tjänster. Testcontainers används inte. |
| k6 i egen container och lasttestrapport | Saknas | Samtidighetsregressioner finns, men ingen leverans av föreskrivet lasttest hittades. |
| CI på pull requests med bygge, tester och Docker-images | Finns, med komplettering för gateway | [GitHub Actions](../.github/workflows/ci.yml) bygger/testar .NET, kontrollerar frontend och bygger fyra domän-API-images. [Körningen för `bf46b12`](https://github.com/kimrodin/HalsingeLiveAB/actions/runs/42013029687) är godkänd. Gatewayen byggs i lösningen men dess Docker-image saknar CI-steg. |
| `/health` i varje tjänst | Finns | Alla fyra domäntjänster har health, liveness och readiness, även verifierat med lokala läsanrop. Gatewayen har en separat enkel health-route. |
| Serilog till Seq | Saknas | Befintlig `ILogger`-loggning uppfyller inte det uttryckliga teknikvalet. |
| OpenAPI för varje API | Saknas i aktuell kod | Swashbuckle och Swagger har tagits bort utan ersättning. Äldre körande containrar svarar fortfarande 200; det är inte belägg för aktuell revision. |
| README med arkitektur och snabbstart | Delvis | Flera guider och ett meddelandediagram finns, men startguiden är ofullständig och inaktuell. |
| Scrum-tavla, stories, poäng och sprintmål | Delvis verifierat | GitHub har stories och acceptanskriterier, exempelvis [#26](https://github.com/kimrodin/HalsingeLiveAB/issues/26) och [#27](https://github.com/kimrodin/HalsingeLiveAB/issues/27). Tavla, poäng och sprintmål har inte verifierats. |
| Anteckningar från granskningar och retrospektiv | Ej verifierat | Inga sådana artefakter hittades i repot. |
| Granskade PR:er och PO-godkända acceptanskriterier | Delvis verifierat | Mergade PR:er och issues med acceptanskriterier finns i GitHub. Formell kodgranskning och produktägarens godkännande är inte fastställda genom denna uppdatering. |
| Slutdemo vecka 7 och individuell reflektion | Ej verifierat | Demo har inte observerats och reflektionsdokument hittades inte i arbetskopian. |

## Ytterligare iakttagelser i befintliga flöden

Dessa är slutsatser från koden. De har inte reproducerats genom att ändra den befintliga körmiljöns kund- eller evenemangsdata.

1. **Säljstart kan kringgås via boknings-API:t.** Kundwebben använder `onSaleFrom` för att aktivera köpknappen. [CreateReservationAsync](../src/services/Booking/features/bookings/BookingService.cs) kontrollerar inte säljstarten, och [EventCatalogClient](../src/services/Booking/features/eventcatalog/EventCatalogClient.cs) tar inte med `OnSaleFrom` i sitt interna kontrakt. Ett direkt anrop med befintligt lager-ID kan därmed reservera före den säljstart som visas i UI:t. Lägg affärsregeln även på servern inför ett demonstrerat biljettsläpp.

2. **Prisändringar i katalogen förs inte vidare till bokningslagret.** [UpdateEventHandler](../src/services/EventCatalog/features/events/UpdateEvent/UpdateEventHandler.cs) ändrar `EventSectionPrice`. Booking använder sitt eget `EventInventory.Price` när en ny reservation skapas. Priset kopieras från katalogen när lagret skapas; någon synkronisering vid senare prisändring hittades inte. En ändring från exempelvis 200 till 250 kronor kan därför lämna nya köp på det tidigare lagerpriset. Bestäm vad som ska gälla för nya respektive redan reserverade köp och synkronisera därefter.

3. **Misslyckad lagerskapning vid nytt evenemang kan döljas för administratören.** [EventsController](../src/services/EventCatalog/features/events/EventsController.cs) sparar evenemanget först och gör därefter upp till tre HTTP-försök att skapa lager. Efter misslyckande returneras fortfarande 201 med `InventoryProvisioned=false`. Frontendens [CreateEventResponseDto](../src/frontend/admin-web/src/features/events/api/events.ts) saknar det fältet, och [EventFormPage](../src/frontend/admin-web/src/features/events/pages/EventFormPage.tsx) behandlar svaret som framgång. Ingen varaktig återförsökskö för just denna operation hittades. Visa lagerfelet och ge en återställningsväg, eller gör provisioneringen beständig.

4. **Vissa skrivande API:er saknar fortfarande åtkomstkontroll.** [EventInventoryController](../src/services/Booking/features/eventinventory/EventInventoryController.cs) kräver ingen `Authorize` för att skapa lager. [PaymentsController](../src/services/PaymentSimulator/features/payments/PaymentController.cs) saknar motsvarande kontroll för betalningsstart, avbryt och återbetalning. Aktuell Compose tar bort direkta API-portar, men gatewayens breda rutter vidarebefordrar dessa anrop utan egen behörighetskontroll. Skydda lageradministration och interna betalningsoperationer med avsedd behörighet eller tjänsteautentisering. TicketServices interna nyckelkontroll och befintliga adminpolicies i andra endpoints behöver bedömas var för sig.

## Val som är tillåtna eller som utökar beskrivningen

- **React och .NET 10:** följer alternativet React och versionskravet .NET 8 eller senare.
- **RabbitMQ.Client i stället för MassTransit:** ett tillåtet val enligt meddelandekravet. Avsaknad av MassTransit är därför ingen separat kravlucka.
- **Två frontendappar med skannern i adminappen:** alla tre gränssnitt finns funktionellt, även om skannern inte är en tredje separat app eller container. Skannern använder adminappens proxyväg, som nu är omkopplad till gatewayen.
- **Gästbokning:** kunden får en slumpad åtkomsttoken vars hash lagras i databasen. Projektbeskrivningen kräver inte uttryckligen kundinloggning, så detta har inte räknats som en saknad funktion.
- **Outbox, inbox, återförsök, dead-letter-köer och avstämning:** konkret stöd för det saga-liknande flödet. Sena lyckade betalningar kan återbetalas när reservationen inte kan bekräftas. Kombinationen av händelser och HTTP för uppslag/kompensation har granskats som en implementation av flödet, inte som avsaknad av RabbitMQ.
- **Statistik, beläggningsgrafer, evenemangsbilder, seedverktyg och inställning av betalningsutfall i admin:** tillägg utöver de tydligt specificerade kärnleverablerna. De fyller inte de kvarvarande kraven på stolval, gatewayens tokenkontroll, e-post eller lasttest.

## Verifiering vid uppdateringen

- Jämförelse av `5f8bb91..bf46b12` och granskning av gateway, frontendproxyer, health, Compose, dokumentation och kvarvarande kravluckor.
- `dotnet build src/ApiGateway/ApiGateway.csproj --no-restore`: godkänt, inga varningar eller fel.
- `dotnet test tests/Booking.UnitTests/Booking.UnitTests.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false`: **66 godkända, 0 misslyckade, 0 överhoppade**, inklusive de två nya korrelationsfallen. TRX finns lokalt i `artifacts/project-review-update-test-results/`. Körningen slutfördes utanför sandlådan efter att ett första försök fastnat utan testresultat.
- `npm run lint` i båda webbapparna: godkänt utan varningar.
- Grund-Compose samt grund- och utvecklings-Compose tillsammans: `config --quiet` godkänt med en tillfälligt tillförd testnyckel. Ingen lokal `.env` ändrades.
- Tolv läsanrop till domäntjänsternas health-endpoints: samtliga HTTP 200; PostgreSQL och Bookings Redis rapporterade `Healthy`.
- `docker ps`: fyra friska API-containrar, men ingen `api-gateway`. Den befintliga miljön använder fortfarande direkta API-portar och Swagger. Gatewayen svarade inte på port 5212. Inga containrar byggdes om eller verksamhetsdata ändrades.
- [CI-körning 42013029687](https://github.com/kimrodin/HalsingeLiveAB/actions/runs/42013029687) för exakt `bf46b12`: samtliga fyra jobb godkända (.NET, två frontendjobb och containerjobb). CI-resultatet ersätter inte en demonstration av det nya gatewayflödet.
- Befintliga GitHub-issues granskades som underlag för nya ärenden. Stängda issues räknas inte ensamma som bevis för att kvarvarande krav är implementerade; exempelvis verifierar sektionsbaserade tester inte köp av en bestämd stol.
- De åtta nya ärendena lästes tillbaka från GitHub: samtliga är öppna och innehåller sammanlagt 52 acceptanskriterier. Länkar finns sist i dokumentet.

## Tidigare verifiering (historik från `5f8bb91`)

Nedanstående resultat är bevarade från den tidigare granskningen och utgör inte en ny lokal körning av hela testsamlingen. Då byggdes och kördes testerna med .NET SDK 10.0.103 mot tre tillfälliga, isolerade containrar med PostgreSQL 18, Redis 7 och RabbitMQ 4. Testerna fick egna anslutningar och använde inte applikationens databaser eller meddelandeköer.

| Testprojekt | Godkända | Misslyckade | Överhoppade |
| --- | ---: | ---: | ---: |
| Booking.UnitTests | 64 | 0 | 0 |
| EventCatalog.UnitTests | 24 | 0 | 0 |
| PaymentSimulator.UnitTests | 47 | 0 | 0 |
| TicketService.UnitTests | 101 | 0 | 0 |
| Booking.IntegrationTests | 38 | 0 | 0 |
| EventCatalog.IntegrationTests | 31 | 0 | 0 |
| PaymentSimulator.IntegrationTests | 30 | 0 | 0 |
| TicketService.IntegrationTests | 15 | 0 | 0 |
| **Totalt .NET** | **350** | **0** | **0** |

Integrationstesterna omfattar bland annat samtidiga reservations- och betalningsförsök, återleverans av meddelanden, databasavbrott, kompensation och konkurrerande biljettstatusändringar. TRX-resultaten finns lokalt under `artifacts/project-review-test-results/` och ignoreras av Git.

Övriga kontroller vid den tidigare granskningen:

- `npm run lint` i båda apparna: inga fel. Kundappen gav en varning om en saknad `useEffect`-dependency i `HomePage.tsx`.
- `npm run build` i båda apparna: godkänt. Vite varnade för stora JavaScript-paket.
- Adminappens två datumtester: båda godkända.
- Grund- och utvecklings-Compose validerades tillsammans med `config --quiet` och en tillfälligt tillförd testnyckel.
- Dåvarande läsanrop verifierade Swagger; `/health` fanns vid den tidpunkten endast i TicketService.
- NuGet gav då `NU1903` för `Microsoft.OpenApi` 2.3.0. Swagger-beroendena har senare tagits bort. Den gamla varningen har inte räknats som ett verifierat aktuellt fel; någon ny fullständig beroendesäkerhetsgranskning har inte gjorts.

Frontendbyggena och .NET-testerna i den tidigare granskningen slutfördes utanför sandlådan efter lokala processbegränsningar. Alla fyra API:er byggdes genom testprojekten; Docker-images byggdes inte om lokalt. En nyinstallation på ren maskin, interaktiv kameraskanning och fullständigt köp genom webbläsaren har fortfarande inte verifierats. Extern CI har nu kontrollerats enligt avsnittet ovan. Godkända tester gäller de befintliga testfallen och bevisar inte att alla projektkrav är uppfyllda.

## Föreslagen ordning för återstående arbete

1. Komplettera stolmodellen genom hela köpet och verifiera att samma numrerade stol bara kan säljas en gång. Behåll de befintliga testerna för sektionsantal.
2. Slutför token- och åtkomstkontrollen i den befintliga gatewayarkitekturen. Lägg säljstartsregeln på servern och hantera prissynkronisering och misslyckad lagerskapning.
3. Lägg till e-postleverans med Mailpit samt SignalR för liveförsäljning.
4. Komplettera händelserna: `SeatsHeld` enligt flödestexten och ta ställning till `TicketScanned` i arkitekturbilden.
5. Lägg till Serilog/Seq, återinför OpenAPI och komplettera med Testcontainers. Behåll befintliga health-endpoints och lägg till gatewayens Docker-image i CI.
6. Rätta snabbstart och miljömall, verifiera en ren installation inklusive gatewayflödet och kör k6 med en sparad lasttestrapport.
7. Samla länkar eller filer för Scrum-tavla, sprintmål, granskningar, retrospektiv, PO-acceptans, slutdemo och individuella reflektioner. Markera sådant som redan finns externt som verifierat när underlaget har granskats.

## Skapade issues med acceptanskriterier

Följande åtta prioriterade ärenden har skapats utifrån uppdateringen. Varje ärende har en user story, nuläge, föreslagen prioritet, 6–7 testbara acceptanskriterier och anvisning för verifiering. Prioritet och kriterier är underlag för produktägarens godkännande; de är inte markerade som PO-godkända.

| Issue | Föreslagen prioritet | Omfattning |
| --- | --- | --- |
| [#122 – Numrerade stolar](https://github.com/kimrodin/HalsingeLiveAB/issues/122) | P1 | Stolval genom hela köpet och samtidighetsskydd för samma stol. |
| [#123 – Tokenkontroll och interna operationer](https://github.com/kimrodin/HalsingeLiveAB/issues/123) | P1 | Komplettera befintlig YARP, skydda skrivoperationer och bygg gatewayens image i CI. |
| [#124 – E-post med Mailpit](https://github.com/kimrodin/HalsingeLiveAB/issues/124) | P1 | PDF-leverans, beständiga leveransjobb och återförsök. |
| [#125 – SignalR](https://github.com/kimrodin/HalsingeLiveAB/issues/125) | P2 | Liveuppdaterad tillgänglighet, bokningsstatus och försäljning. |
| [#126 – k6 och lasttestrapport](https://github.com/kimrodin/HalsingeLiveAB/issues/126) | P1 | Containeriserat lasttest genom gatewayen med reproducerbara resultat. |
| [#127 – Serilog till Seq](https://github.com/kimrodin/HalsingeLiveAB/issues/127) | P2 | Strukturerad loggning och korrelation genom köpflödet. |
| [#128 – Verifierad snabbstart](https://github.com/kimrodin/HalsingeLiveAB/issues/128) | P1 | Miljömall, aktuella guider och start från ren klon. |
| [#129 – OpenAPI](https://github.com/kimrodin/HalsingeLiveAB/issues/129) | P2 | Återinför och validera kontrakt för de fyra domän-API:erna. |

Urvalet är inte hela återstående backloggen. Säljstart, prissynkronisering, fel vid lagerprovisionering, Testcontainers, kompletterande meddelanden och processunderlag behöver fortsatt hanteras enligt iakttagelserna och arbetsordningen ovan.
