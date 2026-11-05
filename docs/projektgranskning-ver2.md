# Granskning mot projektbeskrivning version 2

Datum: 2026-11-05. Granskad revision: `5f8bb91`.

Underlag: **Projektbeskrivning Biljettplattform för evenemang Ver 2.docx**, inklusive arkitekturbilden. Dokumentet har använts som kravunderlag. Granskningen omfattar repositoryts tjänster, klienter, datamodeller och migrationer, meddelandeflöden, tester, Docker-konfiguration, CI och dokumentation. Applikationskoden har inte ändrats.

**Bedömning:** Projektet har ett implementerat och testat köpflöde för biljettantal per sektion. Det återstår flera uttryckliga krav innan hela projektbeskrivningen är uppfylld: val av numrerade stolar, YARP-gateway, e-postleverans, SignalR, k6 med lasttestrapport, Testcontainers samt delar av observerbarheten och startdokumentationen.

Statusen **Finns** betyder att implementationen har hittats och granskats; genomförda körningar redovisas separat. **Delvis** betyder att en del av kravet finns. **Saknas** avser den aktuella arbetskopian. **Ej verifierat** används när underlaget kan finnas utanför repot eller kräver en demonstration.

## Viktigaste luckorna

### 1. Kunden kan inte välja en bestämd numrerad stol

**Krav:** Kundens platsval och skydd mot att samma plats säljs två gånger, med Kulturhusets 600 numrerade platser i tre sektioner.

**Nuvarande lösning:** Kunden väljer sektion och antal. [CreateBookingRequest](../src/services/Booking/features/bookings/CreateBookingRequest.cs) innehåller `InventoryId` och `Quantity`. [BookingItem](../src/services/Booking/features/bookings/Models/BookingItem.cs), [BookingConfirmedV1](../src/Contracts/Messaging/BookingConfirmedV1.cs) och [Ticket](../src/services/TicketService/Features/Tickets/Models/Ticket.cs) saknar stol-ID och rad/stolnummer. Kundens [biljettval](../src/frontend/customer-web/src/features/bookings/components/EventTicketInventory.tsx) och [platskarta](../src/frontend/customer-web/src/features/venues/components/VenueSeatingMapViewer.tsx) ger inget val av enskild stol; kartan visar uppladdade bild- eller PDF-filer.

Katalogen har redan en `Seat`-modell och ett unikt index på sektion, rad och nummer i [EventCatalogDbContext](../src/services/EventCatalog/data/EventCatalogDbContext.cs). Den modellen är inte kopplad till köpflödet. `TicketNumberInBookingItem` är ett löpnummer inom bokningsraden och kan därför inte användas som stolnummer.

**Återstående arbete:** För stol-ID genom platsval, reservation, databasbekräftelse, meddelande och biljett. Skydda försäljning av samma stol för samma evenemang i PostgreSQL och testa samtidiga köp av just den stolen. Befintligt sektionsflöde kan fortsatt användas för fri placering.

### 2. YARP-gateway som enda ingång saknas

**Krav:** Alla tre användargränssnitt ska anropa en gemensam YARP-gateway som routar till tjänsterna och kontrollerar tokens.

**Nuvarande lösning:** Det finns inget gatewayprojekt eller någon YARP-konfiguration. Varje webbapp har egna proxyregler i Vite och Nginx. Se [adminwebbens Dockerfile](../src/frontend/admin-web/Dockerfile), [kundwebbens Dockerfile](../src/frontend/customer-web/Dockerfile) och [frontendarkitekturen](frontend-architecture.md). API:erna publiceras dessutom på separata värdportar i [Compose](../src/Compose.yaml).

Keycloak/OIDC finns. JWT- och rollkontroller ligger i tjänsterna i stället för i en gemensam gateway. [Keycloak-guiden](keycloak.md) beskriver uttryckligen lösningen utan gateway.

**Återstående arbete:** Lägg till YARP med gemensamma rutter och avsedd tokenkontroll. Låt webbapparnas API-trafik gå dit och begränsa tjänsternas direkta åtkomst enligt den valda arkitekturen.

### 3. Biljetterna skickas inte med e-post

**Krav:** Biljettjänsten skapar en PDF med QR-kod och skickar biljetten via e-post. Arkitekturbilden visar även Mailpit.

**Nuvarande lösning:** [TicketDocumentService](../src/services/TicketService/Features/Tickets/DownloadTicket/TicketDocumentService.cs) skapar PDF och QR-kod. Kunden hämtar biljetten på [bokningssidan](../src/frontend/customer-web/src/features/bookings/pages/BookingPage.tsx). Det finns ingen e-postavsändare, SMTP-konfiguration eller Mailpit-tjänst. Kundens e-postadress sparas i Booking, men ingår inte i [bekräftelsemeddelandet](../src/Contracts/Messaging/BookingConfirmedV1.cs) som TicketService tar emot.

