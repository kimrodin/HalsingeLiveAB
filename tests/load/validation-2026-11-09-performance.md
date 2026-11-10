# Kompletterande lasttestvalidering - 2026-11-09

Köpflöde, biljettsläpp med linjär rampning, sista biljetten och ihållande
köptrafik klarade sina fördefinierade svarstidsgränser, korrekthetskrav och
slutavstämningar. Denna rapport kompletterar [den tidigare rapporten](validation-2026-11-09.md)
med de saknade delarna i [issue #126](https://github.com/kimrodin/HalsingeLiveAB/issues/126):
konfigurerbar rampning, svarstids-/feltrösklar samt uppmätt p99.

## Revision och bevarat underlag

Körningarna använde `k6Loadtesting`, basrevision
`c9bec15725198816c79bde9d0c62cc51d85dcd06`, med arbetskopians komplettering av
`config.js`, `checkout.js`, `release.js`, `run.ps1` och `compose.yaml`.
`workingTreeDirty` var därför `true`. Applikationskoden ändrades inte.
Första körningen byggde applikationsavbilderna från aktuell källkod med byggcache;
efterföljande körningar återanvände dem med `-SkipBuild`.

[Den sparade JSON-sammanfattningen](reports/validation-2026-11-09-performance.json)
innehåller varje körnings inställningar, exakta mätvärden, tröskelutfall,
databas-/Redis-/köantal och applikationens/infrastrukturens avbilds-ID:n.
Varje `configuration.sourceSha256` innehåller SHA-256 för alla körningsfiler;
de jämfördes med arbetskopian när sammanfattningen skapades. Detta skiljer
den testade arbetskopian från basrevisionen även innan ändringen har committats.
Fullständiga råfiler och tjänsteloggar finns lokalt i de Git-ignorerade
katalogerna `tests/load/results/<runId>/`.

## Miljö och testdata

| Uppgift | Värde |
| --- | --- |
| Datum/tidszon | 2026-11-09, Europe/Stockholm (UTC+01:00) |
| Värddator | Windows 10.0.26200; Intel Core i7-1255U, 10 kärnor / 12 logiska processorer |
| Docker | Desktop, Linux via WSL2; server 29.8.1; kärna `6.6.87.2-microsoft-standard-WSL2` |
| Docker-resurser | 12 processorer, 8 163 381 248 byte minne (cirka 7,60 GiB) |
| Verktyg | Compose 5.5.1, PowerShell 5.1.26100.8457, Node.js 24.13.1, `grafana/k6:1.3.0` |
| Applikation | Fyra domäntjänster i .NET 10 och API-gatewayen |
| Infrastruktur | `postgres:18-alpine`, `redis:7-alpine`, `rabbitmq:4-management-alpine` |
| Databas | Fyra databaser i en PostgreSQL-container; 100 anslutningar på servern, högst 20 i varje tjänstepool |
| Lagring | Nya projektspecifika diskvolymer per körning, Redis AOF; volymerna togs bort efteråt |

Körningarna gjordes efter varandra på samma dator som k6 och den befintliga
utvecklingsmiljön. CPU-/RAM-användning per tjänst registrerades inte. Testerna
använde nya ståplatsevenemang ett år framåt, med försäljningen redan öppen och
biljettpris 100 SEK. Betalningssimulatorn använde `Succeeded`, 500 ms planerad
fördröjning och 5000 ms tidsgräns. Alla köp- och kataloganrop gick via gatewayen.
SQL användes för testdata, betalningspolicy och slutavläsning.

## Kommandon, belastning och trösklar

Kommandona kördes från repots rot i nedanstående ordning. Docker Desktop behöver
vara igång. Utelämna `-SkipBuild` efter ändringar i applikationskoden.

```powershell
# Checkout: 10 uppmätta köp med 2 VU, plus ett inbyggt uppvärmningsköp.
powershell -NoProfile -File tests/load/run.ps1 -Scenario Checkout

# Kort biljettsläpp: ramp 0 -> 5/s på 3 s, sedan 5/s i 4 s och 2/s i 4 s.
powershell -NoProfile -File tests/load/run.ps1 -Scenario Release -Capacity 10 -BurstRate 5 -BurstSeconds 4 -TailRate 2 -TailSeconds 4 -PreSaleSeconds 2 -RampSeconds 3 -PreAllocatedVus 32 -AbandonPercent 0 -CancelPercent 20 -MaxQuantity 2 -ThinkMaxSeconds 1 -SkipBuild

# Sista biljetten: 50 köpförsök för kapacitet 1, utan ramp eller kundavbokning.
powershell -NoProfile -File tests/load/run.ps1 -Scenario Release -Capacity 1 -BurstRate 20 -BurstSeconds 2 -TailRate 5 -TailSeconds 2 -PreSaleSeconds 2 -RampSeconds 0 -PreAllocatedVus 32 -AbandonPercent 0 -CancelPercent 0 -MaxQuantity 1 -ThinkMaxSeconds 0 -SkipBuild

# Ihållande köp: ramp 0 -> 5/s på 4 s, sedan 5/s i 10 s och 3/s i 20 s.
powershell -NoProfile -File tests/load/run.ps1 -Scenario Sustained -Capacity 100000 -BurstRate 5 -BurstSeconds 10 -TailRate 3 -TailSeconds 20 -PreSaleSeconds 2 -RampSeconds 4 -PreAllocatedVus 64 -AbandonPercent 0 -CancelPercent 0 -MaxQuantity 1 -ThinkMaxSeconds 0 -HttpP95Ms 400 -HttpP99Ms 800 -CheckoutP95Ms 12000 -CheckoutP99Ms 18000 -MaxHttpErrorRate 0.01 -SkipBuild

# Avsiktligt tröskelbrott: samma korrekta köpflöde, men HTTP-budget 1 ms.
powershell -NoProfile -File tests/load/run.ps1 -Scenario Checkout -Vus 1 -Iterations 2 -HttpP95Ms 1 -HttpP99Ms 1 -SkipBuild
```

De tre första mätkörningarna använde standardbudgetarna HTTP p95 <= 500 ms,
p99 <= 1000 ms, köpflöde p95 <= 15000 ms och p99 <= 20000 ms samt HTTP-felandel
<= 0. Sustained använde sina uttryckliga budgetar ovan, inklusive HTTP-felandel
<= 0,01. Dessa lokala budgetar valdes före körningarna och justerades inte efter
mätresultaten. De utgör inte ett produktions-SLA.

Release gav 7 försök under rampen och 28 på platåerna, totalt 35.
Sustained gav 9 försök under rampen och 110 på platåerna, totalt 119.
Rampen räknar endast starter strikt före fasens slut. När arean under rampen
är ett heltal, som i Sustained, utesluts en eventuell iteration exakt på
slutgränsen före HTTP-anrop. Detta undviker skillnader som beror på k6:s
schemaläggning vid tidsgränsen; alla förväntade inre starter måste fortfarande ske.

Release/Sustained läste katalogen med 5 bläddringsflöden/s under hela
trafikperioden, med 2 sekunders bläddring före köpen. Varje bläddringsflöde läste
lista, evenemang och lager. Kort Release köpte 1–2 biljetter med upp till 1 sekunds
betänketid och planerade 20% kundavbokning. Övriga köp använde en biljett,
ingen betänketid och ingen kundavbokning. Release hade 112 förallokerade VU,
sista biljetten 80 och Sustained 208, inklusive bläddring och samtliga köpfaser.
Alla använde 60 sekunders tidsgräns för betalning/biljetter och 720 sekunder för
slutavstämning. Checkout använde sin tidsgräns på 30 sekunder per köp.

## Mätresultat

Resultatkatalogernas namn anges i lokal tid; starttiderna nedan är UTC.

| Körning | runId | Start (UTC) | Resultat |
| --- | --- | --- | --- |
| Checkout | `20261109-150313-ae5a8a63` | 14:03:13 | Godkänd |
| Release med ramp | `20261109-150458-fcd37f29` | 14:04:58 | Godkänd |
| Sista biljetten | `20261109-150626-a0dd1931` | 14:06:26 | Godkänd |
| Sustained med ramp | `20261109-150916-03a45326` | 14:09:16 | Godkänd |

Alla fyra hade `k6ExitCode: 0`, `verificationPassed: true` och `passed: true`.
Körskripten avslutades med kod 0 och städade sina egna containrar och volymer.

| Uppmätt utfall | Checkout | Release | Sista biljetten | Sustained |
| --- | ---: | ---: | ---: | ---: |
| Köpförsök | 10 | 35 | 50 | 119 |
| Accepterade reservationer | 10 | 10 | 1 | 119 |
| Betalda bokningar | 10 | 7 | 1 | 119 |
| Kundavbokningar | 0 | 3 | 0 | 0 |
| Förväntade lageravslag | 0 | 25 | 49 | 0 |
| HTTP-felandel | 0% | 0% | 0% | 0% |
| Misslyckade kontroller / totalt | 0 / 159 | 0 / 392 | 0 / 189 | 0 / 2432 |
| Uteblivna iterationer | Ej rapporterat av exekveringstypen | 0 | 0 | 0 |

Övergivna reservationer och betalningsavslag var noll i dessa fyra körningar.
`release_unexpected_error` var noll i samtliga tre körningar med `release.js`.

### Svarstider, millisekunder

Värdena kommer direkt från `summary.json`, avrundade till två decimaler.
p50 motsvarar `med`; N är antalet mätvärden för respektive fördelning.

| Körning / mätvärde | N | p50 | p95 | p99 |
| --- | ---: | ---: | ---: | ---: |
| Checkout / HTTP | 148 | 12.79 | 61.67 | 772.76 |
| Checkout / hela köpet | 10 | 3800.50 | 4201.70 | 4206.74 |
| Release / HTTP | 280 | 27.89 | 193.86 | 398.47 |
| Release / reservation | 35 | 98.35 | 400.41 | 425.87 |
| Release / betalt köp | 7 | 6188.00 | 7013.10 | 7141.02 |
| Sista biljetten / HTTP | 151 | 31.58 | 196.04 | 357.38 |
| Sista biljetten / reservation | 50 | 73.78 | 316.71 | 382.36 |
| Sista biljetten / betalt köp | 1 | 5776.00 | 5776.00 | 5776.00 |
| Sustained / HTTP | 1508 | 34.32 | 270.27 | 492.14 |
| Sustained / reservation | 119 | 86.13 | 476.74 | 660.09 |
| Sustained / betalt köp | 119 | 6305.00 | 8882.30 | 9459.86 |

HTTP-fördelningen inkluderar katalogläsning, förberedelser och lageravslag.
Checkout inkluderar även sitt inbyggda uppvärmningsköps HTTP-anrop. Övriga
scenariers separata uppvärmning är utesluten och sparas i `warmup.json`;
den kontrollerar korrekthet men har inga svarstidsgränser. Köptider inkluderar
betänketid och asynkron betalnings-/biljettpollning. `release_paid_duration`
innehåller enbart lyckade köp; lageravslag ingår i reservationstiderna.

### Genomströmning

Exporterade räknartakter gäller hela k6-processen, inklusive bläddring och väntan
på slutförande. De motsvarar inte direkt den konfigurerade ankomsttakten.

| Körning | HTTP-anrop / s | Köpförsök / s | Slutförda betalda köp / s |
| --- | ---: | ---: | ---: |
| Checkout | 5.206 | Exporteras inte separat | 0.352 |
| Release | 21.442 | 2.680 | 0.536 |
| Sista biljetten | 19.344 | 6.405 | 0.128 |
| Sustained | 36.482 | 2.879 | 2.879 |

## Slutavstämning mot tjänsternas databaser

Två godkända avläsningar i följd, fem sekunder isär, krävdes per körning.
Alla hade `consecutivePasses: 2` och tom `issues`. Avstämningslogiken kördes
också om mot de sparade råtillstånden utan avvikelser när JSON-underlaget skapades.

| Sluttillstånd | Checkout | Release | Sista biljetten | Sustained |
| --- | ---: | ---: | ---: | ---: |
| Kapacitet | 1000 | 10 | 1 | 100000 |
| Bekräftade bokningar | 11 | 7 | 1 | 119 |
| Bekräftat biljettantal | 11 | 10 | 1 | 119 |
| Giltiga biljetter | 11 | 10 | 1 | 119 |
| Lyckade betalningar | 11 | 7 | 1 | 119 |
| Tillgängligt SQL-lager | 989 | 0 | 0 | 99881 |
| Tillgängligt Redis-lager | 989 | 0 | 0 | 99881 |
| Aktiva Redis-reservationer | 0 | 0 | 0 | 0 |

Checkout inkluderar sitt uppvärmningsköp i slutantalet. Övriga scenarier
utesluter sitt separata uppvärmningsevenemang från avstämningen av mätlagret.
Verifieringen fann ingen överförsäljning, dubbla/felaktiga biljetter, dubbla
betalningar, biljetter/betalningar utan bokning eller bokningar utan slutstatus.
Outbox-/inbox-arbete och avvisade inbox-meddelanden var noll. Alla sex RabbitMQ-köer,
inklusive dead-letter-köerna, hade noll väntande och okvitterade meddelanden.

## Verifierat tröskelbrott

Kontrollkörningen `20261109-151128-6d5f083c` startade 14:11:28 UTC och använde
HTTP p95/p99 <= 1 ms. De 41 HTTP-anropen hade p50 15.49 ms, p95 564.74 ms och
p99 1588.84 ms. Båda HTTP-latenströsklarna bröts som avsett; k6 avslutades med
kod 99 och körskriptet med kod 1.

Båda uppmätta köp lyckades och alla 44 kontroller godkändes. Slutavstämningen
godkändes två gånger: tre bekräftade bokningar, betalningar och giltiga biljetter
inklusive uppvärmningen, 997 tillgängliga biljetter i SQL/Redis samt inga aktiva
reservationer eller väntande köjobb. Trots detta sparade `outcome.json`
`passed: false`, `verificationPassed: true` och `k6ExitCode: 99`.
Städningen slutfördes även här. Kontrollen visar att svarstidskraven faktiskt
påverkar hela testresultatet och inte bara redovisas som mätvärden.

## Regression och slutsatser

Compose-konfigurationen validerades utan fel. PowerShells avstämningstester
godkände 16 fall, och Nodes testkörare godkände 16 tester. De nya fallen täcker
fasernas förskjutning, både heltals- och halvtalsarea under rampen, bevarade 50
försök utan rampning, konfigurerbara budgetar i båda k6-skripten, p99-export,
ogiltiga inställningar och den separata uppvärmningens korrekthetskrav.

```powershell
docker compose -f tests/load/compose.yaml config --quiet
powershell -NoProfile -File tests/load/verify.tests.ps1
node --experimental-vm-modules --test tests/load/release.tests.cjs
```

Kriterierna om konfigurerbar rampning, svarstids-/feltrösklar och rapportens p99
är nu implementerade och provade mot riktiga tjänster. Sista biljetten såldes
en gång av 50 försök, och ihållande trafik slutförde samtliga 119 köp.

Detta är fortsatt lokal validering. Små urval, särskilt ett enda köp i sista
biljetten-testet, ger begränsat underlag för svanspercentiler. Sustained ger fler
mätvärden men kördes bara med 5 respektive 3 nya köpflöden/s och är ingen sökning
efter maximal kapacitet. Produktionskapacitet kräver längre representativa
körningar, avsedd databastopologi/resursbudget och överenskomna tjänstemål.
Full standardkörning med tio minuters utgång av övergivna reservationer ingick
inte i denna komplettering. Unik försäljning av numrerad stol återstår enligt
issuets avgränsning tills stolflödet finns.
