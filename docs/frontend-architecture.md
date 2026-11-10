# Frontendarkitektur och regler för API-kontrakt

Repot innehåller två fristående frontendappar: `src/frontend/customer-web` och
`src/frontend/admin-web`. De har separata API-hjälpare eftersom de har olika
åtkomstkrav. Kundappen gör offentliga anrop och använder en gästtoken för åtgärder
som gäller en viss bokning. Adminappen autentiserar med Keycloak och skickar en
Bearer-token. Håll transport- och autentiseringslogiken lokal för respektive app.

## Ansvar för API-kontrakt

Backendtjänsterna äger de kontrakt för förfrågningar och svar som de exponerar.
Frontendens API-moduler ska ligga tillsammans med funktionen som använder dem.
När appen behöver en annan struktur omvandlar modulerna backendens DTO:er till
modeller som passar gränssnittet.

| Tjänst | Kontraktsområde | Användning i frontend |
| --- | --- | --- |
| EventCatalog | Evenemang, scener, platskartor och evenemangsbilder | Båda apparna läser katalogdata. Adminwebben skapar, redigerar, laddar upp och raderar också katalogdata. |
| Booking | Bokningar, gäståtkomst, biljettlager, bokningsdetaljer för admin och autentiserad proxy för administrativ incheckning och makulering av biljetter | Kundwebben skapar och visar gästbokningar. Adminwebben hanterar bokningar och anropar de administrativa biljettåtgärderna. |
| TicketService | Biljettposter och endpoints för biljett-PDF | Adminwebben läser biljetter och PDF:er direkt. Booking anropar TicketService för biljettläsning och PDF med gäståtkomst. |
| PaymentSimulator | Betalningar och administrativa inställningar för betalningssimulering | Adminwebben hanterar simulatorns inställningar. |

URL-prefixen förenklar routningen och definierar inte vem som äger kontrakten.
Vite och frontendcontainrarnas Nginx skickar API-anrop vidare till API Gateway.
Gatewayen routar `/api` till EventCatalog, `/booking-api` till Booking,
`/ticket-api` till TicketService och `/payment-api` till PaymentSimulator.
Adminwebben använder det sistnämnda prefixet. Gatewayen tar bort respektive
frontendprefix innan anropet skickas till backendtjänsten.

## Delning och generering av typer

Behåll typer för förfrågningar och svar nära den API-modul som använder dem så
länge de är appspecifika, betjänar olika vyer eller omvandlas från backendens
DTO:er. Samma namn innebär inte att två typer beskriver samma kontrakt. Kundens
och administratörens `BookingDetails` har exempelvis olika struktur och
åtkomstregler och hålls därför åtskilda.

Om samma backendkontrakt deklareras separat i båda apparna och återkommande
ändringar gör att typerna glider isär, bekräfta först att backend fortfarande är
källan till kontraktet. Överväg sedan att generera klienttyper från ett API-schema
eller införa ett litet versionshanterat kontraktspaket. Dela bara kontraktstyper
som faktiskt är identiska. Behåll varje apps transport, autentisering och
modeller för användargränssnittet separat.

Vid dokumentationstillfället deklarerar båda apparna samma `VenueSeatingMap`-fält.
Denna kända dubblering kan samlas om typerna börjar ändras oberoende av varandra
eller fler liknande fall uppstår. Den motiverar inte i sig att API-hjälpare eller
applikationskod delas. När ett backendkontrakt ändras ska den ansvariga tjänsten
och alla API-moduler som använder kontraktet uppdateras i samma ändring.
Kontrollera även omvandlingar från DTO:er till modeller för gränssnittet.

## Kodreferenser

- [Adminappens transport och Bearer-autentisering](../src/frontend/admin-web/src/shared/api/http.ts)
- [Kundappens transport](../src/frontend/customer-web/src/shared/api/http.ts)
- [Adminappens boknings-API och använda typer](../src/frontend/admin-web/src/features/bookings/api/bookings.ts)
- [Kundappens boknings-API och använda typer](../src/frontend/customer-web/src/features/bookings/api/bookings.ts)
- [Adminappens kontrakt för platskartor](../src/frontend/admin-web/src/features/seating-maps/types.ts)
- [Kundappens kontrakt för platskartor](../src/frontend/customer-web/src/features/venues/api/seatingMaps.ts)
- [Gatewayens rutter och borttagning av prefix](../src/ApiGateway/appsettings.json)
