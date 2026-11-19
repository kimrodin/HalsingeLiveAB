# Personuppgifter: kartläggning och rutiner

## Beslutad teknisk hantering

Detta är ett testprojekt; använd inte riktiga kunduppgifter. Simuleringen använder
samma tvåårsgräns för alla kategorier och behåller bokningar och relaterade poster.
Den tekniska gallringen räknar därför två kalenderår från
`Bookings.CreatedAt` (UTC) och pseudonymiserar alla bokningar med samma
normaliserade e-postadress när en bokning i gruppen når gränsen. Kundnamn,
kund-e-post och eventuell `CustomerId` tas bort. Ett gemensamt
`PseudonymizedCustomerId` bevarar gruppkopplingen och den hashade gästtokenen
bevaras så att befintlig gäståtkomst fortsätter att fungera. Boknings-ID,
betalningar, bokningsrader, biljetter och meddelandekopplingar bevaras.

Eftersom dessa poster fortsätter att vara länkade via boknings-ID benämns
åtgärden **pseudonymisering**, inte anonymisering. Ingen rättslig grund eller
bevaranderegel fastställs här eftersom projektet är en simulering och inte ska
driftsättas. Den gemensamma tvåårsgränsen finns i Booking; automatisk gallring
är inte implementerad i alla källor.

## Datakartläggning

| Källa | Uppgifter och koppling | Teknisk hantering |
| --- | --- | --- |
| Booking PostgreSQL | Namn, e-post, valfri kundlänk, boknings-ID, status, belopp, datum och bokningsrader. Gäståtkomst lagras som tokenhash. | Tvåårig gallring rensar direktidentifierare för hela e-postgruppen. Tokenhash och en gemensam pseudonymiserad kundkoppling bevaras. |
| PaymentSimulator PostgreSQL | Betalnings-ID, boknings-ID, belopp, valuta, status och betalningstider. | Betalningarna innehåller inte namn/e-post i den aktuella modellen men är kopplingsbara via boknings-ID. |
| TicketService PostgreSQL | Biljett-ID, boknings-/bokningsrads-ID, evenemangs- och platsuppgifter, pris och status. | Biljetter bevaras och är kopplingsbara via boknings-ID. |
| Booking inbox/outbox | Betalningsresultat i inbox samt betalnings- och biljettbegäranden i outbox. Innehåller boknings-/meddelande-ID, status, försök och ibland feltext. | Registerutdraget tar med lokala inbox-/outboxposter. Feltext ska granskas innan den delas. Tvåårig gallring av dessa poster är ännu inte implementerad. |
| PaymentSimulator outbox | Beständiga betalningsresultat med betalnings-/boknings-ID, belopp, utfall och leveransstatus. | Ingår i exportens sammanställning via PaymentSimulator. Feltext lämnas utanför och behöver granskas separat. Tvåårig gallring är ännu inte implementerad. |
| RabbitMQ och felköer | Aktuella meddelandekontrakt innehåller boknings-ID eller korrelations-ID och köp-/biljettuppgifter. | Kvitterade meddelanden tas normalt bort ur arbetskön. Köer och dead-letter-köer är beständiga; ingen TTL är konfigurerad, så felköer rensas inte automatiskt efter två år. Kontrollera dem via godkänd driftåtkomst och återpublicera inte under en begäran. |
| Redis | Tillfälliga reservationsnycklar och markörer med reservations-/boknings-ID; namn, e-post och åtkomsttoken lagras inte där. Terminala markörer har 24 timmars TTL; lager- och reservationsnycklar har ingen generell TTL. | Reservationsflödet tar bort aktiva nycklar när de avslutas. Kontrollera utgångna/orphan-nycklar; radera inte lager-/reservationsnycklar manuellt utan att återställa lagersaldot. Ingen separat tvåårig gallring behövs för namn/e-post eftersom de inte lagras där. |
| Keycloak | Nuvarande dokumentation beskriver administratörskonton; kundköp använder gästtoken. | Kontrollera realm-användare, attribut och händelseloggar för en eventuell faktisk koppling innan utlämnande eller radering. |
| Seq och containerloggar | Logghändelser kan innehålla boknings-/korrelations-ID och tekniska fel. | Sök på boknings-ID och tillämpa separat beslutad gallring och åtkomstpolicy. Undvik att lägga namn, e-post eller autentiseringshemligheter i loggar. |
| Kundwebbläsare | `sessionStorage` innehåller gästtoken under nyckeln `booking-access:{bookingId}`. | Pseudonymisering återkallar inte tokenhashen på servern; befintlig gäståtkomst fortsätter. Webbläsartoken är inte tillgänglig för serverexport och ska tas bort av kunden/operatören från sessionslagringen. |
| Säkerhetskopior | Kan innehålla äldre kopior av databas-, kö- och loggdata. | Backup- och återställningspolicy hanteras utanför repots Compose-konfiguration. Efter återställning ska gallringsjobbet köras innan systemet öppnas för användning. |

