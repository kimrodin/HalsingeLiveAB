# RabbitMQ och meddelandeflöden

## Översikt

RabbitMQ används för att skicka meddelanden mellan projektets tjänster när arbetet kan fortsätta i bakgrunden. En tjänst publicerar ett meddelande om något som har hänt eller behöver göras. RabbitMQ placerar meddelandet i rätt kö, där den mottagande tjänsten hämtar och behandlar det. Avsändaren behöver alltså inte vänta på att mottagaren slutför hela arbetet.

De tre berörda tjänsterna har olika ansvar:

- **Booking** hanterar bokningar, reservationer och lagersaldo. När betalningen är klar bekräftar tjänsten bokningen och begär att biljetter utfärdas.
- **PaymentSimulator** behandlar simulerade betalningar och publicerar resultatet: betalningen lyckades, misslyckades eller nådde sin tidsgräns.
- **TicketService** tar emot bekräftade bokningsrader och skapar en biljett per bokad plats.

RabbitMQ används för betalningsbegäran, betalningsresultat och biljettutfärdande. Kundwebben startar flödet genom Bookings HTTP-API. Booking sparar då betalningsbegäran i sin databas och svarar `202 Accepted` med status `Pending`. En bakgrundsarbetare skickar begäran till PaymentSimulator via RabbitMQ. Kunden följer sedan bokningens status genom polling; reservationens ursprungliga sluttid gäller hela tiden.

```mermaid
sequenceDiagram
    participant C as Kundwebb
    participant B as Booking
    participant R as RabbitMQ
    participant P as PaymentSimulator
    participant T as TicketService

    C->>B: HTTP POST /StartPayment/{bookingId}
    B->>B: Spara betalningsavsikt och request-outbox atomärt
    B-->>C: HTTP 202 Accepted, Pending
    B->>R: AMQP: PaymentRequestedV1 från outbox
    R->>P: AMQP: leverera betalningsbegäran
    P->>P: Spara betalningsresultat och outbox
    P->>R: AMQP: PaymentSucceededV1 eller PaymentFailedV1
    R->>B: AMQP: leverera betalningsresultat
    B->>B: Spara resultat i inbox
    B->>P: HTTP: hämta aktuell betalningsstatus för verifiering
    P-->>B: Aktuell betalningsstatus
    B->>B: Tillämpa verifierat resultat
    opt Betalningen godkänns och bokningen kan bekräftas
        B->>B: Bekräfta bokning och spara biljett-outbox
        B->>R: AMQP: BookingConfirmedV1 per bokningsrad
        R->>T: AMQP: leverera bekräftad bokningsrad
        T->>T: Spara biljetter
    end
    Note over B,P: HTTP används även vid kompensation: spärrning eller återbetalning
```

Betalningsbegäran mellan tjänsterna följer alltså vägen **Booking → RabbitMQ → PaymentSimulator**. Diagrammets första HTTP-anrop kommer från kundwebben till Booking. Det senare HTTP-anropet till PaymentSimulator verifierar en betalning som redan har behandlats.

HTTP används även för bland annat uppslag av betalningsstatus, återbetalningar, biljettfrågor och PDF-hämtning. Se även [betalningsflödet](payments.md) och [administration av betalningssimuleringen](payment-simulation-admin.md).

### Vilka flöden är inkopplade?

| Meddelande | Avsändare → mottagare | Nuvarande användning |
| --- | --- | --- |
| `PaymentRequestedV1` | Booking → PaymentSimulator | Startar betalningen via Bookings request-outbox och PaymentSimulators konsument. |
| `PaymentSucceededV1` | PaymentSimulator → Booking | Levererar lyckade betalningar via outbox, RabbitMQ och inbox. |
| `PaymentFailedV1` | PaymentSimulator → Booking | Levererar slutliga utfall `Failed` och `TimedOut` via samma resultatflöde. |
| `BookingConfirmedV1` | Booking → TicketService | Begär biljettutfärdande för varje bekräftad bokningsrad. |

## Begrepp i implementationen

| Begrepp | Betydelse i projektet |
| --- | --- |
| Publicerare / konsument | Kod som skickar respektive tar emot meddelanden. |
| Exchange och routing key | En exchange tar emot publiceringar. Meddelandets routing key avgör vilka bundna köer som får det. |
| Kö | Lagrar meddelanden tills konsumenten kan behandla dem. |
| Publisher confirm | RabbitMQ bekräftar för publiceraren att meddelandet har tagits emot. Det betyder inte att mottagaren har slutfört arbetet. |
| Acknowledgement, `ack` | Konsumenten kvitterar leveransen. Kvittensen lämnas när konsumenten har fullgjort sitt ansvar för meddelandet. |
| Outbox | En databastabell med meddelanden som ska skickas. Affärsändringen och meddelandet sparas tillsammans så att publicering kan återupptas efter avbrott. |
| Inbox | En databastabell med mottagna meddelanden som ska behandlas lokalt. Booking använder den för betalningsresultat. |
| Dead-letter-kö, DLQ | En separat kö för leveranser som konsumenten avvisar, exempelvis på grund av ogiltigt innehåll. |
| Idempotens | Samma begäran eller händelse kan behandlas igen utan att betalning, lagerändring eller biljetter skapas en gång till. |