Kundens åtkomsttoken sparas i `sessionStorage`. Bokningslänken ensam räcker därför inte för att återöppna bokningen på en annan enhet eller i en ny webbläsarsession. Detta gör den uteblivna e-postleveransen särskilt märkbar.

**Återstående arbete:** Ordna mottagaruppgifter och en tillförlitlig leveransväg med återförsök och skydd mot dubbla utskick. Lägg till Mailpit för lokal demonstration och verifiera hela leveransen.

### 4. SignalR för liveförsäljning saknas

**Krav:** SignalR för liveuppdateringar anges både i arkitekturen och bland de tekniska kraven.

**Nuvarande lösning:** Ingen SignalR-hubb eller klient finns. [Bokningssidan](../src/frontend/customer-web/src/features/bookings/pages/BookingPage.tsx) hämtar status med ungefär 1–2 sekunders intervall. Dashboardens [försäljning](../src/frontend/admin-web/src/features/dashboard/components/SalesStatisticsSection.tsx) och [beläggning](../src/frontend/admin-web/src/features/dashboard/components/OccupancySection.tsx) uppdateras med 60 sekunders intervall. Biljettlagret på evenemangssidan har inget motsvarande kontinuerligt uppdateringsintervall.

**Återstående arbete:** Publicera relevanta lager- och försäljningsändringar till en SignalR-hubb, anslut klienterna och hantera återanslutning och ny hämtning av aktuell status.

### 5. k6-container och lasttestrapport saknas

**Krav:** k6-lasttest i egen container och en levererad lasttestrapport.

**Nuvarande lösning:** Inga k6-skript, någon k6-tjänst eller någon lasttestrapport hittades. Däremot finns relevanta samtidighetstester: [BookingPaymentHttpTests](../tests/Booking.IntegrationTests/BookingPaymentHttpTests.cs) tävlar med två respektive 200 HTTP-anrop om sista platsen. [BookingPaymentTests](../tests/Booking.IntegrationTests/BookingPaymentTests.cs) testar även att PostgreSQL stoppar överförsäljning när Redis avsiktligt visar för stort saldo. Dessa tester gick igenom vid granskningen.

De bevisar bestämda korrekthetsegenskaper i testmiljön. De mäter inte hela den containeriserade plattformens svarstider, kapacitet eller felandel under ett biljettsläpp.

**Återstående arbete:** Skapa k6-scenarier för det externa köpflödet och sista tillgängliga platsen, kör dem i container och dokumentera belastning, miljö, svarstider, felutfall och kontroll av sålda platser mot kapaciteten.

### 6. Observerbarheten är ofullständig

**Krav:** `/health` per tjänst och strukturerad loggning med Serilog till Seq.

**Nuvarande lösning:** Bara [TicketService](../src/services/TicketService/Program.cs) exponerar `/health`. Det endpointet returnerar ett fast svar utan kontroll av databas eller RabbitMQ. EventCatalog, Booking och PaymentSimulator saknar `/health`. Lokala läsanrop gav följande resultat:

| API | `/health` | `/swagger/v1/swagger.json` |
| --- | --- | --- |
| EventCatalog, port 8080 | 404 | 200 |
| Booking, port 8081 | 404 | 200 |
| TicketService, port 8082 | 200 | 200 |
| PaymentSimulator, port 5111 | 404 | 200 |

Kod använder `ILogger` med namngivna parametrar, men Serilog-paket, Seq-konfiguration och Seq-container saknas. Infrastrukturens egna Compose-healthchecks ersätter inte API-tjänsternas health-endpoints.

**Återstående arbete:** Lägg till health-endpoints i alla fyra tjänster samt Serilog och Seq. Skilj gärna processens livstecken från beredskap att hantera trafik när beroenden är otillgängliga.

### 7. Startinstruktionerna fungerar inte för en ren klon som de är skrivna

**Krav:** Hela systemet ska starta med ett `docker compose up`, och README ska förklara starten på under fem minuter.

**Nuvarande lösning:** [Startguiden](README.MD) instruerar läsaren att kopiera `.env.example` till `.env` och förlitar sig på att `COMPOSE_FILE` då väljer rätt filer. `.env.example` saknas i arbetskopian och bland de spårade filerna. Det finns inte heller någon Compose-fil eller README i projektets rot; Compose-filerna ligger under `src`. Grundkonfigurationen kräver dessutom `TICKET_SERVICE_INTERNAL_API_KEY`.

