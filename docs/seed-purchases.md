# Testdata för evenemang och köpta biljetter

Knappen **Skapa seeddata** i adminappens sidomeny kör två steg i utvecklingsläge:

1. EventCatalog `POST /seed` skapar eller återanvänder tre scener, deras sektioner
   och fyrtio evenemang. Därefter skapas biljettlager i Booking.
2. Booking `POST /SeedPurchases` skapar eller återanvänder betalda, bekräftade
   bokningar för just dessa seedevenemangs ID:n. Matchande lyckade betalningar
   säkerställs i PaymentSimulator via `POST /api/payments/seed`, och meddelanden
   läggs i den vanliga outboxen för biljettutfärdande.

Båda de nya endpointsen kräver en admintoken och miljön `Development`. Booking
vidarebefordrar admintoken till PaymentSimulator. Ingen verklig betalleverantör
anropas. TicketService använder den befintliga interna API-nyckeln och det vanliga
idempotenta flödet för biljettutfärdande.

De ursprungliga tio evenemangen för 2027 behåller sina ID:n och definitioner.
Trettio ytterligare evenemang använder ordningsnummer 11–40: två per månad under
2026 samt sex konserter i oktober. Inga nya scener behövs. Testlagret använder
katalogens sektionskapaciteter.

Köpen fyller ungefär 35–84 procent av varje sektion, med minst åtta platser där
kapaciteten tillåter det. Varje köp innehåller högst åtta biljetter. Köpdatumen
fördelas deterministiskt över åtta dagar under de sex veckorna före evenemanget,
med varierande klockslag. Framtida seedevenemang får även framtida syntetiska
betalningsdatum. Uppgifterna används för försäljning, omsättning, beläggning,
bokningslistor och biljett-PDF:er.

Evenemangs-ID:n använder `10000000-…`, bokningar `51000000-…`, bokningsrader
`52000000-…`, outboxmeddelanden `53000000-…` och betalningar `54000000-…`.
Evenemangets, sektionens och köpets ordningsnummer ger stabila ID:n. Upprepade
eller samtidiga seedanrop återanvänder befintliga rader. Ändrade bokningar eller
betalningar ger en konflikt i stället för att återställas.

Seedverktygen är utvecklingsverktyg. Kör skapande och radering ett i taget, utan
kundbokningar eller aktiva reservationer på seedevenemangen. Verktygen samordnar
inte samtidig kundtrafik mellan EventCatalog och Booking.

Varje evenemang skapas i en vanlig databastransaktion. Betalda bokningar och
biljettmeddelanden i outbox sparas tillsammans. Matchande simulerade betalningar
är idempotenta. Därefter byggs Redis-lagret om från de sparade antalen i
PostgreSQL, i stället för att tillfälliga reservationer skapas och kompenseras.
Upprepat skapande lägger inte till fler köp eller drar av lagret igen. Befintliga
biljettmeddelanden köas på nytt för att reparera utfärdandet efter ett avbrott.

**Ta bort seeddata** anropar först Booking `DELETE /SeedPurchases` och tar sedan
bort katalogens evenemang, sektioner och scener. Efter idempotent radering av
betalningar och biljetter återställer Booking lagret och tar bort tillhörande
bokningar, bokningsrader och kömeddelanden i en databastransaktion. Om ett beroende
misslyckas lämnas databasen oförändrad och raderingen kan försökas igen. Efter
slutförd transaktion laddas Redis om från databasen. Samma omladdning sker när
raderingen försöks igen trots att inga bokningar finns kvar.

Bara köp med det deterministiska ID-prefixet `51000000-…` tas bort. Vanliga
bokningar på seedevenemang blockerar raderingen. Orelaterade vanliga bokningar och
betalningar behålls. Äldre avbrutna rader med `DeletingSeed` kan fortfarande tas
bort utan att lagret återställs två gånger. Nya raderingar skapar inte detta mellanläge.

Ett kort transaktionsbundet rådgivande lås används runt seedoperationer och
utskick av seedbiljetter, eftersom biljettarbetaren körs automatiskt. Arbetaren
läser om köade meddelanden under låset så att en gammal meddelandeomgång inte kan
återskapa raderade seedbiljetter. Seedbiljetter utfärdas via synkrona HTTP-anrop
medan låset hålls. Vanliga kundbiljetter använder RabbitMQ. Enbart en bekräftelse
från meddelandeförmedlaren skulle lämna seedmeddelanden som kan återskapa
biljetter efter radering. Historiska seedbetalningar skapas som redan avslutade
betalningar via seedfunktionens HTTP-API och går inte genom betalningsköerna.
Ingen återställningsjournal i Redis eller något sessionsbundet rådgivande lås behövs.

Bygg om EventCatalog, Booking, PaymentSimulator och TicketService efter
uppdateringen innan knappen används. Funktionen kräver ingen ändring av databasschemat.

Verifieringen omfattar två riktade integrationstester: ett för katalogens
månadsfördelning och stabila ID:n, och ett för köpens livscykel med skapande,
upprepat skapande, radering, bevarande av vanliga poster och nytt skapande efter
återställt lager. Skapande och radering av seedbetalningar täcks av ett separat
integrationstest i PaymentSimulator.

## Kodreferenser

- [Samordning av seedanrop i adminappen](../src/frontend/admin-web/src/features/seed/api/seed.ts)
- [Samordning av seeddata i katalogen](../src/services/EventCatalog/features/Seed/SeedAllHandler.cs)
- [Deterministiska definitioner av seedevenemang](../src/services/EventCatalog/features/Seed/SeedEvents/SeedEventsHandler.cs)
- [Bekräftade köp, lager och biljett-outbox](../src/services/Booking/features/Seed/SeedPurchases/SeedPurchasesHandler.cs)
- [Radering av köp och återställning av lager](../src/services/Booking/features/Seed/SeedPurchases/DeleteSeedPurchasesHandler.cs)
- [Skapande av simulerade seedbetalningar](../src/services/PaymentSimulator/features/Seed/SeedPaymentsHandler.cs)
- [Utskick och låsning för seedbiljetter](../src/services/Booking/features/tickets/TicketIssueOutboxDispatcher.cs)
- [Integrationstester för köpens livscykel](../tests/Booking.IntegrationTests/SeedPurchasesTests.cs)
- [Integrationstester för katalogens seeddata](../tests/EventCatalog.IntegrationTests/Features/Seed/SeedEventsTests.cs)