## Kontrakt och metadata

Kontrakten delas av tjänsterna och finns i [Contracts/Messaging](../src/Contracts/Messaging). De är versionsmärkta med `V1` och serialiseras som JSON.

| Kontrakt | Innehåll |
| --- | --- |
| [PaymentRequestedV1](../src/Contracts/Messaging/PaymentRequestedV1.cs) | `BookingId`, bokningens totala `Amount`, `Currency` och meddelandemetadata. PaymentSimulator tilldelar betalnings-ID när försöket skapas. |
| [PaymentSucceededV1](../src/Contracts/Messaging/PaymentSucceededV1.cs) | Betalningens `PaymentId`, `BookingId`, `Amount`, `Currency` och `PaidAtUtc`, samt meddelandemetadata. |
| [PaymentFailedV1](../src/Contracts/Messaging/PaymentFailedV1.cs) | Samma betalningsidentifierare och belopp, samt `FailureCode`: `Failed` eller `TimedOut`. |
| [BookingConfirmedV1](../src/Contracts/Messaging/BookingConfirmedV1.cs) | En bokningsrad med `BookingItemId`, evenemangs- och platsinformation, `Quantity`, `UnitPrice` och `Currency`, samt meddelandemetadata. |

Följande metadata följer varje meddelande:

- `MessageId` identifierar meddelandet och ska behållas vid nya leveransförsök.
- `CorrelationId` kopplar samman meddelanden i samma flöde. Projektets publicerare använder bokningens ID.
- `OccurredAtUtc` anger när begäran eller utfallet inträffade. Tiden ändras inte när publiceringen görs om.

Alla kontraktsfält är `required` och saknar genererade ID:n eller standardvärden för pengar. Mottagaren måste ändå kontrollera innehållet: ett angivet men tomt GUID eller ett ogiltigt belopp är fortfarande felaktigt. Valuta ska vara en uttrycklig kod med tre stora bokstäver. Betalningsbelopp måste vara positiva; ett uttryckligen angivet biljettpris på noll är tillåtet.

`PaidAtUtc` anger när betalningen lyckades, medan `OccurredAtUtc` beskriver händelsens tidpunkt. En senare återbetalning ändrar inte det historiska framgångsmeddelandet. Ett nätverksavbrott eller en otillgänglig broker är inte ett slutligt betalningsfel och ska inte omvandlas till `PaymentFailedV1`.

## Betalningsbegäran: från Booking till PaymentSimulator

### 1. Spara begäran och svara kunden

`StartPaymentAsync` i [StartPayment.cs](../src/services/Booking/features/bookings/Payments/StartPayment.cs) kontrollerar att bokningen kan betalas och läser beloppet från bokningsdatabasen. Därefter sparas `PaymentRequested = true` och en [PaymentRequestOutboxMessage](../src/services/Booking/features/payments/Requests/PaymentRequestOutboxMessage.cs) i samma transaktion. Om lagringen av meddelandet misslyckas rullas även betalningsavsikten tillbaka.

Ett unikt index på `BookingId` och det gemensamma betalningslåset förhindrar flera begäranden för samma bokning. Den första accepterade starten svarar `202 Accepted` med `Pending` utan att kontakta PaymentSimulator eller RabbitMQ. Ett upprepat anrop återanvänder det befintliga flödet och kan även läsa och tillämpa ett redan tillgängligt resultat.

### 2. Publicera från request-outbox

[PaymentRequestOutboxWorker](../src/services/Booking/features/payments/Requests/PaymentRequestOutboxWorker.cs) kör [PaymentRequestOutboxDispatcher](../src/services/Booking/features/payments/Requests/PaymentRequestOutboxDispatcher.cs) ungefär varje sekund. Dispatchern hämtar högst 25 förfallna rader och använder den sparade ögonblicksbilden för att skapa `PaymentRequestedV1`. Radens ID blir `MessageId`, bokningens ID blir `CorrelationId` och skapandetiden blir `OccurredAtUtc`.

Booking registrerar [IPaymentRequestedPublisher](../src/services/Booking/features/payments/Requests/IPaymentRequestedPublisher.cs) med implementationen [RabbitMqPaymentRequestedPublisher](../src/services/Booking/features/payments/Requests/RabbitMqPaymentRequestedPublisher.cs). Publiceraren använder tjänstens gemensamma RabbitMQ-anslutning och skickar beständig JSON med publisher confirms och obligatorisk routning (`mandatory`).

`DeliveredAtUtc` sätts först efter brokerbekräftelsen. Vid publiceringsfel ligger raden kvar med `AttemptCount`, `LastError` och `NextAttemptAtUtc`. Nya försök använder samma meddelande-ID, belopp, valuta och tidpunkt. Ett avbrott efter publicering men före leveransmarkeringen kan därför ge en dubblett, vilket mottagarens idempotens hanterar.

### 3. Behandla betalningsbegäran

