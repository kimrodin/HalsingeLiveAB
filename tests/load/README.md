# Lasttester för köpflöde och biljettsläpp

Kräver att Docker Desktop körs med Linux-containrar. Kör från repots rotkatalog;
du behöver varken installera k6, ändra `.env` eller logga in som administratör.

Dokumenterad validering: [resultat för köpflöde, kort biljettsläpp, tävling om sista biljetten
och regressionstester, 2026-11-09](validation-2026-11-09.md). Alla tre dokumenterade
lasttestkörningar klarade sina korrekthetskontroller. Rapporten innehåller kommandon,
miljö, uppmätta tider och återstående arbete för
[ärende #126](https://github.com/kimrodin/HalsingeLiveAB/issues/126).
Dessa små lokala körningar fastställer inte kapaciteten i produktion.

1. Kör det lilla testet:

   ```powershell
   .\tests\load\run.ps1
   ```

2. Följ de fyra stegen som skrivs ut: starta en ny testmiljö, kör testet, verifiera
   och städa sedan upp. Första körningen hämtar k6 och bygger tjänsterna och tar därför längre tid.

3. Läs sammanfattningen i terminalen eller öppna `tests/load/results/<run>/summary.json`.
   Prova lite mer trafik med:

   ```powershell
   .\tests\load\run.ps1 -Vus 5 -Iterations 25
   ```

Standardscenariot `Checkout` kör **10 köp fördelade på 2 virtuella användare**,
plus ett uppvärmningsköp. Varje köp reserverar en biljett, startar betalning och
hämtar status var 500:e ms tills bokningen är bekräftad och exakt en giltig biljett
finns. Köpflödet avbryts efter 30 sekunders statuskontroller. Alla köp måste lyckas;
även avbrutna eller uteblivna iterationer gör att testet underkänns. De inledande
tröskelvärdena kontrollerar korrekthet. Den här första versionen mäter svarstider
utan att sätta ett godtyckligt prestandamål.

Titta på `checkout_succeeded` (måste vara 100%), `completed_checkouts` (måste
motsvara det begärda antalet köp) och `checkout_duration` (tid från reservation till
biljetter, inklusive statuskontroller). Enbart HTTP-tider mäter inte det asynkrona
köpflödet. Uppvärmningens HTTP-anrop ingår i de inbyggda HTTP-mätvärdena; de egna
köpmätvärdena omfattar bara de uppmätta köpen. `run.json` registrerar commit och
belastning, och `images.json` registrerar vilka containeravbilder för tjänsterna som
användes. Körningarna sparar också `services.log`.
`outcome.json` visar det samlade resultatet: både k6 och slutverifieringen måste
godkännas. `verification.json` innehåller de senast avlästa tillstånden för
databaser, Redis och köer samt eventuella misslyckade konsistenskontroller.
En godkänd k6-sammanfattning räcker alltså inte för ett godkänt helhetsresultat.

`checkout.js` innehåller de tre HTTP-stegen. `fixture.sql` skapar ett evenemang för
mätningen med konfigurerbar kapacitet (1 000 som standard för Checkout) och ett
separat uppvärmningsevenemang direkt i de tillfälliga databaserna efter att de
ordinarie migreringarna har körts. Alla uppmätta köp använder de vanliga API:erna,
Redis, outboxar, RabbitMQ och meddelandekonsumenter.
Evenemanget ligger alltid ett år framåt i tiden. Som standard lyckas betalningarna
efter en planerad fördröjning på 500 ms; bakgrundsarbetarnas körintervall bidrar
också till den uppmätta köptiden.

Varje körning får ett unikt Compose-projekt med nya instanser av PostgreSQL, Redis
och RabbitMQ. Deras data lagras på isolerade diskbaserade volymer; Redis har AOF
aktiverat. Volymerna tas bort vid städningen. Detta skiljer sig från den äldre
minnesbaserade referensmiljön, så dess tider ska inte jämföras direkt med dessa
körningar.
Bara RabbitMQ:s administrationsgränssnitt exponeras, på en ledig port bunden till
`127.0.0.1`. Inga utvecklingsvolymer monteras. Skriptet tar bara bort den aktuella
körningens containrar och volymer, även om testet misslyckas. Resultaten finns kvar på disk.

## Scenario för biljettsläpp

```powershell
.\tests\load\run.ps1 -Scenario Release
```

Standardinställningarna låter avsiktligt **1 000 köpförsök konkurrera om 100 biljetter**:

| Fas | Belastning |
| --- | --- |
| Första 30 sekunderna | Fem bläddringsflöden/sekund, där varje flöde läser evenemangslistan, evenemanget och biljettlagret. |
| Nästa 20 sekunder | 30 nya köpflöden/sekund, plus bläddring. |
| Nästa 40 sekunder | 10 nya köpflöden/sekund, plus bläddring. |
| Slutavstämning | Vänta tills bokningar, betalningar, biljetter, SQL-/Redis-lager och köer stämmer överens. |

Varje köpflöde börjar med ett försök att reservera 1–3 biljetter. Av de planerade
flödena tilldelas deterministiskt 10% att överge reservationen, 10% att avboka och
80% att betala efter 0–3 sekunders betänketid. När kunder konkurrerar om biljetterna
kan andelarna bland de accepterade reservationerna skilja sig från denna fördelning.
All uppmätt HTTP-trafik använder gatewayens kundvända API-vägar. SQL används bara
för testdata, simuleringsinställningar och slutavläsning.
Uppvärmningsköpen använder ett annat evenemang och lyckas alltid innan de valda
betalningsinställningarna tillämpas. De sparas separat i `warmup.json`.

**Räkna med cirka 11–13 minuter för standardkörningen efter uppstart.** Övergivna
reservationer behåller applikationens verkliga giltighetstid på tio minuter.
Verifieringen väntar tills de löper ut; den förkortar, raderar eller frigör dem inte
manuellt. Kontroller görs var femte sekund och två godkända avläsningar i följd
krävs. Standardgränsen för slutavstämningen är 720 sekunder efter att k6 har
avslutats. Förloppet skrivs ut var 30:e sekund.

`burst` och `tail` använder `constant-arrival-rate`: nya flöden startar oberoende av
om tidigare flöden har avslutats. Ett flöde innehåller flera HTTP-anrop, så
flöden/sekund är inte samma sak som anrop/sekund. Varje fas förallokerar 128
virtuella användare, VU (bläddringen använder ytterligare 16). Öka
`-PreAllocatedVus` vid behov. Alla `dropped_iterations`, uteblivna köpförsök,
oväntade fel eller avbrutna flöden gör att testet underkänns.

Ett HTTP 409-svar från `CreateBooking` räknas som ett förväntat lageravslag bara om
dess problemrubrik är `Not enough tickets are available.` Andra konflikter gör
fortfarande att testet underkänns. Release kräver minst ett lageravslag och minst
ett slutfört betalningsutfall; varje besökare behöver inte köpa en biljett.

Läs dessa mätvärden var för sig:

- `release_reservation_duration`: HTTP-tid för reservationsanropet, inklusive lageravslag.
- `release_paid_duration`: tid från reservation via betänketid, betalning och
  statuskontroller till giltiga biljetter, bara för slutförda köp. Lageravslag påverkar inte denna fördelning.
- `release_reserved`, `release_sold_out`, `release_abandoned`, `release_cancelled`,
  `release_paid`, `release_payment_rejected`: antal flöden per utfall.
- `release_unexpected_error`: måste vara noll; `release_attempts` måste motsvara den planerade belastningen.

Slutverifieringen jämför de uppmätta räknarna med databasraderna. Den underkänner
överförsäljning, felaktiga, saknade eller dubbla biljetter, betalningar eller
biljetter utan tillhörande bokning, oväntade debiteringar, bokningar utan slutstatus,
lager som inte har återställts, aktiva Redis-reservationer, väntande arbete i
outbox/inbox, avvisade inbox-meddelanden, saknade köer och köer som inte är tomma
(inklusive dead-letter-köer). Det separata uppvärmningsevenemanget utesluts vid
avstämningen av den uppmätta kapaciteten. För ett godkänt resultat krävs också att
k6-processen avslutas utan fel.

För en kortare utvecklingskontroll utan övergivna reservationer:

```powershell
.\tests\load\run.ps1 -Scenario Release -Capacity 10 `
  -BurstRate 5 -BurstSeconds 4 -TailRate 2 -TailSeconds 4 `
  -PreSaleSeconds 2 -PreAllocatedVus 32 -AbandonPercent 0 `
  -CancelPercent 20 -MaxQuantity 2 -ThinkMaxSeconds 1
```

Testa planerat betalningsfel och överskriden tidsgräns i separata körningar:

```powershell
.\tests\load\run.ps1 -Scenario Release -PaymentOutcome Failed -AbandonPercent 0
.\tests\load\run.ps1 -Scenario Release -PaymentOutcome TimedOut -AbandonPercent 0
```

Dessa inställningar gäller alla betalande kunder i körningen. Testet förväntar sig
avbokade bokningar, inga biljetter och återställt lager för dessa kunder. Det
underkänns om en betalning oväntat lyckas. Viktade blandningar av betalningsutfall
och varierande svarstider hos betalningsleverantören är inte implementerade.
Det befintliga läget `Random` med tre lika stora andelar används inte.
`-PaymentDelayMs` och `-PaymentTimeoutMs` konfigurerar den befintliga simulatorn.

## Tävling om sista biljetten

Kör 50 köpförsök mot ett separat evenemang med kapacitet ett. Varje försök begär en
biljett och försöker betala, utan övergivna reservationer eller kundavbokningar:

```powershell
.\tests\load\run.ps1 -Scenario Release -Capacity 1 `
  -BurstRate 20 -BurstSeconds 2 -TailRate 5 -TailSeconds 2 `
  -PreSaleSeconds 2 -PreAllocatedVus 32 -AbandonPercent 0 `
  -CancelPercent 0 -MaxQuantity 1 -ThinkMaxSeconds 0
```

Kräv `release_attempts = 50`, `release_reserved = 1`, `release_paid = 1` och
`release_sold_out = 49`. Slutverifieringen ska visa en bekräftad bokning, en lyckad
betalning, en giltig biljett, noll tillgängliga biljetter i SQL/Redis och inga
aktiva reservationer eller väntande köjobb för det uppmätta evenemanget.
Det separata uppvärmningsköpet utesluts. Detta testar konkurrens om den sista
enheten i lagret. Unik försäljning av numrerade stolar kan verifieras först när
flödet för stolval finns.

## Ihållande trafik genom köpflödet

Ett biljettsläpp kan sälja slut snabbt och därefter främst testa avslagssvar.
Använd `Sustained` för att belasta hela betalnings- och biljettflödet med gott om
biljetter i lagret:

```powershell
.\tests\load\run.ps1 -Scenario Sustained -Capacity 100000 `
  -BurstRate 10 -BurstSeconds 60 -TailRate 10 -TailSeconds 240 `
  -AbandonPercent 0 -CancelPercent 0 -PreAllocatedVus 256
```

Scenariot använder samma flödeslogik men **underkänns vid varje lageravslag**.
Standardkapaciteten är 100 000; övriga standardvärden för takt, tider och
kundgrupper är samma som för Release. Trafiktakter och kundfördelningar är
testantaganden, inte en efterfrågeprognos. Båda scenarierna redovisar svarstider utan
ett godtyckligt p95-mål. Överenskomna prestandamål bör läggas till när avsedd
hårdvara och tjänstemål har definierats.

## Compose-kommandon och verifiering

`run.ps1` anger ett unikt projektnamn, parametrar för testdata, miljövariabler och
montering av resultatkatalogen. Nedan visas den underliggande kommandosekvensen.
Platshållarna representerar värden som skriptet skapar; förbered dem innan du
klistrar in kommandona:

```text
docker compose -p <run-project> -f tests/load/compose.yaml up -d --build --wait --wait-timeout 180
docker compose -p <run-project> -f tests/load/compose.yaml exec -T postgres psql -U loadtest -d loadtest -v ON_ERROR_STOP=1 -v capacity=<capacity> -f /fixture.sql
docker compose -p <run-project> -f tests/load/compose.yaml run --rm --no-deps --volume <absolute-results-path>:/results --env VUS=1 --env ITERATIONS=1 --env SKIP_SETUP_WARMUP=true --env INVENTORY_ID=74000000-0000-0000-0000-000000000002 k6 run --summary-export=/results/warmup.json /scripts/checkout.js
docker compose -p <run-project> -f tests/load/compose.yaml exec -T postgres psql -U loadtest -d payments -v ON_ERROR_STOP=1 -v payment_outcome=Succeeded -v payment_delay=500 -v payment_timeout=5000 -f /payment-policy.sql
docker compose -p <run-project> -f tests/load/compose.yaml run --rm --no-deps --volume <absolute-results-path>:/results <workload-env-arguments> k6 run --summary-export=/results/summary.json /scripts/release.js
docker compose -p <run-project> -f tests/load/compose.yaml down --volumes --remove-orphans
```

Skriptet väntar också på gatewayen före uppvärmningen och kör `verify.ps1` före
städningen. Bedöm inte resultatet enbart utifrån k6-kommandot. `-SkipBuild`
återanvänder befintliga lokala applikationsavbilder under utveckling; utelämna
flaggan för att testa aktuell källkod. `run.json` registrerar arbetskopians
tillstånd och `images.json` registrerar avbildernas ID:n.

Validering utan lasttestkörning:

```powershell
docker compose -f tests/load/compose.yaml config --quiet
powershell -NoProfile -File tests/load/verify.tests.ps1
node --experimental-vm-modules --test tests/load/release.tests.cjs
```

Dessa kommandon validerar Compose och regressionsfallen. Kör scenarierna ovan för
att samla underlag från verkliga köpflöden och avstämningar. Fullständiga
körningsartefakter finns kvar i den Git-ignorerade katalogen
`tests/load/results/<run>/`. För att bevara underlaget för andra utvecklare, lägg
en rapport utanför `results/` i versionshanteringen med revision, exakta kommandon,
hårdvara/Docker-resurser, avbilds-ID:n, testdata, belastning, svarstidspercentiler,
genomströmning, fel och slutliga avstämningsantal, som i den dokumenterade
valideringen.

De nuvarande sammanfattningarna innehåller median (p50) och p95, men exporterar
inte p99. Lägg till p99 i `summaryTrendStats` innan den fullständiga
percentilrapporten för #126 tas fram. Konfigurerbar upptrappning och tröskelvärden
för svarstider/felandel återstår också. Nuvarande skript tillämpar fasta
korrekthetskrav och mäter svarstider.

## Följ RabbitMQ medan testet körs

Kör ett lite längre test för att hinna öppna gränssnittet:

```powershell
.\tests\load\run.ps1 -Vus 5 -Iterations 100
```

Efter uppstart skriver skriptet ut **RabbitMQ UI: http://127.0.0.1:<port>/#/queues**.
Öppna adressen och logga in med användarnamnet **loadtest** och lösenordet
**loadtest**. Varje körning använder en ledig port, så kopiera den URL som skrivs ut
för just den körningen. Gränssnittet tas bort när testet avslutas och städningen körs.

Under **Queues and Streams**, granska:

- `payments.payment-requested.v1`: Booking till PaymentSimulator.
- `booking.payment-results.v1`: PaymentSimulator till Booking.
- `tickets.booking-confirmed.v1`: Booking till TicketService.

**Ready** räknar meddelanden som väntar på leverans; **Unacked** räknar levererade
meddelanden som väntar på kvittering. Följ även takterna för publicering, leverans
och kvittering. Meddelanden kan konsumeras så snabbt att ingen köbildning syns.
I det här projektet kvitterar PaymentSimulator begäran efter att försöket har
sparats och slutför därefter betalningen i bakgrunden. En tom begärandekö kan
därför fortfarande innebära att betalningar pågår.
Se [RabbitMQ:s administrationsguide](https://www.rabbitmq.com/docs/management).

## Databasanslutningar vid högre belastning

Alla fyra tjänstedatabaser delar en PostgreSQL-server i den här lilla testmiljön.
Servern tillåter 100 anslutningar och varje tjänsts anslutningspool är begränsad
till 20, vilket ger högst 80 poolade anslutningar totalt. Resterande platser ger
utrymme för administration och felsökning. Fler virtuella användare kan dela på
dessa anslutningar; en virtuell användare behöver inte en egen permanent öppen
databasanslutning.
Gränserna registreras i `run.json` och anslutningarna märks per tjänst med
`Application Name`.

Utan uttryckliga poolgränser tillåter Npgsql upp till 100 anslutningar per pool.
Fyra tjänster kan då förbruka den delade serverns alla anslutningar och ge
PostgreSQL-felet `53300: sorry, too many clients already`. Dessa standardvärden
passade inte den delade testservern. Applikationens ordinarie Compose-miljö
använder separata databasservrar; poolgränserna här gäller bara lasttestmiljön.
Högre belastning kan fortfarande ge problem med svarstider eller andra gränser;
kraven för ett godkänt test är desamma.
Se [PostgreSQL:s anslutningsgränser](https://www.postgresql.org/docs/18/runtime-config-connection.html)
och [Npgsqls poolinställningar](https://www.npgsql.org/doc/connection-string-parameters.html#pooling).

För jämförbara körningar, använd samma maskin, Docker-resurser, avbildsversioner
och bakgrundsbelastning. k6 är låst till en viss version; infrastrukturen använder
applikationens avbildstaggar för huvudversioner, så jämför `images.json` om dessa
taggar uppdateras. Den här lilla miljön delar en PostgreSQL-container mellan fyra
databaser. k6 delar Docker-värd med tjänsterna. CPU-/RAM-budgetar, antal instanser,
produktionens ingress/TLS och nätverksfördröjning modelleras inte. Innan
produktionskapaciteten uppskattas behöver avsedd databastopologi och resursgränser
användas, och k6 köras på en separat maskin.
Scenarierna testar gästköp; Keycloak-inloggning, rendering i webbläsare,
PDF-generering och riktiga betalningsleverantörer ingår inte. Bläddring sker före
köpfaserna, men testevenemangets försäljning är redan öppen. Serverns kontroll av
`OnSaleFrom` implementeras eller verifieras inte av denna teständring.

Referenser: [k6:s delade iterationer](https://grafana.com/docs/k6/latest/using-k6/scenarios/executors/shared-iterations/)
och [tröskelvärden](https://grafana.com/docs/k6/latest/using-k6/thresholds/).
