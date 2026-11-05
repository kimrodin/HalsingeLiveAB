# Entréskannern – guide till incheckning av biljetter

Den här guiden förklarar entréskannern i adminwebben: vad den gör, hur kameraskanningen och manuell inmatning fungerar, vilka bibliotek och API:er som används och varför implementationen har byggts på det sättet.

## Vad gör entréskannern?

Entréskannern hjälper personal att kontrollera en biljett vid dörren. Personalen väljer först ett evenemang och läser sedan biljettens QR-kod med kamera, eller skriver in biljett-ID:t manuellt. Skannern skickar ID:t och valt event till servern. Servern avgör om biljetten finns, gäller rätt event och fortfarande är giltig.

Skannern litar alltså inte enbart på innehållet i QR-koden. QR-koden ger ett ID; TicketService är auktoriteten för biljettens status.

## Tekniker

| Teknik | Användning |
| --- | --- |
| **React + TypeScript** | Hanterar sidans tillstånd, kameraelementet, validering och visning av resultat. |
| **Material UI** | Formulär, eventväljare, knappar, laddningsindikatorer och resultatmeddelanden. |
| **TanStack React Query** | Hämtar eventlistan och skickar check-in som en mutation med laddnings- och fellägen. |
| **`@zxing/browser`** | Läser QR-koder från videoströmmen i webbläsaren. |
| **MediaDevices / `getUserMedia`** | Webbläsarens kamera-API, som ZXing använder för att få bildrutor. |
| **Keycloak + Bearer-token** | Adminwebbens API-hjälpare skickar inloggad admins token till BookingService. |

## Var finns koden?

```text
src/frontend/admin-web/src/features/check-in/pages/
  TicketScannerRoute.tsx     Laddar skannersidan vid behov
  TicketScannerPage.tsx      Eventval, kamera, manuell inmatning och resultat

src/frontend/admin-web/src/features/bookings/
  api/bookings.ts            API-funktion för check-in

src/services/Booking/features/bookings/
                              Adminendpoint som kontrollerar rättigheter och anropar TicketService

src/services/TicketService/Api/TicketsController.cs
                              Slutlig validering och ändring av biljettstatus
```

Skannersidan laddas via `React.lazy` och `Suspense` i `TicketScannerRoute`. Det skjuter upp nedladdningen av kamerabiblioteket tills skannerfunktionen behövs och visar en laddningsindikator under tiden.

## Flödet från skärm till biljettstatus

```text
Admin öppnar Entréskanner
  → skannersidan hämtar aktiva/kommande event
  → admin väljer event
  → kamera läser QR-koden, eller admin skriver ID manuellt
  → sidan kontrollerar att texten har GUID-format
  → checkInTicket() skickar POST till BookingService
  → BookingService kontrollerar adminbehörighet och anropar TicketService
  → TicketService kontrollerar event och biljettstatus
  → resultatet visas på sidan
```

I koden anropar `TicketScannerPage` `getEvents()` för eventväljaren. Vid lyckad skanning eller manuell inmatning anropas `checkInTicket(ticketId, eventId)`, som gör ett POST-anrop till BookingService. Adminwebbens gemensamma HTTP-hjälpare förnyar vid behov Keycloak-tokenen och skickar den i `Authorization: Bearer ...`.

BookingService är mellanledet med avsikt: webbläsaren använder sin vanliga adminidentitet, och backend hanterar anropet vidare till TicketService med intern tjänsteautentisering. Den interna API-nyckeln lämnas aldrig till webbläsaren.

## Kameraskanning

När användaren startar kameran kontrollerar sidan först att event är valt, att webbläsaren stöder `getUserMedia` och att videoelementet finns. Därefter skapas en `BrowserQRCodeReader` från ZXing och den kopplas till sidans `<video>`-element.

När ZXing hittar en kod skickas texten till samma funktion som används för manuell inmatning. Efter ett resultat stoppas kameran tills användaren väljer att skanna nästa biljett. Knappen **Stäng kamera** stoppar också kameran.

### Varför stoppas kameran efter en läsning?