1. [PaymentRequestedConsumer](../src/services/PaymentSimulator/messaging/Requests/PaymentRequestedConsumer.cs) läser från begäranskön och deserialiserar `PaymentRequestedV1`.
2. [PaymentRequestedHandler](../src/services/PaymentSimulator/messaging/Requests/PaymentRequestedHandler.cs) kontrollerar metadata, boknings-ID, belopp och valuta och anropar sedan `AcceptAsync` i [ProcessPaymentHandler](../src/services/PaymentSimulator/features/payments/ProcessPayment/ProcessPaymentHandler.cs).
3. Betalningen sparas med `BookingId` som idempotensnyckel. Samma bokning med samma belopp och valuta återanvänder det befintliga försöket. Ändrat belopp eller ändrad valuta betraktas som en konflikt.
4. Konsumenten kvitterar så snart betalningen är beständigt sparad eller ett befintligt försök har verifierats. Den väntar inte på simuleringens fördröjning, så nästa begäran kan behandlas medan tidigare betalningar fortfarande är `Pending`. Även befintliga terminala utfall kvitteras normalt.

[PaymentRecoveryWorker](../src/services/PaymentSimulator/features/payments/ProcessPayment/PaymentRecoveryWorker.cs) kontrollerar förfallna `Pending`-betalningar ungefär varje sekund och sparar utfallet tillsammans med resultat-outbox. Betalningarna finns kvar för återhämtning även efter att begäran har kvitterats eller tjänsten har startats om. Det direkta HTTP-anropet `POST /api/payments` använder fortsatt `Handle` och väntar på resultatet enligt sin befintliga tidsgräns.

### 4. Hantera köfördröjning och utgångna reservationer

Före publicering kontrollerar dispatchern bokningens aktuella status och reservationstid. Om reservationen redan är ogiltig markeras raden med `SuppressedAtUtc` och skickas inte. Det betyder inte att betalningsflödet är färdighanterat: ett tidigare osäkert publiceringsförsök kan redan ha nått RabbitMQ.

[PaymentReconciliationService](../src/services/Booking/features/bookings/Payments/PaymentReconciliationService.cs) anropar därför `ReconcilePaymentAsync` för oavslutade betalningar. Avstämningen startar aldrig en betalning via HTTP. Om reservationen är giltig men ingen betalning ännu finns väntar den på meddelandeflödet. Om bokningen har avbokats eller reservationen har gått ut används HTTP för att spärra ett ännu inte skapat försök eller avbryta/återbetala ett befintligt försök. Flödet markeras som avslutat först när kompensationen har verifierats.

Ett meddelande som redan köats kan levereras efter reservationens sluttid. En etablerad spärr gör att PaymentSimulator återanvänder det terminala försöket utan en ny debitering. Om betalningen hinner lyckas före spärren återbetalas den när Booking behandlar resultatet eller stämmer av betalningen. Meddelandekontraktet innehåller ingen reservationsdeadline, så mottagaren kan inte själv garantera att en försenad begäran aldrig debiteras före kompensationen.

## Betalningsresultat: från PaymentSimulator till Booking

### 1. Spara utfallet och meddelandet tillsammans

`CompleteDuePayments` i [ProcessPaymentHandler](../src/services/PaymentSimulator/features/payments/ProcessPayment/ProcessPaymentHandler.cs) uppdaterar förfallna `Pending`-betalningar till sitt planerade slutliga utfall och skapar en rad i `PaymentResultOutboxMessages` i samma PostgreSQL-sats. Om outbox-insert misslyckas rullas även statusändringen tillbaka. Detta gäller oavsett om betalningen startades via HTTP eller RabbitMQ, och även när återställningsarbetaren slutför den.

[PaymentResultOutboxMessage](../src/services/PaymentSimulator/messaging/Results/PaymentResultOutboxMessage.cs) sparar en ögonblicksbild av betalnings-ID, boknings-ID, belopp, valuta, utfall och tidpunkter. Ett unikt index på `PaymentId` förhindrar flera resultatrader för samma betalning.

Avbokning och återbetalning hanteras också:

- Avbokning av ett väntande försök ger `TimedOut` och sparar felresultatet atomärt i [RefundPaymentHandler](../src/services/PaymentSimulator/features/payments/RefundPayment/RefundPaymentHandler.cs).
- Avbokning innan ett betalningsförsök finns sparar både en spärr mot senare betalningsstart och ett `TimedOut`-meddelande tillsammans i `ProcessPaymentHandler.Cancel`.
- Återbetalning av en redan lyckad betalning bevarar dess ursprungliga framgångsmeddelande. Återbetalning är inte ett nytt `PaymentFailedV1`.

### 2. Publicera från outbox

[PaymentResultOutboxWorker](../src/services/PaymentSimulator/messaging/Results/PaymentResultOutboxWorker.cs) kör regelbundet [PaymentResultOutboxDispatcher](../src/services/PaymentSimulator/messaging/Results/PaymentResultOutboxDispatcher.cs). Dispatchern hämtar väntande rader i omgångar om högst 25 och bygger antingen `PaymentSucceededV1` eller `PaymentFailedV1` från den sparade ögonblicksbilden.

[RabbitMqPaymentResultPublisher](../src/services/PaymentSimulator/messaging/Results/RabbitMqPaymentResultPublisher.cs) publicerar meddelandet. Först efter RabbitMQ:s bekräftelse sätts `DeliveredAtUtc`. Vid fel sparas `AttemptCount`, `LastError` och nästa försökstid, `NextAttemptAtUtc`, med exponentiellt ökande väntetid. Samma meddelande-ID och innehåll används vid nästa försök.