Dokumentationen innehåller flera kvarvarande uppgifter från tidigare implementationer:

- README säger att EventCatalog normalt kräver manuell migration och föreslår att lägga till migrationskod. Compose sätter redan `InitializeDatabase=true`, och API:t har motsvarande migrationskod.
- README hänvisar till `seed-seating-maps.sql`, som saknas.
- README säger att platskartor måste placeras manuellt tills fillagring finns. Uppladdning och lokal fillagring finns redan.
- [Beläggningsdokumentationen](dashboard-occupancy.md) beskriver `.env` under `src`, medan README beskriver projektroten.
- [Betalningssimulatorns guide](payment-simulation-admin.md) säger att produktions-Nginx saknar betalningsproxy, men adminwebbens Dockerfile innehåller den.
- IDE-fliken `docs/redis.md` som nämndes i frågan motsvarar ingen fil i aktuell arbetskopia.

**Återstående arbete:** Lägg till en ofarlig miljömall utan riktiga hemligheter, bestäm en enda dokumenterad startväg och rätta den inaktuella dokumentationen. Verifiera därefter instruktionerna i en ren klon. Den befintliga körmiljön visar inte att en ny installation klarar femminuterskravet.

## Kravöversikt

| Krav i projektbeskrivningen | Status | Bedömning och belägg |
| --- | --- | --- |
| .NET 8 eller senare och ASP.NET Core Web API | Finns | Alla fyra API:er använder .NET 10. |
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
| Saga-liknande reservera/betala/bekräfta eller släpp | Finns | Meddelandeflöde, kompensation, återbetalning och avstämning. Se [StartPayment](../src/services/Booking/features/bookings/StartPayment.cs). |
| Betalsimulator med lyckat, misslyckat och timeout | Finns | Konfigurerbara och lagrade utfall. Även slumpat utfall och admininställningar finns. |
| RabbitMQ mellan tjänsterna | Finns | `PaymentRequestedV1`, `PaymentSucceededV1`, `PaymentFailedV1` och `BookingConfirmedV1` är inkopplade. Se [meddelandedokumentationen](messaging.md). |
| `SeatsHeld` vid reservation | Saknas | Ingen sådan kontraktstyp, publicering eller konsumtion hittades. Publiceringen anges uttryckligen i dokumentets bokningsflöde. |
| `TicketScanned` i arkitekturbilden | Saknas | Skanning ändrar biljettstatus via HTTP. Ingen sådan händelse hittades. Detta är en avvikelse från bilden; flödestexten kräver framför allt att biljetten markeras använd. |
| Biljetter skapas vid `BookingConfirmed` | Finns | [BookingConfirmedConsumer](../src/services/TicketService/Features/Tickets/IssueTickets/BookingConfirmedConsumer.cs) och idempotent utfärdande med unikt databasindex finns. |
| PDF med QR-kod, QuestPDF och QRCoder | Finns | [TicketDocumentService](../src/services/TicketService/Features/Tickets/DownloadTicket/TicketDocumentService.cs), verifierat av tester. |
| Biljett skickas via e-post | Saknas | PDF hämtas i kundwebben. Mailpit från arkitekturbilden saknas också. |
| Entréskanner och engångsanvändning | Finns, integrerad i adminappen | Kamera och manuell inmatning i [TicketScannerPage](../src/frontend/admin-web/src/features/check-in/pages/TicketScannerPage.tsx). Eventkontroll och optimistisk samtidighetskontroll skyddar statusändringen i TicketService. |
| YARP som gemensam ingång och tokenkontroll | Saknas/annorlunda | Separata Nginx/Vite-proxyer och JWT-kontroll i respektive tjänst. |
| Keycloak med OIDC | Finns | Realm, adminroll, PKCE och JWT-validering finns. Kundflödet använder gästtoken. |
| SignalR för liveuppdateringar | Saknas | Polling används i befintliga vyer. |
| Egen PostgreSQL-databas per domäntjänst | Finns | Fyra separata databastjänster och EF Core-kontexter. Keycloaks lokala utvecklingsdatabas är separat från detta krav. |
| Multi-stage Dockerfile per tjänst | Finns | Alla fyra API:er samt båda webbapparna har separata bygg- och körsteg. |
| Start av hela systemet med Docker Compose | Delvis | Samlad konfiguration finns och validerar med en tillförd testnyckel. Den dokumenterade starten från en ren rotkatalog är ofullständig. |
| Konfiguration via `.env`, inga hemligheter i repot | Delvis | Biljettjänstens interna nyckel krävs från miljön och `.env` ignoreras. Miljömall saknas; Compose och realm innehåller fasta utvecklingsuppgifter. Ingen fullständig granskning av historiska hemligheter har gjorts. |
| xUnit-enhetstester för bokningsregler | Finns | Bokningsvalidering och flera övriga tjänsteregler är testade. Resultat nedan. |
| Integrationstest med Testcontainers | Annorlunda | Integrationstester använder verklig PostgreSQL, Redis och RabbitMQ, men infrastrukturen startas med Compose/CI-tjänster. Testcontainers används inte. |
| k6 i egen container och lasttestrapport | Saknas | Samtidighetsregressioner finns, men ingen leverans av föreskrivet lasttest hittades. |
| CI på pull requests med bygge, tester och Docker-images | Finns i konfiguration | [GitHub Actions](../.github/workflows/ci.yml) bygger och testar .NET, kör frontendkontroller och bygger alla fyra API-images. Extern körhistorik har inte verifierats. |
| `/health` i varje tjänst | Delvis | Finns bara i TicketService. |
| Serilog till Seq | Saknas | Befintlig `ILogger`-loggning uppfyller inte det uttryckliga teknikvalet. |
| OpenAPI för varje API | Finns i utvecklingsläge | Swagger-dokumenten svarade 200 för samtliga fyra API:er. |
| README med arkitektur och snabbstart | Delvis | Flera guider och ett meddelandediagram finns, men startguiden är ofullständig och inaktuell. |
| Scrum-tavla, stories, poäng och sprintmål | Ej verifierat | Inget granskningsbart underlag hittades i repot. Kan finnas i GitHub Projects eller Azure Boards. |
| Anteckningar från granskningar och retrospektiv | Ej verifierat | Inga sådana artefakter hittades i repot. |
| Granskade PR:er och PO-godkända acceptanskriterier | Ej verifierat | Kräver PR-/granskningshistorik och produktägarens underlag. Kan inte avgöras från koden. |
| Slutdemo vecka 7 och individuell reflektion | Ej verifierat | Demo har inte observerats och reflektionsdokument hittades inte i arbetskopian. |

