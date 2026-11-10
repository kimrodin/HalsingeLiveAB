# Lokal validering av lasttester - 2026-11-09

Historisk mätning före införandet av konfigurerbar rampning, prestandatrösklar
och p99-export. Se [den kompletterande rapporten](validation-2026-11-09-performance.md)
för validering av dessa ändringar. Slutsatserna nedan gäller den angivna revisionen.

Nya körningar av köpflödet, ett kort biljettsläpp och en tävling om sista biljetten
klarade sina k6-tröskelvärden och slutavstämningar. Den här rapporten bevarar
mätresultaten och regressionsresultaten för
[ärende #126](https://github.com/kimrodin/HalsingeLiveAB/issues/126).
Belastningen motsvarar små lokala korrekthetskontroller. Produktionskapacitet och
mål för svarstider har ännu inte bedömts.

## Revision och miljö

Alla tre körningar använde grenen `k6Loadtesting` vid
`692eb1c78f0d2116a5444d821e0708a3e0374de8`. Varje `run.json` registrerade
`workingTreeDirty: false`; den här dokumentationen skrevs efter körningarna.
Den första körningen byggde aktuell källkod med hjälp av Dockers byggcache.
Efterföljande körningar använde `-SkipBuild` och identiska avbilds-ID:n för
applikationen och infrastrukturen.

| Uppgift | Dokumenterad miljö |
| --- | --- |
| Datum och lokal tidszon | 2026-11-09, Europe/Stockholm (UTC+01:00) |
| Värddator | Windows, version 26200; Intel Core i7-1255U, 10 fysiska kärnor / 12 logiska processorer |
| Docker-motor | Docker Desktop, Linux-containrar via WSL2; klient/server 29.8.1 |
| Linux-kärna | `6.6.87.2-microsoft-standard-WSL2` |
| Docker-resurser | 12 processorer; 8 163 381 248 byte minne (cirka 7,60 GiB) |
| Docker Compose | 5.5.1 |
| PowerShell / Node.js | 5.1.26100.8457 / 24.13.1 |
| k6 | `grafana/k6:1.3.0` |
| Infrastruktur | `postgres:18-alpine`, `redis:7-alpine`, `rabbitmq:4-management-alpine` |
| Applikation | Fyra domäntjänster i .NET 10 och API-gatewayen, byggda med respektive Dockerfile |
| Databastopologi | Fyra tjänstedatabaser i en PostgreSQL-container; 100 serveranslutningar, högst 20 anslutningar per tjänsts pool |
| Lagring | Nya volymer per Compose-projekt; Redis AOF aktiverat; volymerna togs bort efter varje körning |

Körningarna gjordes efter varandra. Den befintliga utvecklingsmiljön fortsatte att
köras på samma maskin och k6 delade Docker-värd med tjänsterna. Ingen CPU- eller
RAM-användning per tjänst registrerades. Dessa förhållanden begränsar jämförelser
med andra maskiner och produktionsmiljöer.

## Regressionskontroller

Följande kontroller godkändes tidigare under samma valideringssession mot samma
revision:

| Kommando från repots rotkatalog | Resultat |
| --- | --- |
| `docker compose -f tests/load/compose.yaml config --quiet` | Avslutningskod 0 |
| `powershell -NoProfile -File tests/load/verify.tests.ps1` | 16 fall godkända |
| `node --experimental-vm-modules --test tests/load/release.tests.cjs` | 9 tester godkända, 0 misslyckade |

Testerna för biljettsläppsskriptet använder förberedda HTTP-svar för att testa
förväntade lagerkonflikter, oväntade konflikter, övergivna reservationer,
avbokning, statuskontroller för betalning/biljetter, dubbla biljetter, planerade
betalningsavslag och hantering av iterationer vid schemaläggningens fasgränser.
Avstämningstesterna omfattar felaktigt lager, biljetter, betalningar, Redis-tillstånd,
outbox-/inbox-tillstånd, kötillstånd och mätvärdesräknare. Dessa regressionsfall
validerar testverktygen; de faktiska körningarna nedan testar tjänsterna.

Docker-körningarna och Nodes testkörare behövde köras utanför kommandosandlådan,
eftersom den blockerade åtkomst till Dockers kommunikationskanal och skapandet av
Nodes testprocess.

## Kommandon och belastning

Kör från repots rotkatalog med Docker Desktop igång. Nedan följer de exakta
kommandon som användes, i körordning. `-SkipBuild` återanvänder avbilderna som det
första kommandot byggde; utelämna flaggan när ändrad applikationskod ska valideras.

### Köpflöde (Checkout)

```powershell
powershell -NoProfile -File tests/load/run.ps1 -Scenario Checkout
```

Tio uppmätta köp av en biljett vardera, fördelade på två virtuella användare,
plus ett uppvärmningsköp under förberedelsen mot samma lager på 1 000 biljetter.
Checkout hämtar status var 500:e ms och har en tidsgräns på 30 sekunder för
betalning/biljetter.

### Kort biljettsläpp

```powershell
powershell -NoProfile -File tests/load/run.ps1 -Scenario Release -Capacity 10 -BurstRate 5 -BurstSeconds 4 -TailRate 2 -TailSeconds 4 -PreSaleSeconds 2 -PreAllocatedVus 32 -AbandonPercent 0 -CancelPercent 20 -MaxQuantity 2 -ThinkMaxSeconds 1 -SkipBuild
```

Fem bläddringsflöden/sekund i 10 sekunder, med två sekunders bläddring innan
köptrafiken börjar. Köpförsöken kommer med fem/sekund i fyra sekunder och sedan
två/sekund i fyra sekunder: 28 försök för 10 biljetter. Varje försök begär en eller
två biljetter. De planerade kundgrupperna är 20% avbokning och 80% betalning, med
upp till en sekunds betänketid. Gruppfördelningen bland accepterade reservationer
beror på konkurrensen om biljetterna.

### Tävling om sista biljetten

```powershell
powershell -NoProfile -File tests/load/run.ps1 -Scenario Release -Capacity 1 -BurstRate 20 -BurstSeconds 2 -TailRate 5 -TailSeconds 2 -PreSaleSeconds 2 -PreAllocatedVus 32 -AbandonPercent 0 -CancelPercent 0 -MaxQuantity 1 -ThinkMaxSeconds 0 -SkipBuild
```

Fem bläddringsflöden/sekund i sex sekunder. Efter två sekunder börjar köpförsöken
med 20/sekund i två sekunder och fem/sekund i två sekunder: 50 försök för en
biljett. Varje försök begär en biljett och försöker betala direkt.

Båda biljettsläppskörningarna förallokerar 32 virtuella användare per köpfas plus
16 för bläddring (80 totalt). De använder en tidsgräns på 60 sekunder för
betalning/biljetter och standardgränsen på 720 sekunder för slutavstämning.
Ingen av körningarna överger reservationer. Ett köp görs mot ett separat
uppvärmningsevenemang med en biljett före mättrafiken och utesluts från
avstämningen av uppmätt lager och utfall.

Alla testdata använder ståplatslager med priset 100 SEK per biljett och ett
evenemang ett år framåt i tiden vars försäljning redan är öppen. Betalningarna
använder `Succeeded`, en fördröjning på 500 ms och en tidsgräns på 5 000 ms.
Alla HTTP-anrop i flödena går genom gatewayen. SQL skapar testdata, ställer in
simuleringsutfall och läser av sluttillståndet. Varje körning använder nya data
och städar upp sina egna containrar, nätverk och volymer.

## Dokumenterade körningar och utfall

Resultatkatalogernas namn använder lokal tid; starttiderna nedan anges i UTC.
Fullständiga JSON-filer och tjänsteloggar finns kvar lokalt under
`tests/load/results/<run>/`, som ignoreras av Git. Tabellerna i rapporten bevarar
sammanfattningen av mätresultaten i repot.

| Körning | Resultatkatalog | Startad (UTC) | Samlat resultat |
| --- | --- | --- | --- |
| Checkout | `20261109-141643-3b390fae` | 13:16:43 | Godkänd |
| Kort biljettsläpp | `20261109-141909-996471bf` | 13:19:09 | Godkänd |
| Sista biljetten | `20261109-142040-e2b5d78d` | 13:20:40 | Godkänd |

Varje `outcome.json` registrerade `k6ExitCode: 0`, `verificationPassed: true`,
`verificationFailure: null` och `passed: true`. Alla tre körskriptsprocesser
avslutades med kod 0 och slutförde städningen. Båda biljettsläppskörningarnas
uppvärmningskörningar godkändes också.

| Uppmätt utfall | Checkout | Kort biljettsläpp | Sista biljetten |
| --- | ---: | ---: | ---: |
| Köpförsök | 10 | 28 | 50 |
| Accepterade reservationer | 10 | 9 | 1 |
| Betalda bokningar | 10 | 7 | 1 |
| Kundavbokningar | 0 | 2 | 0 |
| Förväntade lageravslag | 0 | 19 | 49 |
| Övergivna reservationer | 0 | 0 | 0 |
| Betalningsavslag | 0 | 0 | 0 |
| Andel HTTP-fel | 0% | 0% | 0% |
| Misslyckade kontroller / totalt antal kontroller | 0 / 167 | 0 / 325 | 0 / 185 |
| Uteblivna iterationer | Rapporteras inte av denna exekveringstyp | 0 | 0 |

Båda biljettsläppskörningarna registrerade `release_unexpected_error = 0`.
Försöksräknarna motsvarade den planerade belastningen. HTTP 409-svar för
lagersaldo accepterades bara med den förväntade problemrubriken för otillgängliga
biljetter. Tävlingen om sista biljetten gav exakt en lyckad köpare och 49 lageravslag.

### Svarstider och genomströmning

Värdena nedan kommer från varje uppmätt `summary.json`. Tiderna anges i
millisekunder, avrundade till två decimaler; p50 är det exporterade värdet `med`.

| Körning / mätvärde | p50 (ms) | p95 (ms) | p99 |
| --- | ---: | ---: | --- |
| Checkout / `checkout_duration` | 3853.50 | 4333.55 | Exporteras inte |
| Checkout / `http_req_duration` | 17.42 | 64.78 | Exporteras inte |
| Kort biljettsläpp / `release_reservation_duration` | 38.55 | 109.98 | Exporteras inte |
| Kort biljettsläpp / `release_paid_duration` | 5846.00 | 6522.40 | Exporteras inte |
| Kort biljettsläpp / `http_req_duration` | 14.93 | 64.00 | Exporteras inte |
| Sista biljetten / `release_reservation_duration` | 32.25 | 61.63 | Exporteras inte |
| Sista biljetten / `release_paid_duration` | 5709.00 | 5709.00 | Exporteras inte |
| Sista biljetten / `http_req_duration` | 15.19 | 50.45 | Exporteras inte |

Skripten konfigurerar inte export av p99. Värdet kan inte återskapas från dessa
sammanfattade percentiler. Tävlingen har bara ett mätvärde för ett betalt köp;
dess identiska p50-/p95-värden för betalda köp beskriver enbart det köpet.

| Exporterad räknare | Checkout antal / takt per sekund | Kort biljettsläpp antal / takt per sekund | Sista biljetten antal / takt per sekund |
| --- | ---: | ---: | ---: |
| HTTP-anrop | 156 / 5.385 | 229 / 22.772 | 148 / 19.139 |
| Köpförsök | Exporteras inte separat | 28 / 2.784 | 50 / 6.466 |
| Slutförda betalda bokningar | 10 / 0.345 | 7 / 0.696 | 1 / 0.129 |

Takterna är k6:s exporterade räknartakter för hela körningen, inklusive
bläddring/förberedelser och väntan på slutförande där det är tillämpligt.
De konfigurerade ankomsttakterna beskrivs i belastningsavsnittet.
HTTP-mätvärdena för Checkout inkluderar förberedelse- och uppvärmningsanrop;
dess egna köpmätvärden omfattar bara de 10 uppmätta köpen. HTTP-mätvärdena för
Release inkluderar förberedelser och bläddring, medan den separata
uppvärmningsprocessen skriver till `warmup.json`. Tiderna för betalda köpflöden
inkluderar statuskontroller för betalning och betänketid. Lageravslag ingår i
reservationstider och HTTP-mätvärden, men utesluts från tiderna för betalda köpflöden.

### Slutavstämning

Verifieringen krävde två godkända avläsningar i följd, med fem sekunders mellanrum,
för varje körning. Varje `verification.json` registrerade `passed: true`,
`consecutivePasses: 2` och en tom `issues`-lista.

| Sluttillstånd | Checkout | Kort biljettsläpp | Sista biljetten |
| --- | ---: | ---: | ---: |
| Lagerkapacitet | 1000 | 10 | 1 |
| Bokningsrader | 11 | 9 | 1 |
| Bekräftade bokningar | 11 | 7 | 1 |
| Bekräftat biljettantal | 11 | 10 | 1 |
| Giltiga biljetter | 11 | 10 | 1 |
| Lyckade betalningar | 11 | 7 | 1 |
| Tillgängligt antal i SQL | 989 | 0 | 0 |
| Tillgängligt antal i Redis | 989 | 0 | 0 |
| Aktiva Redis-reservationer | 0 | 0 | 0 |

Slutantalen för Checkout inkluderar dess enda uppvärmningsköp. Båda
biljettsläppskolumnerna utesluter sina separata uppvärmningsevenemang.
Bekräftade antal låg inom kapaciteten; biljetter och betalningar stämde med
bokningarna. Inga dubbla biljetter/betalningar, biljetter/betalningar utan
tillhörande bokning, oreglerade debiteringar eller uppmätta bokningar utan
slutstatus hittades. Väntande arbete i bokningarnas outbox/inbox, avvisade
inbox-meddelanden och arbete i betalningarnas outbox var alla noll.
Alla sex köer, inklusive de tre dead-letter-köerna, hade noll meddelanden som
väntade på leverans eller kvittering. Städningen tog bort varje körnings
tillfälliga volymer.

## Dokumenterade avbilds-ID:n

Följande ID:n från `images.json` var identiska i alla tre körningar.
k6 använde den versionslåsta taggen i miljötabellen.

| Avbild | ID |
| --- | --- |
| `halsingelive-load-api-gateway:local` | `sha256:50578bd2c5c117c51d304056cc943cc3a95f5404c733aa5078bc7f6a3eba36ea` |
| `halsingelive-load-booking:local` | `sha256:523398f5b6e7810a72105dfe17eca9e77a11def5bf8a2ca6aa0e0f11d259c7aa` |
| `halsingelive-load-eventcatalog:local` | `sha256:7e87a9288a1b160139d861509eb39185b786b4c57288aab525328b35bfcd0738` |
| `halsingelive-load-payment-simulator:local` | `sha256:964f6144674c7ebb8f3c62aef8bf547d4c7803e8eb56a6c70a9bd0ad9e602643` |
| `halsingelive-load-ticketservice:local` | `sha256:6ffdb7bc2a8bb2589655d8ae521a39f33636ad03303613bce4a682497d86e926` |
| `postgres:18-alpine` | `sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873` |
| `redis:7-alpine` | `sha256:8da19c7399dca900c6822a1cf30f9d2285585e2c4b457ff4095b0f4500054d3f` |
| `rabbitmq:4-management-alpine` | `sha256:b768d2e09a809b48458d6c3c32fc2894643a6bfafd76264c120373eff0ee6532` |

## Slutsatser och återstående validering

De dokumenterade körningarna testade köpflödet fram till biljettutfärdande,
bläddring i katalogen, kundavbokning, förväntad lagerbrist och en separat tävling
om sista biljetten. Slutliga antal, betalningar, biljetter, Redis-tillstånd och köer
stämde överens i alla tre körningar. Tävlingen om sista biljetten bekräftade att
bara ett av 50 försök köpte den enda tillgängliga biljetten i denna körning.

Ärende #126 är fortfarande inte slutfört:

- Definiera konfigurerbar upptrappning och tröskelvärden för svarstider/felandel
  före representativa prestandakörningar. Nuvarande faser har fasta ankomsttakter,
  korrekthetskraven är fasta och svarstiderna saknar prestandamål för godkänt/underkänt.
- Exportera p99 genom `summaryTrendStats` och dokumentera värdet i en senare rapport.
- Samla representativa resultat för biljettsläpp och ihållande trafik på avsedd
  hårdvara med överenskomna mål. Dessa körningar har för få mätvärden för betalda
  köp för att tillförlitligt uppskatta kapacitet eller de högsta svarstidspercentilerna.
- Det fullständiga standardbiljettsläppet med verklig utgång efter tio minuter för
  övergivna reservationer, ihållande trafik och betalningsutfallen `Failed`/`TimedOut`
  mot tjänsterna kördes inte här. Regressionstäckningen för motsvarande beteende
  i testverktygen redovisas separat ovan.
- Verifiera att varje numrerad stol bara säljs en gång när flödet för stolval finns.
  Dessa testdata kontrollerar ståplatslager samt biljetternas identitet och antal.

Använd [lasttesternas README](README.md) för att upprepa körningarna. Bevara varje
ny rapport utanför den ignorerade resultatkatalogen, med belastning, miljö,
sammanfattade mätresultat och underlag för slutavstämningen.