### 3. Ta emot och spara i Bookings inbox

[PaymentResultConsumer](../src/services/Booking/features/payments/Results/PaymentResultConsumer.cs) lyssnar på båda resultattyperna i samma kö. Routing key avgör vilket kontrakt som ska deserialiseras.

[PaymentResultInbox](../src/services/Booking/features/payments/Results/PaymentResultInbox.cs) validerar meddelandet och kontrollerar att bokningen finns, att betalning har begärts och att belopp och valuta stämmer. Booking förväntar sig för närvarande `SEK`. Ett framgångsmeddelande måste också ha en giltig betalningstid.

Resultatet sparas i [PaymentResultInboxMessage](../src/services/Booking/features/payments/Results/PaymentResultInboxMessage.cs) innan konsumenten kvitterar till RabbitMQ. Samma `MessageId` med identiskt innehåll accepteras som en dubblett. Samma ID med ändrat innehåll avvisas.

Kvittensen betyder här att Booking har tagit över ansvaret för fortsatt behandling. Bokningen behöver ännu inte vara bekräftad eller avbokad.

### 4. Tillämpa resultatet på bokningen

[PaymentResultInboxWorker](../src/services/Booking/features/payments/Results/PaymentResultInboxWorker.cs) hämtar väntande resultat genom [PaymentResultInboxDispatcher](../src/services/Booking/features/payments/Results/PaymentResultInboxDispatcher.cs). Själva tillämpningen finns i [ApplyPaymentResult.cs](../src/services/Booking/features/bookings/Payments/ApplyPaymentResult.cs).

Booking använder samma databaslås per bokning och samma utfallshantering som HTTP-flödet. Innan resultatet tillämpas hämtas betalningens aktuella status via HTTP. Då verifieras bland annat betalnings-ID, belopp, valuta och utfall, och en återbetalning som skett efter den ursprungliga händelsen upptäcks.

- **Lyckad betalning och giltig reservation:** bokningen bekräftas, lagret uppdateras och biljettmeddelanden sparas i samma transaktion.
- **Misslyckad eller tidsutgången betalning:** bokningen avbokas och reservationen frigörs.
- **Lyckad betalning som inte längre kan ge en bekräftad bokning:** betalningen återbetalas innan flödet markeras som avslutat.

Tillfälliga fel vid uppslag, återbetalning eller databasåtkomst lämnar inbox-raden väntande för nya försök. När arbetet är klart sätts `ProcessedAtUtc`. En osäker konflikt, exempelvis ett utfall som inte stämmer med den aktuella betalningen, sätter i stället `RejectedAtUtc` och `LastError` för utredning.

Affärsändringen och markeringen `ProcessedAtUtc` sker inte i en gemensam transaktion. Ett avbrott mellan dessa steg kan därför ge ombehandling; befintliga boknings- och biljettkontroller gör den säker. Den sparade inbox-raden gör att arbetet finns kvar även efter omstart.

[PaymentReconciliationService](../src/services/Booking/features/bookings/Payments/PaymentReconciliationService.cs) fortsätter också att kontrollera betalningar via HTTP. Resultatmeddelanden och avstämning använder samma bokningslås för att undvika samtidiga motstridiga ändringar.

## Biljettutfärdande: från Booking till TicketService

### 1. Bekräfta bokning och spara outbox

`ConfirmAsync` i [BookingService](../src/services/Booking/features/bookings/BookingService.cs) låser bokningen och kontrollerar betalning, reservation och lager. Bekräftelsen och raderna i [TicketIssueOutboxMessage](../src/services/Booking/features/tickets/Issuance/TicketIssueOutboxMessage.cs) sparas i samma transaktion.

Det skapas ett `BookingConfirmedV1` per bokningsrad, inte ett per hel bokning. Meddelandet innehåller den information som behövs för att utfärda biljetter, så TicketService behöver inte fråga Booking eller EventCatalog när meddelandet behandlas.

### 2. Publicera bekräftade bokningsrader

[TicketIssueOutboxBackgroundService](../src/services/Booking/features/tickets/Issuance/TicketIssueOutboxBackgroundService.cs) kör [TicketIssueOutboxDispatcher](../src/services/Booking/features/tickets/Issuance/TicketIssueOutboxDispatcher.cs) ungefär varannan sekund. Dispatchern hämtar högst 25 förfallna rader och skickar dem med [RabbitMqBookingConfirmedPublisher](../src/services/Booking/features/tickets/Issuance/RabbitMqBookingConfirmedPublisher.cs).

Outbox-radens ID blir `MessageId`, bokningens ID blir `CorrelationId` och radens skapandetid blir `OccurredAtUtc`. Värdena bevaras vid nya försök. `DeliveredAtUtc` sätts efter publisher confirm; annars sparas felet och ett nytt försök planeras.

