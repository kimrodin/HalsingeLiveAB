# Frontendguide för nybörjare

Den här guiden hjälper dig att förstå frontendkoden i Hälsinge Live. Den går igenom vilka appar som finns, hur de är byggda, hur en sida hämtar data och var du börjar när du ska ändra något.

För att starta hela systemet, följ [snabbstarten i projektroten](../README.md).
Efter lokal `.env`-konfiguration räcker `docker compose up -d --build` i
projektroten på Windows, macOS och Linux. Lokal Node-installation behövs endast
om du väljer att köra Vite direkt på värddatorn.

## Vad menas med frontend?

Frontend är den del av systemet som körs i webbläsaren och som användaren ser och använder: sidor, knappar, formulär, listor och meddelanden. Frontend pratar med backend via HTTP-anrop. Backend ansvarar för data och regler; frontend visar resultatet och skickar användarens åtgärder vidare.

I projektet finns två separata frontendappar:

| App | Mapp | Vem använder den? | Inloggning |
| --- | --- | --- | --- |
| Kundwebb | `src/frontend/customer-web` | Besökare som letar evenemang och bokar biljetter | Ingen vanlig användarinloggning. En bokning skyddas med en gästtoken. |
| Adminwebb | `src/frontend/admin-web` | Personal som hanterar evenemang, scener, bokningar och biljetter | Keycloak-inloggning och adminroll krävs. |

Apparna delar grundläggande teknik, men har egna API-hjälpare. Det är avsiktligt: kundwebben använder offentliga API-anrop och gästtoken, medan adminwebben använder administratörens Bearer-token.

## Teknikerna i korthet

| Teknik | Vad den gör här |
| --- | --- |
| **React** | Bygger gränssnittet av återanvändbara komponenter. En komponent kan vara en hel sida, en lista eller exempelvis ett evenemangskort. |
| **TypeScript** | Lägger till typer ovanpå JavaScript. Typer beskriver till exempel vilka fält ett evenemang har och hjälper till att hitta fel medan koden skrivs. |
| **Vite** | Startar utvecklingsservern lokalt och bygger frontendfilerna för publicering. |
| **React Router** | Kopplar webbadresser till sidor, till exempel `/events/:eventId` till evenemangets detaljsida. |
| **TanStack React Query** | Hämtar och cachar data från backend och håller reda på laddning, fel, uppdatering och mutationer. |
| **Material UI (MUI)** | Ger färdiga gränssnittskomponenter, till exempel knappar, formulärfält, tabeller, dialoger och meddelanden. |
| **Emotion** | CSS-in-JS-bibliotek som MUI använder för styling. Stilar kan också skrivas direkt i en komponents `sx`-prop. |
| **Fetch API** | Webbläsarens inbyggda sätt att skicka HTTP-anrop. Projektets hjälpfunktioner bygger på `fetch`. |
| **Keycloak** | Hanterar inloggning och identitet i adminwebben. |
| **ZXing Browser** | Läser QR- och streckkoder med webbläsarens kamera i biljettsskannern. |

React Query och Vite är bygg- och datahanteringsverktyg; de är inte backendtjänster. Backend äger API-kontrakten och lagrar den beständiga datan.

## Mappstrukturen

Båda apparna följer ungefär samma struktur:

```text
src/
  main.tsx                 Startar React och kopplar in grundläggande providers
  app/
    router.tsx             Kopplar webbadresser till sidor
    layouts/               Gemensam ram runt flera sidor, till exempel meny och sidhuvud
    theme.ts               Appens färger och MUI-tema
    auth/                  Adminwebbens Keycloak-inställning
  features/
    events/                Evenemangssidor, komponenter och API-funktioner
    bookings/              Bokningsflöde och boknings-API
    venues/                Scener och platskartor
    ...                    Fler områden, främst i adminwebben
  shared/
    api/                   Gemensam HTTP-hjälpare för just den appen
```

En **feature** är ett område i produkten, exempelvis evenemang eller bokningar. Att samla sidkomponenter, mindre komponenter och API-funktioner per feature gör det lättare att hitta kod som hör ihop.

## Så startar en frontendapp

I `main.tsx` skapas React-applikationen. Där kopplas vanligtvis följande in:

1. Reacts rot kopplas till HTML-elementet `root`.
2. MUI-temat och `CssBaseline` aktiveras för grundläggande utseende.
3. `QueryClientProvider` gör React Query tillgängligt för sidorna.
4. `RouterProvider` visar den sida som matchar webbadressen.

I adminwebben sker Keycloak-inloggningen först. Appen visar den vanliga routade delen först när användaren är inloggad och har adminrollen.

### Vad providers gör

