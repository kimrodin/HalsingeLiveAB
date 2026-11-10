# Evenemangsbilder

Varje evenemang kan ha en uppladdad affisch i PNG- eller JPEG-format, med en
standardgräns på 10 MiB. Metadata lagras i `EventImages` och filinnehållet hanteras
av `IEventImageStorage`, enligt samma lagringsmönster som scenernas platskartor.
Befintliga evenemang behöver inte ha någon bild.

Båda funktionerna använder `Shared/Files` (`IFileStorage`, `LocalFileStorage`,
`FileUploadReader` och `FileContentType`) för diskåtkomst, läsning med storleksgräns
samt kontroll av filsignaturer och filändelser. Funktionsspecifika gränssnitt och
inställningar håller kataloger och formatregler åtskilda. Modeller, HTTP-endpoints
och kompensation mellan databas och filer ligger kvar i respektive funktion.
Omstruktureringen till gemensam filhantering kräver ingen databasmigrering.

Spara först evenemanget i adminappen, öppna sedan **Redigera** och använd
**Evenemangsbild** för att ladda upp, ersätta eller ta bort affischen. Vid ett byte
får bilden ett nytt ID och en ny URL. Den gamla adressen slutar fungera. Svaren för
evenemangslistor och detaljer innehåller ett `imageUrl` som kan vara `null`, men
visar aldrig den interna lagringsnyckeln.

Dashboarden samt kundwebbens lista, karusell och detaljsida använder denna URL.
Kundsidorna visar Hälsinge Lives standardaffisch när en bild saknas eller inte kan laddas.

## API

- `POST /UploadEventImage/{eventId}`: administratörsskyddad multipart-uppladdning
  (`Name`, `File`). Returnerar bildmetadata och nedladdningsadress. Ersätter en befintlig affisch.
- `GET /DownloadEventImage/{eventId}/{imageId}`: offentlig visning av bilden direkt
  i svaret, eller 404 om kombinationen av evenemang och bild saknas.
- `DELETE /DeleteEventImage/{eventId}`: administratörsskyddad radering som returnerar 204 eller 404.

Servern kontrollerar PNG/JPEG-signaturer och att filändelsen stämmer. SVG och PDF
accepteras inte. Hela anropets innehåll får vara högst 11 MiB inklusive
multipart-fält. `EventImageStorage:MaxFileSizeBytes` kan sänka standardgränsen på 10 MiB per fil.

## Lagring och migrering

EF-migreringen `AddEventImages` skapar en unik främmande nyckel till evenemanget
med kaskadradering. Docker Compose monterar `event-images-data` på
`/app/uploads/event-images` och sätter `EventImageStorage__RootPath` till katalogen.
Vid lokal .NET-körning bör `EventImageStorage:RootPath` ange en beständig och
skrivbar katalog. Standardvärdet är annars `event-images` under appens baskatalog.

När ett vanligt evenemang raderas tas även affischfilen bort. Seeddata kan inte
raderas så länge seedevenemangen har bilder, vilket motsvarar skyddet för
platskartor. Misslyckad filradering efter en genomförd databasändring loggas för
senare städning. Filsystemskrivningar och databastransaktioner kan inte genomföras
atomärt tillsammans. Säkerhetskopiera både databasen och bildvolymen. Tillämpa
migreringar genom den vanliga driftsättningsprocessen. I `Development` med
`InitializeDatabase=true` tillämpas de vid uppstart.

## Kodreferenser

- [Endpoints för uppladdning, nedladdning och radering](../src/services/EventCatalog/features/events/Images/EventImagesController.cs)
- [Validering av uppladdning och kompensation mellan databas och filer](../src/services/EventCatalog/features/events/Images/UploadEventImageHandler.cs)
- [Bildradering och filstädning](../src/services/EventCatalog/features/events/Images/DeleteEventImageHandler.cs)
- [Filläsning med storleksgräns](../src/services/EventCatalog/shared/Files/FileUploadReader.cs)
- [Kontroll av filsignatur och filändelse](../src/services/EventCatalog/shared/Files/FileContentType.cs)
- [Migrering för bildtabellen](../src/services/EventCatalog/data/Migrations/20261029101740_AddEventImages.cs)
- [Bildhantering i adminappen](../src/frontend/admin-web/src/features/events/components/EventImageManager.tsx)
- [Integrationstester för bilder](../tests/EventCatalog.IntegrationTests/Features/Events/EventImageHandlerTests.cs)