Utvecklingsverktygets seedbokningar med ID-prefix `51000000-` är ett undantag. Deras biljetter utfärdas via HTTP medan dispatchern håller samma databaslås som seedraderingen. Dispatchern läser om raden under låset och inväntar TicketServices svar innan `DeliveredAtUtc` sätts. Därmed lämnas inga seedmeddelanden i RabbitMQ som kan återskapa biljetter efter radering. Vanliga kundbokningar använder fortsatt RabbitMQ. Seedbetalningar skapas direkt som historiska, slutreglerade betalningar via seed-API:t och går inte genom betalningsköerna. Försäljningsstatistiken läser betalningar via HTTP oavsett hur kundbetalningen startades.

### 3. Skapa och spara biljetter

[BookingConfirmedConsumer](../src/services/TicketService/Features/Tickets/IssueTickets/BookingConfirmedConsumer.cs) anropar [IssueTicketsHandler](../src/services/TicketService/Features/Tickets/IssueTickets/IssueTicketsHandler.cs), som validerar meddelandet och sparar biljetterna. Först när detta är klart kvitteras leveransen.

Biljettutfärdandet är idempotent per `BookingItemId`. En omleverans med samma bokningsinformation återanvänder befintliga biljetter. Ändrad information för samma bokningsrad behandlas som en konflikt. Ett unikt databasindex på bokningsrad och biljettnummer inom raden skyddar även mot samtidiga försök.

Evenemangets starttid jämförs med PostgreSQLs mikrosekundprecision, så att ett oförändrat meddelande med finare tidsprecision kan levereras igen utan en falsk konflikt.

## Exchange, köer och routning

Alla vanliga meddelanden publiceras till den beständiga topic-exchangen `halsingelive.events`. Felaktiga leveranser skickas till den beständiga direct-exchangen `halsingelive.dead`.

| Meddelande | Routing key | Konsumentkö | Dead-letter routing key | Dead-letter-kö |
| --- | --- | --- | --- | --- |
| `PaymentRequestedV1` | `payment.requested.v1` | `payments.payment-requested.v1` | `payment-request.dead` | `payments.payment-requested.v1.dead` |
| `PaymentSucceededV1` | `payment.succeeded.v1` | `booking.payment-results.v1` | `payment-result.dead` | `booking.payment-results.v1.dead` |
| `PaymentFailedV1` | `payment.failed.v1` | `booking.payment-results.v1` | `payment-result.dead` | `booking.payment-results.v1.dead` |
| `BookingConfirmedV1` | `booking.confirmed.v1` | `tickets.booking-confirmed.v1` | `ticket-issue.dead` | `tickets.booking-confirmed.v1.dead` |

Publicerare och konsumenter deklarerar samma topologi. Köerna är beständiga, inte exklusiva och tas inte bort automatiskt när en konsument kopplas ned. Därmed kan meddelanden köas även innan mottagartjänsten har startat.

Topologin finns bland annat i [PaymentRequestMessagingTopology](../src/services/PaymentSimulator/messaging/Requests/PaymentRequestMessagingTopology.cs), [PaymentResultMessagingTopology i PaymentSimulator](../src/services/PaymentSimulator/messaging/Results/PaymentResultMessagingTopology.cs), [motsvarande topologi i Booking](../src/services/Booking/features/payments/Results/PaymentResultMessagingTopology.cs) och biljettpublicerarens fil.

## Leveranssäkerhet och felhantering

Publicerarna skickar beständiga JSON-meddelanden med `ContentType=application/json`, `MessageId`, `CorrelationId` och meddelandetyp i AMQP-metadata. De använder publisher confirms och `mandatory`, så att ett meddelande som inte kan routas inte tyst betraktas som levererat.

Leveransen kan ske mer än en gång. Exempelvis kan RabbitMQ ha accepterat ett meddelande precis innan publiceraren kraschar, utan att outbox-raden hinner markeras som levererad. Vid nästa försök skickas samma meddelande igen. Idempotens i mottagarna är därför en del av lösningen.

### När kvitteras ett meddelande?

| Konsument | Villkor för `ack` |
| --- | --- |
| PaymentSimulator: betalningsbegäran | Betalningen är beständigt sparad eller ett befintligt försök har verifierats. Konsumenten väntar inte på slutförandet; återställningsarbetaren hanterar väntande betalningar. |
| Booking: betalningsresultat | Resultatet är validerat och beständigt sparat i inbox. Fortsatt arbete ägs därefter av inbox-arbetaren. |
| TicketService: bekräftad bokningsrad | Biljetterna har sparats. |

Konsumenterna använder manuell kvittens och hämtar ett meddelande åt gången (`prefetch=1`). Om tjänsten stängs av under arbetet lämnas ofärdiga leveranser okvitterade för omleverans.

### Vad händer vid fel?

- Ogiltig JSON, saknade obligatoriska fält och valideringsfel som identifieras som permanenta avvisas direkt till dead-letter-kön.
- Övriga behandlingsfel, exempelvis databasavbrott, lämnar leveransen okvitterad och försöks igen med väntetiderna 1, 2, 4, 8, 16 och därefter högst 30 sekunder. Varje försök använder ett nytt scope och en ny databaskontext. Meddelandet skickas inte till dead-letter-kön enbart för att flera försök misslyckas.
- Om kanalen bryts eller tjänsten stängs av avbryts väntande försök. RabbitMQ kan då leverera det okvitterade meddelandet igen. Idempotensen hanterar även ett avbrott efter databaslagring men före kvittens. Kända kontrakts- och biljettkonflikter går direkt till dead-letter-kön.
- Publiceringsfel i begärans-, resultat- och biljett-outbox ger nya bakgrundsförsök med samma sparade meddelande.
- Fel efter att Booking har sparat och kvitterat ett resultat hanteras i inbox, inte av RabbitMQ. Tillfälliga fel ger nya försök; konflikter kan sättas i karantän med `RejectedAtUtc`.

