# Begränsningar av reservationsförsök

`POST /booking-api/CreateBooking` begränsas i Booking med Redis. Alla instanser
delar räknare och samma normaliserade klientadress från
[den betrodda proxykedjan](client-addressing.md). Förfalskade headers väljer inte
en ny identitet. IPv4 och motsvarande IPv4-mappad IPv6 räknas tillsammans.

## Konfiguration och standardvärden

| Compose-variabel | Booking-inställning under `ReservationLimits` | Standard |
| --- | --- | --- |
| `BOOKING_RESERVATION_ATTEMPTS` | `AttemptsPerWindow` | 10 försök |
| `BOOKING_RESERVATION_WINDOW_SECONDS` | `WindowSeconds` | 60 sekunder |
| `BOOKING_RESERVATION_MAX_ACTIVE` | `MaxActiveReservationsPerEvent` | 2 reservationer |
| `BOOKING_RESERVATION_MAX_QUANTITY` | `MaxReservedQuantityPerEvent` | 10 biljetter |
| `BOOKING_RESERVATION_HMAC_KEY` | `HmacKey` | Obligatorisk hemlighet: 64 hextecken |

Direktkörning använder exempelvis `ReservationLimits__HmacKey` som miljövariabel;
`dotnet run` läser inte rotens `.env`. Ogiltiga eller saknade värden stoppar
Booking vid start. Generera hemligheten enligt [konfigurationsguiden](configuration.md).
Alla instanser i samma miljö måste använda samma nyckel, gränsvärden och
`KeyPrefix` (standard `booking:reservation-limits:v1`). Miljöer ska ha separata
Redis-databaser eftersom även lagernycklarna delas inom databasen.

Försöksgränsen gäller per IP över samtliga evenemang. Den använder ett rullande
tidsfönster baserat på Redis klocka. Grundläggande giltiga bokningsanrop räknas
innan lager och evenemang hämtas: även senare 404, 409 och nekad aktiv kvot
förbrukar försök. Felaktiga bokningsuppgifter som ger valideringsfel räknas inte.
Anrop som redan nekas av försöksgränsen förlänger inte fönstret.

De två aktiva gränserna gäller per IP och evenemang, summerat över alla sektioner.
En reservation kan gälla flera biljetter. Bekräftat betalda bokningar räknas inte
längre som aktiva. Befintlig validering tillåter högst 10 biljetter per anrop;
en lägre konfigurerad total biljettgräns ger 400 om det begärda antalet aldrig
skulle få plats i kvoten.

## NAT och kundupplevelse

Kunder bakom samma publika IP delar alla gränser. Det gäller exempelvis familjer,
företag, skolor, mobilnät och VPN. Två samtidiga reservationer är en försiktig
standard som behöver utvärderas mot verklig trafik. Enskilda IP-adresser ger
inget fullständigt skydd mot klienter med många adresser eller IPv6-adresser.
Lokalt delar anrop från samma loopbackadress också kvot.

Vid överskriden gräns får kunden 429 med svensk förklaring och `Retry-After`
i hela sekunder. Kundwebben visar väntetiden och behåller formuläruppgifterna.
Ingen ny bokning eller lagerreservation skapas av det nekade anropet. Kvoten kan
bli tillgänglig tidigare om en befintlig reservation avslutas. Ett nytt anrop
kontrollerar alltid båda gränserna igen.

## Atomära ändringar och frigöring

Lua i Redis kontrollerar aktiva kvoter och saldo i samma atomära operation som
skapar reservationen. Försöksräknaren uppdateras separat, före bokningsarbetet.
Parallella anrop och flera Booking-instanser använder samma gränser.

Avbokning, utgång, bekräftad betalning och kompensation efter misslyckad
databaslagring tar bort just reservationens kvotbidrag. Upprepning frigör inte
kvot eller lager ytterligare. Nekade försök förlänger aldrig befintliga
reservationer. Bekräftelse eller avbokning nollställer inte försöksfönstret.

`RedisReservationCleanupService` söker efter utgångna reservationer var 15:e
sekund, upp till 1 000 per omgång i batcher om 100. Indexet ligger i Redis och
överlever en Booking-omstart. Därför hittas även reservationer som aldrig fick
en databasrad efter exempelvis en processkrasch. Nya reservationer och
lagerläsningar städar också relevanta utgångna reservationer. Ordinarie
`BookingExpirationService` uppdaterar bokningsstatus i PostgreSQL.

## Data och driftsättning

Klientnyckeln är HMAC-SHA256 över den normaliserade IP-adressen. Rå IP och
klientnyckel skrivs inte till bokningen eller de nya reservationsloggarna.
HMAC-värdet är stabilt med samma hemlighet; TTL begränsar lagringen, inte hur
länge samma indata ger samma identifierare. Nyckeln roteras inte automatiskt,
eftersom ett okoordinerat byte skulle återställa klienternas kvoter.

