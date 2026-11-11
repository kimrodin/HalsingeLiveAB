# TicketService – guide till biljettsystemet

Den här guiden förklarar TicketService för dig som är ny i projektet. Den beskriver vilken uppgift tjänsten har, hur en biljett skapas och används, vilka tekniker som ingår och varför koden är uppdelad som den är.

## TicketService i en mening

TicketService är en separat backendtjänst som sparar utfärdade biljetter, skapar biljett-PDF:er med QR-kod och hanterar biljettens status, till exempel giltig, incheckad eller avbokad.

Den är separat från BookingService eftersom en bokning och en biljett har olika livscykler. Booking hanterar reservation och betalning. TicketService ansvarar för biljetten som används efter att bokningen har bekräftats.

## Tekniker

| Teknik | Användning |
| --- | --- |
| **ASP.NET Core / .NET 10** | HTTP-API, dependency injection, konfiguration och autentisering. |
| **C#** | Ticketmodellen, validering och applikationslogik. |
| **RabbitMQ** | Levererar bekräftade bokningsrader till biljettutfärdandet. |
| **Entity Framework Core** | Läser och skriver biljetter i databasen. |
| **PostgreSQL + Npgsql** | Beständig lagring och databasanslutning. |
| **JWT Bearer / Keycloak** | Skyddar admin-API:er med administratörens token. |
| **Intern API-nyckel** | Skyddar tjänst-till-tjänst-anrop från BookingService. |
| **QRCoder** | Skapar en QR-kod som innehåller biljettens ID. |
| **QuestPDF** | Skapar biljett-PDF:er vid behov. |

## Var finns koden?

```text
src/services/TicketService/
  Program.cs                         Konfiguration, tjänster, auth och middleware
  Api/TicketsController.cs           HTTP-endpoints och åtkomstkontroll
  Data/TicketDbContext.cs             EF Core-modell och databasregler
  Features/Tickets/Models/            Biljettmodell och statusar
  Features/Tickets/IssueTickets/      Utfärdande och idempotens
  Features/Tickets/DownloadTicket/    QR-kod och PDF-generering
  messaging/Issuance/                 RabbitMQ-konsument och kötopologi
  Infrastructure/Messaging/           Gemensam RabbitMQ-anslutning
```

`TicketsController` tar emot HTTP-anrop och översätter dem till användning av databasen eller `IssueTicketsHandler` och `TicketDocumentService`. Det håller transportdetaljer (URL:er, headers och HTTP-statusar) separerade från delar som utfärdar biljetter eller skapar dokument.

## Biljettens data och status

En biljett har ett eget `TicketId` och kopplas till en bokning, bokningsrad, ett evenemang, en scen och en sektion. Den sparar också en ögonblicksbild av namn, starttid och pris. Det gör att biljetten kan visa informationen som gällde när den utfärdades även om kataloginformationen senare ändras.

Statusarna i `TicketStatus` är:

| Status | Betydelse |
| --- | --- |
| `Valid` | Kan användas för entré. |
| `CheckedIn` | Har redan använts vid entrén. |
| `Cancelled` | Är makulerad och ska inte godtas. |

Databasen använder `TicketId` som primärnyckel och har bland annat ett unikt index på kombinationen bokningsrad och biljettnummer. Det hindrar att samma nummer på samma bokningsrad utfärdas två gånger, även om två processer försöker samtidigt. Check constraints skyddar även mot ogiltigt biljettnummer och negativt pris.

## Så skapas biljetter efter betalning

Det förenklade flödet är:

```text
Booking bekräftar en betald bokning
  → Booking sparar ett ticket-issue-meddelande i sin outbox
  → en dispatcher publicerar BookingConfirmed till RabbitMQ
  → BookingConfirmedConsumer tar emot meddelandet
  → IssueTicketsHandler skapar en biljett per köpt plats
  → konsumenten kvitterar meddelandet när biljetterna har sparats
```

En **outbox** är en tabell där Booking sparar ett meddelande i samma transaktion som sin egen ändring. En separat dispatcher publicerar det till RabbitMQ och markerar det levererat när brokern har bekräftat publiceringen. TicketService behandlar meddelandet och kvitterar först efter att biljetterna har sparats. På så sätt kan tjänsterna arbeta oberoende och hantera tillfälliga avbrott.

Utvecklingsmiljöns seedbokningar är ett undantag: dispatchern anropar `POST /tickets/issue-from-booking` med intern API-nyckel och väntar på svaret. HTTP-endpointen använder samma `IssueTicketsHandler`. Det gör att seedradering kan samordnas med pågående utfärdande utan kvarvarande seedmeddelanden i RabbitMQ.

### Varför utfärdandet är idempotent