Meddelandeflöden och loggkorrelation beskrivs i [messaging](messaging.md) och
[Seq](seq.md); Redis-nycklarna beskrivs i [redis](redis.md).

## Utlämnande och radering på begäran

1. Registrera begäran i verksamhetens godkända ärendehantering och verifiera
   identiteten mot lämpliga uppgifter. Kännedom om e-postadress eller
   boknings-ID ensamt är inte identitetskontroll.
2. En Keycloak-administratör med `AdminOnly` kan söka bokningar med namn,
   e-post eller boknings-ID. Bokningsdetaljer visar tidigare bokningar för
   samma e-post och fortsätter att visa grupphistorik efter pseudonymisering.
   Använd **Hämta uppgiftssammanställning** eller **Radera
   direktidentifierare** på en bokning som omfattas av den verifierade begäran.
3. Exporten omfattar Booking, betalning, biljetter, PaymentSimulator-resultat-
   outbox samt lokala inbox-/outbox-poster. Den listar dessutom källor som
   kräver manuell kontroll. Granska fritext i felmeddelanden innan utlämnande.
   Genomför
   kontrollen i RabbitMQ/felköer, Redis, Seq, Keycloak och backup-/restoreflödet
   innan ärendet avslutas.
4. Exportfilen innehåller personuppgifter. Förvara den endast i godkänd plats,
   begränsa åtkomst och radera arbetskopior enligt verksamhetens rutin.
5. Begärd pseudonymisering tar bort direkta identifierare från alla bokningar
   med samma normaliserade e-post och bevarar deras gemensamma koppling samt
   gästtokenhash. Den tar **inte** bort länkade boknings-, betalnings-, biljett-,
   meddelande- eller loggposter. Hantera eventuella bevarandekrav och undantag
   enligt beslut från verksamheten.
6. Dokumentera vem som hanterade ärendet, identitetskontrollen, vilka källor
   som kontrollerades, resultatet och eventuella kvarvarande uppgifter i
   ärendehanteringen. API-loggarna innehåller boknings-ID och Keycloak-subjekt
   för åtkomstspårning.

## Tvåårig gallring

Booking kör ett bakgrundsjobb vid start och därefter var 24:e timme. Det väljer
poster vars `CreatedAt` är äldre än eller lika med aktuell UTC-tid minus två
kalenderår. Jobbet behandlar högst 250 poster per databasbatch, kan köras om och
ändrar inte bokningsstatus eller betalnings-/biljettposter.

Jobbet omfattar för närvarande alla bokningsstatusar och saknar statusbaserade
undantag. Verksamheten behöver godkänna detta eller besluta om undantag innan
driftsättning.

Administratörer kan granska antal och upp till 100 boknings-ID:n via
`GET /privacy/bookings/retention-preview`. Verifiera syntetiska poster före och
efter gränsen innan driftsättning. En återställning från säkerhetskopia kan
återinföra äldre direktidentifierare; kör gallringsjobbet och kontrollera
resultatet efter varje återställning.

Tvåårsperioden och kvarhållande av kopplade poster gäller för simuleringen.
Detta repo är inte en produktionsklar integritetslösning; särskilt gallring av
inbox/outbox, felköer, loggar och säkerhetskopior är inte automatiserad.
