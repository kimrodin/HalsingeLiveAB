# API för försäljningsstatistik och tjänsteintegration

`GET /GetSalesStatistics?from=2026-10-01&to=2026-10-31` i Booking kräver den
befintliga policyn `AdminOnly`. Via adminwebben används `/booking-api/GetSalesStatistics`.
Dashboardens beteende och frontendimplementation beskrivs i
[Admin-dashboarden](admin_dashboard.md). Detta dokument behandlar API-kontrakt,
beräkningsregler, tjänsteintegration och driftsättning.

## Vad siffrorna betyder

- Datum avser svenska kalenderdagar i `Europe/Stockholm` och båda gränsdagarna
  ingår. De faktiska UTC-gränserna varierar vid övergång mellan sommar- och
  vintertid. Standardperioden är idag och de föregående 29 dagarna. Högsta tillåtna intervall är 366 dagar.
- Bara bekräftade bokningar med matchande lyckade betalningar räknas med.
  Tillfälliga reservationer, avbokade eller utgångna bokningar och misslyckade betalningar utesluts.
- `PaidAt` avgör försäljningsdatumet, aldrig bokningens skapandetid eller evenemangets tidpunkt.
- Bara SEK stöds, enligt befintligt boknings- och betalningskontrakt. Beloppen är
  de betalda bokningarnas totalbelopp. Ingen momsuppdelning eller valutaomräkning härleds.
- `grossRevenue` omfattar lyckade betalningar för dessa bokningar. Fulla
  återbetalningar minskar `netRevenue` på den **ursprungliga försäljningsdagen**,
  även om återbetalningen sker senare. Historiska resultat kan därför ändras efter återbetalningar.
- `soldTickets` är nettoantalet biljetter för försäljning som inte har återbetalats.
  `refundedTickets` anger det borttagna antalet. Någon modell för delåterbetalning finns inte.
- `daily` innehåller varje dag, med nollvärden där det behövs. Summorna motsvarar
  `summary`. `previousPeriod` omfattar den direkt föregående, lika långa perioden.
- Procentuella förändringar är `null` om föregående värde är noll, eftersom
  förändringen då är odefinierad. Ingen påhittad ökning på 100 procent används.

Exempel på svarets struktur:

```json
{
  "from": "2026-10-01",
  "to": "2026-10-31",
  "currency": "SEK",
  "timeZone": "Europe/Stockholm",
  "summary": { "grossRevenue": 450, "refundedRevenue": 200, "netRevenue": 250, "soldTickets": 2, "refundedTickets": 3 },
  "previousPeriod": { "from": "2026-08-31", "to": "2026-09-30", "summary": { "grossRevenue": 0, "refundedRevenue": 0, "netRevenue": 0, "soldTickets": 0, "refundedTickets": 0 } },
  "changes": { "netRevenuePercent": null, "soldTicketsPercent": null },
  "daily": []
}
```

`daily` är förkortat ovan. Det verkliga svaret innehåller 31 poster.

## Tjänsteintegration och begränsningar

PaymentSimulator erbjuder `GET /api/payments/sales` med `from` som inkluderande
UTC-gräns, `toExclusive` som exkluderande UTC-gräns och sidmarkörerna `afterPaidAt`
och `afterPaymentId`. Båda statistikendpointsen kräver `AdminOnly`. Booking
vidarebefordrar inkommande Bearer-token för varje anrop. PaymentSimulator
validerar samma utfärdare, målgrupp och adminroll. Befintliga endpoints för
betalningsbehandling behåller sina åtkomstregler. Inställningar för
identitetsutfärdare och metadata konfigureras i Compose.

PaymentSimulator tillämpar datumvillkoret i PostgreSQL med ett filtrerat index på
`(PaidAt, Id)`. Svaret innehåller högst 500 rader per sida, sorterade efter
betalningsdatum och ID. Booking läser bara dessa boknings-ID:n och behåller
bekräftade bokningar. Obekräftade eller raderade bokningar bidrar inte. Hela den
historiska bokningslistan genomsöks eller hämtas inte över nätverket. Fel i äldre
betalningar utanför vald period och jämförelseperiod kan inte blockera rapporten.

