# Verifiering av snabbstarten

## Upprepa kontrollen

1. Klona önskad revision till en ny katalog och skapa `.env` enligt
   [konfigurationsguiden](configuration.md), med nya hemligheter.
2. Ange ett unikt `COMPOSE_PROJECT_NAME`, exempelvis `hl-quickstart-20261109`.
   Kontrollera att namnet saknar befintliga volymer och välj lediga
   `*_PUBLISHED_PORT`. Ändrade admin-/Keycloak-portar kräver även
   [anpassade inloggningsadresser](keycloak.md).
3. Kör följande från testklonens rot. Kontrollera att terminalens miljövariabler
   inte överstyr testprojektet eller portarna.

   ```sh
   git rev-parse HEAD
   git status --short
   docker version
   docker compose version
   docker compose config --quiet
   docker compose up -d --build
   docker compose ps
   ```

4. Mät tiden till fungerande health och webbappar. Följ sedan
   [snabbstartens testköp](../README.md) och kontrollera betalning och biljett-PDF.
   Kör `docker compose down` och startkommandot igen; kontrollera att data finns kvar.
5. Dokumentera revision/patch, OS, verktygsversioner, kommandon och resultat.
   Redovisa nedladdning, bygge och total tid separat, inklusive vad som var cachat.
   För att godkänna #128 ska även en annan utvecklare följa guiden.

Använd endast testprojektets volymer. `down` behåller dem för granskning;
undvik `down -v` i utvecklingsmiljön. Spara inga lösenord eller token i rapporten.

## Resultat 2026-11-09

Lokal klon av `10fa674fe379fcff04f8be8555024682ad3b002a` med arbetskopians
ändringar för #128. Compose-patchens SHA-256:
`78161528a3e01f4c0ef60d61db2a8bf4daaac091128aeb9d4068fc84394b9df5`.

| Miljö | Värde |
| --- | --- |
| Värd | Windows 11 Home 10.0.26200, x86_64; Docker med 16 CPU och 15,2 GiB minne |
| Verktyg | Docker CLI/Engine 29.8.2, Compose 5.6.0, headless Chrome 156.0.8078.80 via Playwright |
| Beroenden | PostgreSQL 18-alpine, Redis 7-alpine, RabbitMQ 4-management-alpine, Keycloak 26.8.2 |
| Isolering | `hl-issue128-20261109-b`, elva nya volymer och egen `.env` |
| Värdportar | Admin 5173, kund 15174, gateway 15212, Keycloak 8180; övriga förskjutna med 10000 |

Kommandona ovan användes, med upprepad start och `down`. För Nginx-provet
ändrades testets `COMPOSE_FILE` till enbart `src/Compose.yaml` före nästa start.

| Mätning | Tid och omfattning |
| --- | --- |
| Klon och miljökonfiguration | Inte separat tidsmätta |
| Nedladdning vid uppmätt dev-start | Inga nya image-lager; tidigare nedladdningar saknar separat tidsmätning |
| Dev-bygge | 1,65 s parallellt byggfönster, med cachade lager och paketrestore |
| `up -d --build` | 38,40 s, exitkod 0; start 09:46:39 svensk tid |
| Samtliga HTTP-kontroller klara | Inom 61,57 s från start, inklusive kontroller och frontendens `npm ci` |
| Separat webbläsarflöde | 99,73 s för inloggning, seed, köp och PDF |
| Nginx med bevarade testvolymer | 69,74 s för startkommandot, varav parallellt byggfönster 37,16 s. Nedladdning/extrahering av Nginx-lager tog 2,1 s och överlappade bygget |

Godkänt i testet:

- Alla 67 obligatoriska variabler fanns; saknad intern API-nyckel avvisades före start.
- Fyra API:er migrerade tomma databaser automatiskt. Gateway, readiness,
  webbappar och Keycloak-discovery gav HTTP 200.
- Admininloggning och seedning av 40 evenemang med lager och syntetiska köp fungerade.
- Ett kundköp på 425 SEK blev `Confirmed` / `Succeeded`, med biljett-PDF och
  fungerande adminuppslag. Även betalningsguidens PowerShell-exempel fungerade.
- Bokningen, uppladdad platskarta och evenemangsbild fanns kvar efter full
  omstart med Nginx. Inga JavaScript-undantag observerades under köpflödet.
- Testcontainrarna monterade bara egna volymer. De tio ursprungliga
  utvecklingsvolymerna behölls och deras containrar förblev stoppade.

Testcontainrarna stoppades och testvolymerna behölls. Råresultaten finns i
`.quickstart-verification/`. Katalogen innehåller privat testkonfiguration och
är Git-ignorerad.

**Kvar:** en annan utvecklares genomgång. Fem minuter är verifierat för
systemstart med tomma volymer och cachade bygglager. En helt kall installation
inklusive alla nedladdningar är inte tidsverifierad. macOS/Linux som värd har
inte körtestats; de separata mätningarna ovan ska inte summeras som en obruten körning.

## Kodreferenser

- [Grundmiljön som verifieras: tjänster, volymer och healthchecks](../src/Compose.yaml)
- [Utvecklingsmiljön med Vite](../src/Compose.dev.yaml)
- [Gatewayens API-rutter för HTTP-kontrollerna](../src/ApiGateway/appsettings.json)
- [Keycloak-realm för admininloggningen](../infrastructure/keycloak/halsingelive-realm.json)
- [Seedflödet som används i kontrollen](../src/services/EventCatalog/features/Seed/SeedAllHandler.cs)
- [Kundens boknings- och betalningsflöde](../src/frontend/customer-web/src/features/bookings/pages/BookingPage.tsx)
- [Biljett-PDF och QR-kod](../src/services/TicketService/Features/Tickets/DownloadTicket/TicketDocumentService.cs)