En videoström kan visa samma QR-kod i många bildrutor. Om varje bildruta startade ett API-anrop skulle samma biljett kunna skickas flera gånger på kort tid. Skannern använder därför ett lås (`scanLockedRef`) och stoppar strömmen när en kod behandlas. Operatören startar nästa skanning med en tydlig knapp efter att resultatet har visats.

Kamerans livscykel är dessutom kopplad till Reacts cleanup: när sidan stängs stoppas ZXing-kontrollerna. Ett sessionsnummer (`cameraSessionRef`) gör att ett sent svar från en kamera som redan stängts inte återaktiverar den. Detta minskar risken för läckande kameraströmmar och uppdateringar från en gammal session.

Kameraåtkomst kan kräva användarens tillåtelse och en säker kontext, till exempel HTTPS eller localhost. Därför finns manuell inmatning som reservväg.

## Manuell inmatning och validering

QR-koden innehåller ett GUID som identifierar biljetten. Skannern kontrollerar formatet med ett reguljärt uttryck innan den skickar anropet. Det fångar upp tomma eller uppenbart felaktiga värden tidigt, och ger personalen ett begripligt felmeddelande.

Formatkontrollen är bara en första kontroll. Den visar inte om biljetten finns eller om den är giltig. Det kan bara backend avgöra utifrån den sparade biljettposten.

## Vad servern kontrollerar

TicketService kontrollerar bland annat att:

1. En biljett med ID:t finns.
2. Ett event-ID har skickats med.
3. Biljettens event-ID är det event som personalen valt.
4. Biljettstatusen är `Valid`.

Om allt stämmer ändras status till `CheckedIn` och en tidsstämpel sparas. Om biljetten redan är incheckad, avbokad eller gäller ett annat event returneras ett konfliktresultat. Om två incheckningar sker samtidigt skyddar databasens concurrency token mot att båda behandlas som en ny giltig incheckning.

## Resultat och felmeddelanden

Skannersidan visar resultat med MUI `Alert`. Under nätverksanrop visas en laddningsindikator och en text om att biljetten kontrolleras. Kända serverfel översätts till meddelanden som passar personalens situation, till exempel:

| Svar | Meddelandeidé |
| --- | --- |
| `404` | Biljetten hittades inte. |
| `409` | Biljetten gäller ett annat event, är redan incheckad eller är avbokad. |
| `400` | Eventval eller biljett-ID behöver kontrolleras. |
| `5xx` / nätverksfel | Tekniskt fel; kontrollera anslutningen och försök igen. |

Detaljerna för konflikter kan variera; serverns status och validering är det som avgör om insläpp godkänns.

## Varför denna design?

- **Servern fattar beslutet.** En bild av en QR-kod kan kopieras; bara backend kan avgöra om biljetten fortfarande är giltig.
- **Event måste väljas och skickas med.** Det förhindrar att en biljett av misstag checkas in vid fel evenemang.
- **Både kamera och manuell inmatning stöds.** Det hjälper när kamera saknas, är blockerad eller inte kan läsa koden.
- **Kameran stoppas efter en träff.** Det förhindrar upprepade anrop från samma kod och låter personalen se resultatet innan nästa biljett.
- **BookingService är proxy till TicketService.** Adminwebben använder sin Keycloak-identitet; intern tjänsteautentisering stannar på serversidan.
- **Koden delar check-in-API med bokningsdetaljerna.** `checkInTicket()` finns i bookings API-modul eftersom samma check-in-åtgärd även kan göras från adminens bokningsvy. Affärslogiken dupliceras inte i två skärmar.
- **Skannersidan laddas vid behov.** ZXing är främst användbart på denna sida, så lazy loading undviker att lägga dess kod i alla adminvyer från första sidladdningen.

## Bra ställen att börja läsa

1. Läs `TicketScannerPage.tsx` för det fullständiga UI- och kameraflödet.
2. Läs `TicketScannerRoute.tsx` för lazy loading.
3. Läs `features/bookings/api/bookings.ts` och sök efter `checkInTicket` för API-anropet.
4. Följ BookingService-endpointen för admin check-in för att se behörighetskontrollen och proxyn.
5. Läs `TicketService/Api/TicketsController.cs` för den slutliga statuskontrollen.

För hur biljetter skapas, lagras och får sin QR-kod, se [TicketService-guiden](ticketservice-guide.md).