Betalningsdatumet avgör om en betalning ingår i intervallet. Betalningar utan
`PaidAt` tillhör ingen daterad rapport. Den befintliga migreringen `PaymentPaidAt`
fyller i datum för äldre lyckade betalningar från `CompletedAt`. Ogiltiga
svarssidor ger 502, exempelvis vid saknade datum, dubbletter, rader utanför
intervallet eller en sidmarkör som inte går framåt. Detsamma gäller inkonsekventa
betalningar för bekräftade bokningar inom rapportintervallet. Timeout i
betaltjänsten ger 502. Avbrott från anroparen förs vidare. Ogiltiga kalenderintervall ger 400.

`PaymentSalesIndex` lägger till indexet utan att ändra befintliga data.
Driftsätt och migrera PaymentSimulator före den uppdaterade Booking-tjänsten.
Lokalt tillämpar PaymentSimulator migreringar vid uppstart som tidigare.
Ingen migrering krävs i Booking.

Sidmarkören använder oföränderliga betalningsdatum och ID för att skilja poster
med samma datum åt. Återbetalningar läses vid anropstillfället och hänförs till
ursprungligt försäljningsdatum. Det finns ingen gemensam distribuerad ögonblicksbild.
En betalning eller återbetalning som sparas medan rapporten skapas kan bli synlig vid nästa anrop.

## SignalR-hubb för försäljningsnotiser

Booking exponerar en autentiserad SignalR-hubb på `/hubs/sales`. Adminwebben
ansluter genom API Gateway på `/booking-api/hubs/sales`. Hubben kräver den
befintliga policyn `AdminOnly` och använder administratörens Keycloak-token.
För WebSocket-anslutningar från webbläsaren accepterar Booking SignalR-parametern
`access_token` i frågesträngen enbart på denna hubbsökväg. Nginx stänger av
åtkomstloggning för hubbanrop. Gatewayen och Booking begränsar detaljerade
anrops- och proxyloggar som kan innehålla frågesträngen.

När Booking har behandlat en lyckad försäljning eller återbetalning skickas
händelsen `SalesUpdated` till anslutna adminklienter. Händelsen har inga argument
och innehåller inga boknings-, betalnings-, biljett-, kund- eller försäljningsdata.
Klienterna använder den som en signal om att sparade värden är inaktuella och
hämtar data från den autentiserade endpointen `GetSalesStatistics`, som fortsatt
är källan till statistiken.

Adminwebbens Nginx-proxy vidarebefordrar WebSocket-headers för protokolluppgradering
på `/booking-api/`. API Gateway routar sökvägen till Booking. Klientens
återanslutning, periodiska hämtning och cachehantering beskrivs i
[Admin-dashboarden](admin_dashboard.md).

Den nuvarande driftsättningen kör en instans av Booking. Om Booking skalas till
flera instanser behöver en SignalR-backplane eller en hanterad SignalR-tjänst
konfigureras, så att en notis från en instans når adminklienter som är anslutna till en annan.

## Kodreferenser

- [Statistikendpoint för administratörer och HTTP-fel](../src/services/Booking/features/statistics/SalesStatisticsController.cs)
- [Summering, återbetalningar och jämförelseberäkningar](../src/services/Booking/features/statistics/SalesStatistics.cs)
- [Svenska datumgränser och periodvalidering](../src/services/Booking/features/statistics/SalesPeriod.cs)
- [Sidindelad hämtning av betalningar och svarsvalidering](../src/services/Booking/features/statistics/StatisticsPayments.cs)
- [Endpoint för försäljningsbetalningar och filtrering med sidmarkör](../src/services/PaymentSimulator/features/payments/Statistics/PaymentSalesController.cs)
- [Migrering för betalningarnas försäljningsindex](../src/services/PaymentSimulator/data/Migrations/20261102120000_PaymentSalesIndex.cs)
- [Autentiserad hubb för försäljningsnotiser](../src/services/Booking/features/statistics/SalesUpdatesHub.cs)
- [Försäljningsnotis efter behandling av inbox](../src/services/Booking/features/payments/Results/PaymentResultInboxDispatcher.cs)
- [Tester för statistikberäkningar](../tests/Booking.UnitTests/SalesStatisticsTests.cs)
- [Tester för betalningsklientens validering](../tests/Booking.UnitTests/StatisticsPaymentsTests.cs)