## Ytterligare iakttagelser i befintliga flöden

Dessa är slutsatser från koden. De har inte reproducerats genom att ändra den befintliga körmiljöns kund- eller evenemangsdata.

1. **Säljstart kan kringgås via boknings-API:t.** Kundwebben använder `onSaleFrom` för att aktivera köpknappen. [CreateReservationAsync](../src/services/Booking/features/bookings/BookingService.cs) kontrollerar inte säljstarten, och [EventCatalogClient](../src/services/Booking/features/eventcatalog/EventCatalogClient.cs) tar inte med `OnSaleFrom` i sitt interna kontrakt. Ett direkt anrop med befintligt lager-ID kan därmed reservera före den säljstart som visas i UI:t. Lägg affärsregeln även på servern inför ett demonstrerat biljettsläpp.

2. **Prisändringar i katalogen förs inte vidare till bokningslagret.** [UpdateEventHandler](../src/services/EventCatalog/features/events/UpdateEvent/UpdateEventHandler.cs) ändrar `EventSectionPrice`. Booking använder sitt eget `EventInventory.Price` när en ny reservation skapas. Priset kopieras från katalogen när lagret skapas; någon synkronisering vid senare prisändring hittades inte. En ändring från exempelvis 200 till 250 kronor kan därför lämna nya köp på det tidigare lagerpriset. Bestäm vad som ska gälla för nya respektive redan reserverade köp och synkronisera därefter.

3. **Misslyckad lagerskapning vid nytt evenemang kan döljas för administratören.** [EventsController](../src/services/EventCatalog/features/events/EventsController.cs) sparar evenemanget först och gör därefter upp till tre HTTP-försök att skapa lager. Efter misslyckande returneras fortfarande 201 med `InventoryProvisioned=false`. Frontendens [CreateEventResponseDto](../src/frontend/admin-web/src/features/events/api/events.ts) saknar det fältet, och [EventFormPage](../src/frontend/admin-web/src/features/events/pages/EventFormPage.tsx) behandlar svaret som framgång. Ingen varaktig återförsökskö för just denna operation hittades. Visa lagerfelet och ge en återställningsväg, eller gör provisioneringen beständig.

