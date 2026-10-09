# Stöd för scrum master – måndag 12 oktober 2026

Ta med en bild av nuläget, ett förslag på sprintmål och frågor som hjälper teamet framåt. Du behöver inte kunna all kod.

## Nuläge i projektet

| Område | Nuläge i koden |
|---|---|
| Backend | EventCatalog i C# med PostgreSQL och API för evenemang och lokaler. |
| Admingränssnitt | React med lista och formulär för evenemang. Använder fortfarande mockdata. |
| Lokalhantering | Backend har stöd för att skapa, läsa, ändra och radera. Frontendens sida ”Scener” är bara en rubrik. |
| Validering | `UpdateVenueRequest.cs` har regler för namn, adress, kapacitet och sittplatstyp. |
| Databas | Lokal start finns dokumenterad i `README.MD`, men EF Core-migrationer saknas enligt README. |
| Planering och tester | Ingen dokumenterad sprintbacklog eller automatiserade tester hittades i repot. De kan finnas utanför projektet. |

Bedömningen bygger på kod och dokumentation granskade den 9 oktober 2026. Systemet har inte körts och eventuell extern projekttavla har inte granskats.

## Ta med till mötet

### 1. Aktuell tavla eller backlog

Be teamet tydliggöra vad som är klart, pågående och blockerat. Kod som finns behöver fortfarande vara verifierad innan den räknas som färdig.

### 2. Förslag på sprintmål

> En administratör kan skapa ett evenemang kopplat till en verklig lokal och se att det sparats i databasen.

Detta är ett förslag att diskutera med produktägaren och teamet. Det ger ett gemensamt mål för frontend, backend och databas.

### 3. Viktiga beroenden

- Frontend använder mockdata och behöver kopplas till API:t.
- Fälten behöver stämma överens mellan frontend och backend, exempelvis lokal, pris och datum.
- Ett fungerande flöde för att skapa evenemang behöver tillgängliga lokaler i databasen.
- Teamet behöver en gemensam rutin för databasändringar eftersom migrationer saknas.

### 4. Frågor om hinder

- Kan alla starta projektet lokalt?
- Har alla tillgång till den databas de behöver?
- Väntar någon på API, beslut eller kodgranskning?
- Finns det uppgifter som är för stora eller otydliga och behöver delas upp?
- Vilka beroenden behöver lösas först för att nå sprintmålet?

### 5. Gemensam definition av klart

Föreslå att en uppgift ska:

- Uppfylla sina acceptanskriterier.
- Vara kodgranskad.
- Vara verifierad i det avsedda flödet.
- Ha relevanta kontroller av felaktiga indata och felhantering.

Kom överens om definitionen tillsammans med teamet.

## Exempel: acceptanskriterier för uppdatera lokal

- Giltiga uppgifter sparas och kan läsas tillbaka.
- Saknat namn eller adress, kapacitet under 1 och ogiltig sittplatstyp avvisas.
- Namn över 200 tecken och adress över 500 tecken avvisas.
- En lokal som saknas ger HTTP 404.
- En lyckad uppdatering ger HTTP 204.

## Förslag på öppning

> Vad vill vi kunna demonstrera i slutet av sprinten, vad hindrar oss från det och vilka uppgifter behöver vi prioritera tillsammans?

## Din roll under mötet

Gör mål, hinder och nästa steg tydliga. Hjälp teamet att hålla fokus och se till att identifierade hinder får en ansvarig person och en uppföljning.

Produktägaren ansvarar för prioriteringen och utvecklarna för hur arbetet genomförs.

## Anteckningar från mötet

- **Överenskommet sprintmål:**
- **Prioriterade uppgifter:**
- **Hinder och vem som följer upp:**
- **Beslut:**
- **Nästa uppföljning:**