En provider gör en funktion tillgänglig för alla komponenter under den i komponentträdet. I båda apparna ligger `ThemeProvider` och `QueryClientProvider` ovanför routern. Det gör att sidor kan använda samma MUI-tema respektive samma React Query-cache. `StrictMode` hjälper till att upptäcka vissa problem under utveckling; det är inte en extra produktionsfunktion.

`QueryClient` skapas en gång vid appstart. Om det skapas inne i en sidkomponent riskerar cachen att återställas när komponenten renderas om. Routern använder `createBrowserRouter`, vilket kopplar en URL till en sidkomponent och låter layouten innehålla gemensam navigering runt undersidorna.

## Hur en sida blir till

En vanlig väg genom koden är:

```text
URL → router → layout → sida → feature-komponenter
                         ↘ React Query → feature-API → HTTP-hjälpare
```

Routern hanterar vilken sida som visas. Layouten innehåller sådant som delas mellan sidor, exempelvis navigering. Sidan samordnar data och komponenter för en vy. Mindre komponenter bör få data och händelsehanterare via props när de inte behöver känna till hela appen. Håll formulärens tillfälliga UI-state, som en öppen dialog eller ett valt fält, nära komponenten som använder den. Data från backend hör i stället hemma i React Query.

Exempel på rutter finns i `customer-web/src/app/router.tsx` och `admin-web/src/app/router.tsx`. Parametrar som `:eventId` eller `:id` läses av sidan från aktuell route och används för att hämta rätt resurs. När en ny route läggs till ska man även fundera på navigering dit och vad användaren ser om resursen saknas eller API-anropet misslyckas.

## Kundwebbens viktigaste flöden

### Hitta evenemang

Startsidan `features/events/pages/HomePage.tsx` visar evenemang och en sökruta. Söktexten väntar 300 millisekunder efter senaste tangenttryckningen innan den används i en fråga. Det minskar antalet onödiga API-anrop när användaren skriver.

Evenemangen hämtas med `useInfiniteQuery`. Det betyder att React Query behandlar sidindelade svar som en följd av sidor. `IntersectionObserver` bevakar en markör nära listans slut och hämtar nästa sida när markören kommer in i vyområdet. Knappen **Visa fler** är också en manuell väg att hämta nästa sida. Om just nästa sida misslyckas behålls de redan visade resultaten och användaren kan försöka igen.

Flödet ser förenklat ut så här:

```text
Besökaren skriver i sökrutan
  → söktexten väntar 300 ms
  → React Query startar/återanvänder frågan för sökningen
  → getEventPage() bygger URL och query-parametrar
  → getJson() anropar EventCatalog
  → evenemangskorten renderas
  → scrollmarkören eller knappen hämtar nästa sida
```

### Se ett evenemang

`EventDetailPage.tsx` läser evenemangs-ID:t från webbadressen. Först hämtas evenemanget. När svaret innehåller ett scen-ID aktiveras separata frågor för sceninformation och platskartor. Biljettlager och bokningsalternativ visas när relevanta data har laddats.

Att göra anropen beroende av ID:t innebär att scen- och kartfrågor inte skickas innan evenemanget har berättat vilken scen de gäller.

### Boka och få biljett

Förenklat består bokningen av följande steg:

1. Kunden väljer evenemang, sektion och antal biljetter.
2. Frontend skickar en bokningsbegäran till Booking.
3. Backend svarar med ett boknings-ID och en gästtoken. Tokenen sparas i `sessionStorage` på enheten.
4. Bokningssidan använder boknings-ID och gästtoken för att läsa bokningen och utföra tillåtna åtgärder, som betalning eller avbokning.
5. React Query hämtar status igen medan bokning eller betalning behandlas.
6. När bokningen är bekräftad hämtas biljetterna. Kunden kan öppna eller ladda ner biljettens PDF.

Gästtokenen är åtkomstbeviset för den specifika bokningen. Kundwebben har alltså ingen allmän kundinloggning, men bokningsinformation lämnas inte ut utan token.

## Adminwebbens viktigaste flöden

Adminwebben innehåller bland annat dashboard, evenemangs- och scenhantering, bokningslistor och detaljer, biljettsskanner samt betalningssimulering.

När appen startar initieras Keycloak. En användare utan adminrollen får inte gå vidare till administrationsgränssnittet. Innan ett API-anrop skickas kontrollerar HTTP-hjälparen att tokenen finns och försöker förnya den om den snart går ut. Därefter skickas den som:

```http
Authorization: Bearer <token>
```

Dashboardens flöde, statistik och uppdateringar beskrivs i [Admin-dashboarden](admin_dashboard.md). Adminsidorna använder mutationer för att spara ändringar, exempelvis när ett evenemang skapas eller redigeras.

## API-anrop och data