Ett meddelande kan levereras igen om konsumenten inte hann kvittera det, även om TicketService redan hann skapa biljetterna. Därför söker `IssueTicketsHandler` först efter biljetter på bokningsraden. Om de redan finns och innehållet stämmer returneras de befintliga biljetterna. Om samma bokningsrad kommer tillbaka med andra uppgifter blir det en konflikt i stället för att en andra uppsättning skapas.

Databasens unika index hanterar samtidigt kapplöpningar där två anrop försöker utfärda samma biljetter parallellt. Kombinationen av applikationskontroll och databasregel gör upprepade leveranser säkra utan att lita enbart på nätverkslagret.

## Biljett-PDF och QR-kod

PDF-filen skapas först när någon begär den; PDF:er lagras alltså inte som filer i databasen. `TicketDocumentService` bygger dokumentet med QuestPDF och skapar QR-bilden med QRCoder.

QR-koden innehåller endast biljettens GUID. Den innehåller inte namn, e-post, betalningsinformation eller andra kunduppgifter. Skannern kan därmed använda ID:t för att fråga systemet om biljettens riktiga status. QR-koden i sig är inte bevis på att biljetten fortfarande är giltig.

Evenemangets starttid sparas som ett tidsögonblick (`DateTimeOffset`) och visas på PDF-biljetten i tidszonen `Europe/Stockholm`.

## API och säkerhetsgränser

TicketService erbjuder i huvudsak följande typer av endpoints:

| Typ | Exempel | Skydd |
| --- | --- | --- |
| Utfärda biljetter för seedbokningar via HTTP | `POST /tickets/issue-from-booking` | Intern API-nyckel från BookingService. |
| Läsa biljett för kundens bokning och hämta PDF | `/tickets/by-booking/...` | Intern API-nyckel; Booking verifierar kundens gästtoken innan proxyn används. |
| Administrera och läsa biljetter | `/tickets/admin/...` | Keycloak JWT och rollen `admin`. |
| Ändra biljettstatus | `POST /tickets/{ticketId}/check-in` eller `/cancel` | Intern API-nyckel; adminwebben går via BookingService som kontrollerar adminbehörighet. |

Interna endpoints publiceras inte som ett sätt för webbläsaren att kringgå BookingService. BookingService fungerar som gräns för gästtoken och administrativa rättigheter. Interna nycklar ska läsas från konfiguration eller hemlighetshantering, inte checkas in i källkod.

Vid check-in kontrolleras att ID:t finns, att valt event motsvarar biljettens event och att status fortfarande är `Valid`. Endast då ändras status till `CheckedIn`. Databasfältet för status är en concurrency token; om samtidiga anrop tävlar om samma biljett fångas konflikten och systemet svarar med biljettens nuvarande ogiltiga status i stället för att godkänna den två gånger.

## Varför denna uppdelning?

- **En tjänst äger biljettstatusen.** Skanning och bokningsvyer behöver samma svar på frågan om en biljett är giltig.
- **Outbox och återförsök tål tillfälliga avbrott.** Biljettutfärdandet kan slutföras efter att bokningen har bekräftats även om TicketService inte svarade direkt.
- **Idempotens skyddar mot dubbla meddelanden.** Återförsök är en normal del av distribuerade system, inte ett undantag.
- **PDF genereras vid begäran.** Det undviker att lagra och synkronisera en separat fil när biljettens uppgifter redan finns i databasen.
- **QR-koden innehåller bara ett ID.** Status och behörighet avgörs av servern, och kunddata exponeras inte direkt i koden.
- **Separata auth-gränser passar olika anrop.** Tjänster identifierar varandra med intern nyckel; administratörer autentiseras med JWT/roll.

## Bra ställen att börja läsa

1. Läs [Ticket.cs](../src/services/TicketService/Features/Tickets/Models/Ticket.cs) för att se vilka data en biljett består av.
2. Läs [TicketsController.cs](../src/services/TicketService/Api/TicketsController.cs) för endpointsen och säkerhetsgränserna.
3. Läs [IssueTicketsHandler.cs](../src/services/TicketService/Features/Tickets/IssueTickets/IssueTicketsHandler.cs) för validering och idempotent utfärdande.
4. Läs [TicketDocumentService.cs](../src/services/TicketService/Features/Tickets/DownloadTicket/TicketDocumentService.cs) för PDF- och QR-kodsgenerering.
5. Läs [TicketDbContext.cs](../src/services/TicketService/Data/TicketDbContext.cs) för regler och index som skyddar datan.
6. Läs [TicketServices README](../src/services/TicketService/README.md) för kort information om PDF, intern nyckel och Booking-integrationen.

Se även [guiden till entréskannern](entreskanner-guide.md), som använder biljett-ID:t i QR-koden.