Det finns ingen automatisk återpublicering från dead-letter-köerna. Orsaken behöver utredas innan meddelandet skickas om. Vid ompublicering av samma händelse ska ursprungligt innehåll och `MessageId` bevaras och den ursprungliga routing key användas.

## Databaser och registrering

### Mappstruktur i Booking

Betalningskoden är grupperad efter sitt ansvar. Biljettutfärdandet har en egen mapp, och RabbitMQ-anslutningen delas från infrastrukturen:

```text
Booking/
├── features/
│   ├── payments/
│   │   ├── Requests/     # Betalningsbegäran: outbox, worker, dispatcher och publicerare
│   │   ├── Results/      # Betalningsresultat: konsument, inbox, worker och dispatcher
│   │   └── Gateway/      # HTTP-uppslag, kompensation och kontraktsfel
│   ├── tickets/
│   │   └── Issuance/     # Biljett-outbox, bakgrundstjänst, dispatcher och publicerare
│   └── bookings/         # Bokningens affärsregler, API, validering och reservationstid
│       ├── Payments/     # Betalningsstart, tillämpning av resultat och avstämning
│       ├── Occupancy/    # Beläggningsrapporter och tillhörande controllers
│       └── Models/       # Booking och BookingItem
└── Infrastructure/
    └── Messaging/        # Gemensam RabbitMqConnectionProvider
```

`StartPayment.cs`, `ApplyPaymentResult.cs` och `PaymentReconciliationService.cs` ligger under `features/bookings/Payments`. De två första är delar av den partiella klassen `BookingService` och behåller namespace `Booking.Features.Bookings`. Avstämningstjänsten använder `Booking.Features.Bookings.Payments`.

`VenueOccupancyController.cs`, `EventOccupancyController.cs` och `VenueOccupancyReport.cs` ligger under `features/bookings/Occupancy` med namespace `Booking.Features.Bookings.Occupancy`. `BookingExpirationService.cs` och `GuestAccessToken.cs` ligger fortsatt direkt under `features/bookings`. `ITicketServiceClient`, `HttpTicketServiceClient` och `AdminTicketsController` ligger direkt under `features/tickets`.

### Mappstruktur i PaymentSimulator

Inkommande betalningsbegäranden och utgående betalningsresultat ligger i var sin mapp. Anslutningen delas från infrastrukturen:

```text
PaymentSimulator/
├── messaging/
│   ├── Requests/        # Konsument, handler och topologi för betalningsbegäran
│   └── Results/         # Resultat-outbox, worker, dispatcher, publicerare och topologi
└── Infrastructure/
    └── Messaging/       # Gemensam RabbitMqConnectionProvider
```

`Requests` använder namespace `PaymentSimulator.Messaging.Requests` och `Results` använder `PaymentSimulator.Messaging.Results`. Den gemensamma anslutningen använder `PaymentSimulator.Infrastructure.Messaging` och delas av begäranskonsumenten och resultatpubliceraren. Simuleringslogik och återhämtningsarbetaren finns under `features/payments/ProcessPayment`.

### Lagring och tjänsteregistrering

Outbox och inbox ligger i respektive tjänsts PostgreSQL-databas, inte i RabbitMQ. Modeller och index konfigureras i [BookingDbContext](../src/services/Booking/data/BookingDbContext.cs) och [PaymentDbContext](../src/services/PaymentSimulator/data/PaymentDbContext.cs).

| Migration | Ansvar |
| --- | --- |
| [PaymentRequestOutbox](../src/services/Booking/data/Migrations/20261104064904_PaymentRequestOutbox.cs) | Sparar Bookings betalningsbegäranden och för över äldre oavslutade betalningsavsikter till request-outbox. |
| [TicketIssueOutbox](../src/services/Booking/data/Migrations/20261027133047_TicketIssueOutbox.cs) | Sparar Bookings väntande biljettmeddelanden. |
| [PaymentResultOutbox](../src/services/PaymentSimulator/Migrations/20261103125856_PaymentResultOutbox.cs) | Sparar PaymentSimulators väntande betalningsresultat. |
| [PaymentResultInbox](../src/services/Booking/data/Migrations/20261103125909_PaymentResultInbox.cs) | Sparar Bookings mottagna betalningsresultat och behandlingsstatus. |

Resultatmigrationerna lägger till tabeller utan att ändra befintliga bokningar eller betalningar. Historiska slutliga betalningar får inte automatiskt några outbox-meddelanden. Migrationerna behöver tillämpas genom tjänsternas vanliga migreringsflöde innan de nya arbetarna körs.

Request-outbox-migrationen skapar däremot begäransrader för äldre bokningar med `PaymentRequested = true` och `PaymentSettled = false`, utom redan bekräftade bokningar. Befintliga betalningsförsök återanvänds genom `BookingId`; ogiltiga reservationer undertrycks och kompenseras av avstämningen.