API-funktioner ligger nära den feature som använder dem, till exempel `features/events/api/events.ts`. De ansvarar för att bygga URL:er, query-parametrar och request-typer. Den gemensamma HTTP-hjälparen ansvarar för själva nätverksanropet och grundläggande felhantering.

Exempel på den uppdelningen:

```text
HomePage
  → useInfiniteQuery
  → getEventPage() i events/api/events.ts
  → getJson() i shared/api/http.ts
  → fetch('/api/ListEvents?...')
  → backend svarar med JSON
  → React Query lagrar svaret
  → HomePage visar resultatet
```

Kundwebben använder bland annat `/api` för katalogdata och `/booking-api` för bokningar. Adminwebben använder även `/ticket-api` och `/payment-api`. Vite och Nginx skickar hela sökvägen till ApiGateway genom API_GATEWAY_PROXY_TARGET. Gatewayen väljer backendtjänst och tar bort prefixet.

Backendtjänsterna är källan till API-kontrakten. Frontendens TypeScript-typer beskriver de fält som respektive app använder, men ersätter inte backendens validering eller affärsregler. Se också [frontendens arkitektur- och API-konventioner](frontend-architecture.md).

### Från komponent till HTTP-svar

En API-funktion bör ta emot vanliga parametrar (till exempel ett ID eller filter), bygga sökväg och query-parametrar och returnera typat data. Komponenten ska normalt inte själv bygga URL:er eller anropa `fetch`. Det samlar nätverksdetaljerna på ett ställe och gör API-funktionerna lättare att hitta och ändra.

Kundwebbens `getJson` accepterar en `AbortSignal`. Skicka vidare signalen från React Query så att en begäran kan avbrytas om frågan blir inaktuell, till exempel när sökningen ändras snabbt. API-funktionerna ska inte svälja fel: HTTP-hjälparen kastar ett `ApiError`, så att frågan eller mutationen kan hamna i felläge och sidan kan visa ett begripligt meddelande.

Adminwebbens `request` och `requestBlob` lägger på autentiseringshuvudet automatiskt. JSON-begäranden som skickar en kropp anger också `Content-Type: application/json`. Filuppladdning och nedladdning använder separata flöden (till exempel `Blob`) och ska inte behandlas som JSON. Kontrollera alltid om svaret är `204 No Content` innan du försöker läsa JSON; hjälparna hanterar detta åt API-modulerna.

Kundwebbens gästtoken och adminwebbens Keycloak-token har olika behörighet och livscykel. Skicka aldrig en token till en annan app eller logga den. Åtkomstkontroll och validering måste fortfarande göras av backend; att dölja en knapp i gränssnittet är inte en säkerhetskontroll.

## React Query: frågor, mutationer och cache

En **query** representerar en läsning. Dess `queryKey` identifierar datan i cachen. Ta med de värden som påverkar svaret, såsom ID, söktext, filter och aktuell sida. Då kan två olika sökningar inte råka dela cachepost. Query-funktionen anropar featurets API-funktion och skickar vidare React Querys `signal` om den finns.

En **mutation** representerar en skrivning, till exempel att skapa ett evenemang eller avboka en bokning. När den lyckas behöver berörda queries synkroniseras. `invalidateQueries` markerar matchande data som inaktuell så att den kan hämtas igen; `setQueryData` passar när det nya, korrekta värdet redan finns och kan skrivas till cachen direkt. Välj query-nycklar konsekvent så att rätt listor och detaljer uppdateras.

Visa separata gränssnittstillstånd för första laddningen, tomt resultat, fel och uppdatering. För listor med flera sidor ska ett fel vid nästa sida inte radera sidor som redan visas. Undvik också att skapa en egen kopia av serverdata i komponent-state: den kan hamna ur synk med React Query-cachen.

## Laddning, fel och uppdatering

React Query-frågor ger komponenten status som exempelvis:

- `isPending`: data håller på att hämtas och saknas ännu.
- `isError`: hämtningen misslyckades.
- `isSuccess`: data finns.
- `refetch()`: försök hämta frågans data igen.

Vid en åtgärd som ändrar data används ofta `useMutation`. När åtgärden lyckas kan appen uppdatera cache direkt med `setQueryData` eller tala om för React Query att en fråga ska hämtas på nytt med `invalidateQueries`.

Det är viktigt att skilja mellan fel vid första hämtningen och fel vid hämtning av mer data. Om en lista redan har resultat och nästa sida misslyckas bör listan ligga kvar, med ett separat retry för nästa sida.

## Tid och datum i gränssnittet

Backend skickar datum och tid som värden som frontend tolkar som `Date`. När användaren ska se en evenemangstid formateras den för svensk tidszon, vanligtvis `Europe/Stockholm`. Om ett filter avser en svensk kalenderdag ska kalenderdatum och en exakt tidsstämpel hållas isär: en kalenderdag är inte alltid 24 timmar lång eftersom sommartid ändras. Datumgränser för API-anrop behöver därför följa kontraktet och tidszonspolicyn som används av backend.