Båda Nginx-proxyernas rutinmässiga accessloggar använder JSON med enbart metod,
sökväg utan querysträng, status, antal bytes och svarstid. Klientadress,
forwarding-headers, referrer och user agent ingår inte. Bokningskoden och
gatewayens rutinmässiga requestloggar lägger inte till klientadress.

Nginx felloggar är separat diagnostik och kan innehålla klientadress vid exempelvis
anslutningsfel. Åtkomst till dem kräver behörighet till Docker-värden eller
containerloggarna. Compose använder loggdrivern `local` för båda webbapparna med
rotation vid 10 MB och högst tre filer per container. Det begränsar volymen,
men är ingen tidsbaserad gallring. Loggarna tas bort när containern tas bort;
äldre loggar från tidigare containrar rensas inte retroaktivt av ändringen.
Exporterad feldiagnostik ska hållas åtkomstbegränsad och raderas när felsökningen
är avslutad. CI sparar verifieringsartefakter i sju dagar; lokala testloggar
raderas manuellt efter granskningen. Externa ingressproxyer och loggsamlare behöver motsvarande egna
regler. Kravet att inte rutinlogga klientadress avser access- och reservationsloggar.

Med `p` som `KeyPrefix` används följande tillägg till de befintliga lagernycklarna:

| Redis-nyckel | Innehåll | Retention |
| --- | --- | --- |
| `p:attempts:<HMAC>` | Tidsstämplar och slumpmässiga försöks-ID:n | Ett tidsfönster från senast insläppt försök |
| `p:active:<HMAC>:<event>` | Reservations-ID:n och sluttider | Senaste sluttid + 60 sekunder |
| `p:active:<HMAC>:<event>:quantities` | Antal per reservation | Samma som aktivt index |
| `p:owner:<reservation>` | Referenser till klientens kvotnycklar | Reservationens sluttid + 60 sekunder |
| `p:due` | Reservations-ID:n och sluttider, utan klientidentitet | Poster tas bort vid avslut eller städning |

Reservationshashen innehåller referens till ägarposten, utan HMAC eller rå IP.
Den har ingen egen TTL eftersom metadata behövs för att återställa lagret efter
ett driftavbrott; städningen tar bort den. Befintliga slutmarkörer har 24 timmars TTL.
Vid långvarigt avbrott kan förfallna poster ligga kvar tills Booking kommer upp.
Redis-skripten förutsätter en Redis-databas utan Cluster, som befintlig lagerkod.

Redis-fel vid skapande ger kontrollerad 503. Ingen lokal räknare eller tillåtande
reservväg används. Vid timeout kan Redis ha genomfört en reservation utan att
svaret nått Booking; den förblir då upptagen tills den frigörs eller städas efter
sin ordinarie sluttid (10 minuter). Redis och PostgreSQL har ingen gemensam
transaktion. Redis-persistens, backup och återställning måste hanteras som del av
driften; total förlust av Redis-data återställer även kvoterna.

Vid första utrullningen: stoppa nya reservationsanrop, låt gamla reservationer
avslutas eller löpa ut i 10 minuter och byt därefter alla Booking-instanser.
Äldre reservationer saknar klientkoppling och kan inte räknas retroaktivt.
Byte av HMAC-nyckel eller prefix kräver också att aktiva reservationer är avslutade
och att försöksfönstret har passerat, annars får klienterna nya räknare. Behåll
nyckeln mellan vanliga omstarter.

## Verifiering

[Booking-integrationstesterna](../tests/Booking.IntegrationTests/README.md) använder
riktig Redis och PostgreSQL. `ReservationLimitTests` kontrollerar bland annat
3 försök/60 sekunder, annan käll-IP, återhämtning, falska headers, kvoter mellan
sektioner, betalning, idempotent frigöring, orphan-städning, kompensation och
Redis-avbrott. Tre samtidighetsscenarier skickar vardera 20 HTTP-anrop till två
separata värdar med egna Redis-anslutningar och gemensamma kvoter.

503-testet skapar först en riktig reservation och verifierar sedan att ett
avvisat anrop inte ändrar dess status, sluttid, antal, kvot, försöksräknare eller
Redis-/databaslager. Ett friskt anrop fungerar därefter igen.

[Verifieringen genom hela proxykedjan](../tests/reservation-chain/README.md)
kör riktiga `CreateBooking` genom både frontendproxyer, gateway och Booking med
Redis och PostgreSQL. Den sparar konfiguration, anropsresultat, lager-/kvotsnapshots,
databasläge före/efter och kontrollerade accessloggar. Katalogens syntetiska
evenemangssvar ersätts med en testserver; betalningsflöden startas inte.