Registrering av anslutningar, publicerare och bakgrundstjänster finns i [Bookings program.cs](../src/services/Booking/program.cs), [PaymentSimulators Program.cs](../src/services/PaymentSimulator/Program.cs) och [TicketServices Program.cs](../src/services/TicketService/Program.cs). Compose aktiverar databasinitiering för Booking och TicketService; PaymentSimulator har sitt migreringsflöde vid uppstart.

## Lokal körning och konfiguration

[Compose.yaml](../src/Compose.yaml) startar `rabbitmq:4-management-alpine` med en beständig datavolym och hälsokontroll. De tre tjänsterna väntar på att RabbitMQ är redo innan de startas genom Compose.

| Anslutning | Lokal adress |
| --- | --- |
| RabbitMQ:s administrationsgränssnitt | <http://localhost:15672> |
| AMQP från värddatorn | `localhost:5672` |
| AMQP mellan Compose-tjänster | `rabbitmq:5672` |

Ange `RABBITMQ_USER` och ett genererat `RABBITMQ_PASSWORD` enligt [konfigurationsguiden](configuration.md). Compose använder samma värden för klienterna och för att skapa kontot i en tom broker. Virtual host är `/`. Befintliga brokerkonton uppdateras inte genom att ändra dessa bootstrap-värden.

Skapa rotens lokala `.env`, inklusive gemensam intern nyckel för Booking och TicketService, och kör från repots rot:

```powershell
docker compose up -d --build
```

När tjänsterna körs från IDE:n används normalt `localhost:5672`. Följande inställningar kan anges som miljövariabler:

| Miljövariabel | Betydelse |
| --- | --- |
| `RabbitMq__HostName` | Brokeradress, exempelvis `localhost` eller `rabbitmq`. |
| `RabbitMq__Port` | AMQP-port, normalt `5672`. |
| `RabbitMq__VirtualHost` | Virtual host, normalt `/`. |
| `RabbitMq__UserName` | Användarnamn för tjänstens anslutning. |
| `RabbitMq__Password` | Lösenord för tjänstens anslutning. |

Compose läser dessutom `RABBITMQ_USER` och `RABBITMQ_PASSWORD` för klienternas inloggning. Dessa variabler skapar inte ett motsvarande konto i brokern; kontot behöver konfigureras separat om standardkontot byts ut.

Anslutningshantering finns i [Bookings RabbitMqConnectionProvider](../src/services/Booking/Infrastructure/Messaging/RabbitMqConnectionProvider.cs), som delas av dess meddelandeflöden, och i motsvarande anslutningskod i de andra tjänsterna. Konsumenterna försöker återansluta efter avbrott. Brokerbekräftelser, outbox och inbox hanterar andra delar av återhämtningen.

## Tester

Enhetstester kan köras utan externa tjänster:

```powershell
dotnet test tests/Booking.UnitTests/Booking.UnitTests.csproj
dotnet test tests/PaymentSimulator.UnitTests/PaymentSimulator.UnitTests.csproj
dotnet test tests/TicketService.UnitTests/TicketService.UnitTests.csproj
```

Exempel på tester nära meddelandeflödena är [PaymentRequestOutboxDispatcherTests](../tests/Booking.UnitTests/PaymentRequestOutboxDispatcherTests.cs), [TicketIssueOutboxDispatcherTests](../tests/Booking.UnitTests/TicketIssueOutboxDispatcherTests.cs), [PaymentRequestedHandlerTests](../tests/PaymentSimulator.UnitTests/PaymentRequestedHandlerTests.cs), [BookingConfirmedContractTests](../tests/TicketService.UnitTests/BookingConfirmedContractTests.cs) och [IssueTicketsHandlerTests](../tests/TicketService.UnitTests/IssueTicketsHandlerTests.cs).

### Integrationstester med RabbitMQ

Meddelandetesterna använder fasta könamn och ska därför köras mot en separat testbroker eller en isolerad virtual host. Använd inte samma köer som utvecklingstjänster som redan körs.

Kör testprojekten i följd när de delar testbroker. Booking- och PaymentSimulator-testerna har egna databaser men konkurrerar om samma betalningsköer vid parallell körning. För hela lösningen används `dotnet test HalsingeLiveAB.sln --maxcpucount:1`; CI använder också detta alternativ.

En separat lokal testbroker kan startas så här:

```powershell
docker run --detach --rm --name halsingelive-rabbitmq-test -p 127.0.0.1:5673:5672 -e RABBITMQ_DEFAULT_USER=payment-tests -e RABBITMQ_DEFAULT_PASS=payment-tests rabbitmq:4-management-alpine
docker exec halsingelive-rabbitmq-test rabbitmq-diagnostics -q ping
```

Vänta tills hälsokontrollen lyckas. Med PostgreSQL och Redis igång på Compose-portarna anges testanslutningarna så här:

```powershell
$env:PAYMENT_TEST_DB = 'Host=localhost;Port=5434;Database=postgres;Username=postgres;Password=postgres'
$env:PAYMENT_TEST_RABBITMQ = 'amqp://payment-tests:payment-tests@localhost:5673/'
$env:BOOKING_TEST_POSTGRES = 'Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres'
$env:BOOKING_TEST_REDIS = 'localhost:6379'
$env:TICKET_TEST_POSTGRES = 'Host=localhost;Port=5435;Database=postgres;Username=postgres;Password=postgres'
$env:TICKET_TEST_RABBITMQ = $env:PAYMENT_TEST_RABBITMQ
dotnet test tests/PaymentSimulator.IntegrationTests/PaymentSimulator.IntegrationTests.csproj
dotnet test tests/Booking.IntegrationTests/Booking.IntegrationTests.csproj
dotnet test tests/TicketService.IntegrationTests/TicketService.IntegrationTests.csproj
```

Testerna skapar och tar bort egna slumpmässigt namngivna PostgreSQL-databaser. Betalningsflödets RabbitMQ-tester hoppas över om `PAYMENT_TEST_RABBITMQ` saknas. Biljettflödets meddelandetester kräver både `TICKET_TEST_POSTGRES` och `TICKET_TEST_RABBITMQ`. Övriga integrationstester kan ha andra anslutningskrav. [CI-konfigurationen](../.github/workflows/ci.yml) tillhandahåller PostgreSQL, Redis och en separat RabbitMQ-tjänst för testkörningen.

De tre konsumenternas meddelandetester simulerar databasavbrott genom att neka nya konsumentanslutningar. De verifierar återhämtning efter fler än tre misslyckade försök, både med och utan omstart under väntan, samt att dubbletter inte skapar extra betalningar, inboxrader eller biljetter. Permanenta konflikter ska fortfarande nå dead-letter-kön.

Viktiga testfall finns i:

- [PaymentRequestedConsumerTests](../tests/PaymentSimulator.IntegrationTests/PaymentRequestedConsumerTests.cs): snabb kvittens av flera fördröjda betalningar, dubbletter, ogiltiga meddelanden och återhämtning av kvitterade `Pending`-betalningar efter omstart.
- [PaymentProcessingTests](../tests/PaymentSimulator.IntegrationTests/PaymentProcessingTests.cs): atomär lagring av resultat och outbox, återställning, publiceringsfel och återbetalningar.
- [PaymentRequestTests](../tests/Booking.IntegrationTests/PaymentRequestTests.cs): start utan kontakt med betaltjänsten, atomär lagring av avsikt och begäran samt avstämning utan HTTP-start.
- [PaymentResultMessagingTests](../tests/Booking.IntegrationTests/PaymentResultMessagingTests.cs): hela vägen från Bookings request-outbox genom betalningskonsumenten och resultat-outbox till Bookings inbox och bokningsändring, inklusive dubbletter.
- [PaymentResultTests](../tests/Booking.IntegrationTests/PaymentResultTests.cs): samtidighet med HTTP-avstämning, misslyckade återbetalningar, konflikter och ombehandling efter redan genomförd bokningsbekräftelse.
- [BookingConfirmedConsumerTests](../tests/TicketService.IntegrationTests/BookingConfirmedConsumerTests.cs): återanslutning och biljettutfärdande efter att kön återskapats eller konsumentens anslutning stängts.

Stoppa testbrokern efter körningen med `docker stop halsingelive-rabbitmq-test`.

## Felsökning

Börja med att följa samma `BookingId` genom tjänsterna. `CorrelationId` har normalt detta värde. Använd sedan `MessageId` för en viss händelse och `PaymentId` för betalningsförsöket.

| Symtom eller status | Kontrollera |
| --- | --- |
| En request-outbox-rad har `SuppressedAtUtc` | Bokningen kan inte längre betalas och begäran skickas inte. Kontrollera även `PaymentSettled` och avstämningen; en tidigare osäker leverans kan fortfarande behöva spärras eller återbetalas. |
| En outbox-rad saknar `DeliveredAtUtc` | `LastError`, `AttemptCount`, `NextAttemptAtUtc`, brokeranslutning och routning. Meddelandet är ännu inte markerat som bekräftat av RabbitMQ. |
| Outbox är levererad men inget har hänt hos mottagaren | Köns väntande och okvitterade meddelanden, konsumentens loggar och eventuell dead-letter-kö. Publisher confirm betyder inte avslutad affärsbehandling. |
| Inbox saknar både `ProcessedAtUtc` och `RejectedAtUtc` | Arbetet väntar eller försöks igen. Kontrollera `LastError`, nästa försökstid och tillgängligheten för betalningsuppslag och återbetalning via HTTP. |
| Inbox har `RejectedAtUtc` | En konflikt behöver utredas utifrån `LastError` och den aktuella betalningen. Resultatet är redan kvitterat och ligger därför inte i RabbitMQ:s dead-letter-kö. |
| Meddelanden ligger i en dead-letter-kö | Kontrollera kontrakt, metadata, belopp, valuta och konsumentens fel innan ompublicering. |
| Bekräftad bokning saknar biljetter | Följ bokningsraden från biljett-outbox till `tickets.booking-confirmed.v1`, dess dead-letter-kö och TicketService. Sök på `BookingItemId`. |

Tjänsternas loggar kan följas med:

```powershell
docker compose logs -f booking payment-simulator ticketservice rabbitmq
```