## Köra frontend lokalt

Öppna PowerShell eller en terminal i appens mapp, till exempel `src/frontend/customer-web`:

```bash
npm install
npm run dev
```

För adminwebben används motsvarande kommando från `src/frontend/admin-web`. Adminwebben behöver också att den lokala Keycloak-konfigurationen och backendtjänsterna är tillgängliga. Se apparnas README-filer och [snabbstarten](../README.md) för Docker Compose och lokala proxyinställningar.

Tillgängliga npm-skript i båda apparna:

| Kommando | Syfte |
| --- | --- |
| `npm run dev` | Startar Vites utvecklingsserver. |
| `npm run build` | Kör TypeScript-kontroller och bygger produktionsfiler. |
| `npm run lint` | Kör ESLint-kontroller. |
| `npm run preview` | Förhandsvisar en redan byggd version. |

Vites proxyinställningar finns i respektive `vite.config.ts`. Prefixen (`/api`, `/booking-api`, `/ticket-api` och `/payment-api`) behålls av Vite och tas bort först i ApiGateway. Om sidan laddas men dataanrop misslyckas, kontrollera först terminalen där Vite kör, webbläsarens Network-flik och att backendtjänsten/proxy-målet är igång. Ett `401` eller `403` i adminwebben pekar ofta på inloggning eller behörighet; ett `404` kan betyda fel sökväg eller att resursen saknas. Se apparnas README-filer för proxyportar och miljövariabler.

## Var börjar jag när jag ska ändra något?

| Jag vill... | Börja här |
| --- | --- |
| Ändra kundens startsida | `customer-web/src/features/events/pages/HomePage.tsx` |
| Ändra evenemangets detaljsida | `customer-web/src/features/events/pages/EventDetailPage.tsx` |
| Ändra bokningsflödet | `customer-web/src/features/bookings/` |
| Ändra adminens dashboard | `admin-web/src/features/dashboard/` |
| Ändra adminens evenemangshantering | `admin-web/src/features/events/` |
| Ändra hur en feature anropar API:t | Den featurets `api/`-mapp |
| Ändra gemensam HTTP- eller felhantering | Appens `src/shared/api/http.ts` |
| Ändra vilka sidor en app har | Appens `src/app/router.tsx` |
| Ändra utseende och färgtema | Appens `src/app/theme.ts` eller `src/theme.ts` |

När en backendändring påverkar request- eller responseformatet behöver API-modulen och typerna i alla berörda appar ses över. Kund- och adminwebben har separata transport- och autentiseringsregler, så deras HTTP-hjälpare ska inte blandas ihop.

## Ordlista

| Begrepp | Förklaring |
| --- | --- |
| **Komponent** | En React-funktion som beskriver en del av gränssnittet. |
| **Props** | Data och funktioner som en komponent får från sin förälder. |
| **State** | Data som en komponent håller reda på, till exempel texten i en sökruta. |
| **Route** | Regeln som kopplar en URL till en sida. |
| **API** | Ett gränssnitt som låter frontend och backend utbyta data. |
| **Query** | En läsning av data som React Query hanterar. |
| **Mutation** | En åtgärd som ändrar data, till exempel att skapa eller avboka en bokning. |
| **Cache** | En tillfällig kopia av data som minskar onödiga hämtningar och gör gränssnittet snabbare. |
| **Bearer-token** | En token som skickas med en begäran för att visa vem användaren är och vad den får göra. |
| **Gästtoken** | En hemlig token som ger åtkomst till en specifik bokning utan vanlig användarinloggning. |
| **Proxy** | En mellanväg som tar emot ett frontendanrop och skickar det vidare till rätt backend. |

## Kodreferenser

- [Kundappens start och providers](../src/frontend/customer-web/src/main.tsx)
- [Kundappens sidor och routning](../src/frontend/customer-web/src/app/router.tsx)
- [Kundens evenemangslista](../src/frontend/customer-web/src/features/events/pages/HomePage.tsx)
- [Kundens bokningssida](../src/frontend/customer-web/src/features/bookings/pages/BookingPage.tsx)
- [Adminappens start och behörighetskontroll](../src/frontend/admin-web/src/main.tsx)
- [Adminappens sidor och routning](../src/frontend/admin-web/src/app/router.tsx)
- [Kundappens HTTP-hjälpare](../src/frontend/customer-web/src/shared/api/http.ts)
- [Adminappens HTTP-hjälpare](../src/frontend/admin-web/src/shared/api/http.ts)
- [Gatewayens reverse proxy](../src/ApiGateway/Program.cs)
- [Gatewayens rutter och destinationer](../src/ApiGateway/appsettings.json)