4. **Vissa skrivande API:er saknar åtkomstkontroll.** [EventInventoryController](../src/services/Booking/features/eventinventory/EventInventoryController.cs) kräver ingen `Authorize` för att skapa lager. [PaymentsController](../src/services/PaymentSimulator/features/payments/PaymentController.cs) saknar motsvarande kontroll för betalningsstart, avbryt och återbetalning. Båda är nåbara via publicerade portar eller frontendproxyer. Detta blir särskilt relevant när gatewayens tänkta åtkomstkontroll saknas. Skydda lageradministration och interna betalningsoperationer med avsedd behörighet eller tjänsteautentisering. TicketServices interna nyckelkontroll och befintliga adminpolicies i andra endpoints behöver bedömas var för sig.

## Val som är tillåtna eller som utökar beskrivningen

- **React och .NET 10:** följer alternativet React och versionskravet .NET 8 eller senare.
- **RabbitMQ.Client i stället för MassTransit:** ett tillåtet val enligt meddelandekravet. Avsaknad av MassTransit är därför ingen separat kravlucka.
- **Två frontendappar med skannern i adminappen:** alla tre gränssnitt finns funktionellt, även om skannern inte är en tredje separat app eller container. Det är skannerns gatewayväg som avviker från beskrivningen.
- **Gästbokning:** kunden får en slumpad åtkomsttoken vars hash lagras i databasen. Projektbeskrivningen kräver inte uttryckligen kundinloggning, så detta har inte räknats som en saknad funktion.
- **Outbox, inbox, återförsök, dead-letter-köer och avstämning:** konkret stöd för det saga-liknande flödet. Sena lyckade betalningar kan återbetalas när reservationen inte kan bekräftas. Kombinationen av händelser och HTTP för uppslag/kompensation har granskats som en implementation av flödet, inte som avsaknad av RabbitMQ.
- **Statistik, beläggningsgrafer, evenemangsbilder, seedverktyg och inställning av betalningsutfall i admin:** tillägg utöver de tydligt specificerade kärnleverablerna. De fyller inte de kvarvarande kraven på stolval, gateway, e-post eller lasttest.

## Genomförd verifiering

Alla nedanstående .NET-tester byggdes och kördes lokalt med .NET SDK 10.0.103. Integrationstesterna använde tre tillfälliga, isolerade containrar med PostgreSQL 18, Redis 7 och RabbitMQ 4. Testerna fick egna anslutningar och använde inte applikationens databaser eller meddelandeköer.

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

Övriga kontroller:

- `npm run lint` i båda apparna: inga fel. Kundappen gav en varning om en saknad `useEffect`-dependency i `HomePage.tsx`.
- `npm run build` i båda apparna: godkänt. Vite varnade för stora JavaScript-paket.
- Adminappens två datumtester: båda godkända.
- Grund- och utvecklings-Compose validerades tillsammans med `config --quiet` och en tillfälligt tillförd testnyckel.
- Läsanrop mot befintlig lokal körmiljö verifierade Swagger och health-statusarna i tabellen ovan.
- NuGet gav `NU1903` för `Microsoft.OpenApi` 2.3.0 vid .NET-körningarna. Varningen bör följas upp separat; någon fullständig beroendesäkerhetsgranskning ingick inte.

Frontendbyggena och .NET-testerna slutfördes utanför sandlådan efter lokala processbegränsningar. De begränsningarna har inte räknats som fel i projektet. Alla fyra API:er byggdes genom testprojekten; Docker-images byggdes inte om vid denna granskning. En nyinstallation på ren maskin, interaktiv kameraskanning, fullständigt köp genom webbläsaren och extern CI-/Scrum-historik har inte verifierats. Godkända tester gäller de befintliga testfallen och bevisar inte att alla projektkrav är uppfyllda.

## Föreslagen ordning för återstående arbete

1. Komplettera stolmodellen genom hela köpet och verifiera att samma numrerade stol bara kan säljas en gång. Behåll de befintliga testerna för sektionsantal.
2. Inför YARP och slutför åtkomstkontrollen. Lägg säljstartsregeln på servern och hantera prissynkronisering och misslyckad lagerskapning.
3. Lägg till e-postleverans med Mailpit samt SignalR för liveförsäljning.
4. Komplettera händelserna: `SeatsHeld` enligt flödestexten och ta ställning till `TicketScanned` i arkitekturbilden.
5. Lägg till health-endpoints, Serilog/Seq och den föreskrivna Testcontainers-integrationen.
6. Rätta snabbstart och miljömall, verifiera en ren installation och kör k6 med en sparad lasttestrapport.
7. Samla länkar eller filer för Scrum-tavla, sprintmål, granskningar, retrospektiv, PO-acceptans, slutdemo och individuella reflektioner. Markera sådant som redan finns externt som verifierat när underlaget har granskats.
