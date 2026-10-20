# HälsingeLiveAB

Repositoryt innehåller EventCatalog API:t och en separat admin-webbklient. Compose-filen `src/Compose.yaml` startar EventCatalog, PostgreSQL, Keycloak och admin-webbklienten. Inloggning och lokal utveckling beskrivs i [keycloak.md](keycloak.md).

## Starta EventCatalog med Docker och Swagger

### Förutsättningar

- Docker Desktop med Linux-containrar aktiverade.
- .NET 10 SDK. Det behövs på värddatorn för att köra EF Core-migrationen.
- `dotnet-ef` 10.0.12. Installera verktyget om kommandot `dotnet ef` saknas:

  ```powershell
  dotnet tool install --global dotnet-ef --version 10.0.12
  ```

Kör kommandona nedan från repositoryts rotmapp.

### Starta med script

När förutsättningarna ovan är uppfyllda kan du köra ett av följande kommandon från repositoryts rotmapp.

Windows (PowerShell):

```powershell
.\scripts\start-docker.ps1
```

Windows (Om det inte fungerar):
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-docker.ps1
```
Mac (Terminal):

```bash
bash scripts/start-docker.sh
```

Scripten startar PostgreSQL, väntar tills databasen är redo, applicerar EF Core-migrationerna och bygger och startar alla tjänster. De kan även köras vid senare starter.

### Första starten

1. Starta PostgreSQL-containern:

   ```powershell
   docker compose -f src/Compose.yaml up -d eventcatalog-db
   ```

2. Applicera projektets EF Core-migrationer på databasen `eventcatalog_dev`. Migrationen körs från värddatorn, så anslutningen använder `localhost`:

   ```powershell
   $env:ConnectionStrings__EventCatalog = "Host=localhost;Port=5432;Database=eventcatalog_dev;Username=postgres;Password=postgres"

   dotnet ef database update `
     --context EventCatalog.Data.EventCatalogDbContext `
     --project src/Services/EventCatalog/EventCatalog.csproj `
     --startup-project src/Services/EventCatalog/EventCatalog.csproj

   Remove-Item Env:ConnectionStrings__EventCatalog
   ```

3. Bygg och starta API:t och databasen:

   ```powershell
   docker compose -f src/Compose.yaml up -d --build
   ```

4. Öppna Swagger UI:

   **http://localhost:8080/swagger**

Compose sätter `ASPNETCORE_ENVIRONMENT=Development`, vilket aktiverar Swagger. API:t lyssnar på port `8080`; PostgreSQL publiceras på port `5432`.

### Automatisk migration vid uppstart (valfritt)

Som standard körs migrationerna manuellt enligt stegen ovan. Om du vill att API:t ska applicera väntande EF Core-migrationer automatiskt när Compose startar behöver du aktivera det i API:ts `program.cs`:

1. Kontrollera att filen har `using Microsoft.EntityFrameworkCore;` och `using EventCatalog.Data;`. Direkt efter `var app = builder.Build();` lägger du till:

   ```csharp
   using (var scope = app.Services.CreateScope())
   {
       var db = scope.ServiceProvider.GetRequiredService<EventCatalogDbContext>();
       await db.Database.MigrateAsync();
   }
   ```

2. Kontrollera att `src/Compose.yaml` startar API:t först när PostgreSQL är redo:

   ```yaml
   depends_on:
     eventcatalog-db:
       condition: service_healthy
   ```

3. Bygg om och starta tjänsterna:

   ```powershell
   docker compose -f src/Compose.yaml up -d --build
   ```

Vid API-start kontrollerar `MigrateAsync()` `__EFMigrationsHistory` och applicerar väntande migrationer. Använd EF Core-migrationer för databasschemat och blanda inte `EnsureCreated()` med migrationer.

### Senare starter

När databasen redan har skapats och migrationerna har applicerats startar du tjänsterna så här:

```powershell
docker compose -f src/Compose.yaml up -d
```

Lägg till `--build` efter kodändringar som ska byggas in i API-containern.

Visa status och loggar:

```powershell
docker compose -f src/Compose.yaml ps
docker compose -f src/Compose.yaml logs -f eventcatalog
docker compose -f src/Compose.yaml logs -f eventcatalog-db
```

Stoppa containrarna utan att ta bort databasen:

```powershell
docker compose -f src/Compose.yaml down
```

PostgreSQL-data sparas i den namngivna volymen `eventcatalog-data`. Om du uttryckligen vill radera all lokal databasdata kan du ta bort volymen med `docker compose -f src/Compose.yaml down -v`. Det går inte att ångra.

### Köra API:t från IDE i stället för som container

Starta PostgreSQL med Compose och kör sedan:

```powershell
docker compose -f src/Compose.yaml up -d eventcatalog-db
dotnet run --project src/Services/EventCatalog/EventCatalog.csproj --launch-profile EventCatalog
```

Startprofilen använder `Host=localhost` och öppnar Swagger på **http://localhost:5080/swagger**. Applicera migrationerna enligt steget ovan första gången.

### Anslutningssträngar

API-containern ansluter till databasen via Compose-värdnamnet `eventcatalog-db`. Kommandon som körs på värddatorn, till exempel `dotnet ef database update`, ansluter via `localhost`. Databasen heter `eventcatalog_dev` i båda fallen.

PostgreSQL-imagets `POSTGRES_DB` används när datavolymen initieras första gången. Om en befintlig volym återanvänds skapar en ändring av `POSTGRES_DB` inte automatiskt en ny databas.

Om loggen visar att `eventcatalog_dev` saknas trots att PostgreSQL-containern körs kan du skapa databasen manuellt:

```powershell
docker compose -f src/Compose.yaml exec eventcatalog-db psql -U postgres -d postgres -c 'CREATE DATABASE eventcatalog_dev;'
```

Kör sedan `dotnet ef database update` enligt första-start-steget.

## Admin-webb

Admin-webbklienten ligger i `src/frontend/admin-web` och startas av Compose-konfigurationen i `src/Compose.yaml`. Den anropar EventCatalog API:t via frontend-containerns `/api`-proxy och använder Keycloak för inloggning.

### Utveckla frontend med Docker Compose

Dev-konfigurationen startar frontendens Vite-utvecklingsserver med hot reload. Kör kommandona från repositoryts rotmapp.

Första gången behöver databasen startas och migrationerna appliceras enligt avsnittet [Första starten](#första-starten). Starta sedan API:t, databasen och Keycloak:

```powershell
docker compose -f src/Compose.yaml up -d --build eventcatalog-db keycloak eventcatalog
```

Starta frontend i dev-läge med Compose-override-filen:

```powershell
docker compose -f src/Compose.yaml -f src/Compose.dev.yaml up -d --build frontend
```

Öppna **http://localhost:5173**. Ändringar i `src/frontend/admin-web` laddas om automatiskt. Inloggningen använder Keycloak på **http://localhost:8180**.

Visa frontendens loggar:

```powershell
docker compose -f src/Compose.yaml -f src/Compose.dev.yaml logs -f frontend
```

Stoppa tjänsterna när du är klar:

```powershell
docker compose -f src/Compose.yaml -f src/Compose.dev.yaml down
```

Compose-versionen måste stödja `!override` (Docker Compose 2.24.4 eller senare), som används för att ersätta frontendens vanliga portmappning med Vites dev-port.


## Testdata (seed) och återställning

SQL-filerna ligger i `src/Services/EventCatalog/Database/`:

- `reset.sql` raderar all data men behåller tabellen `__EFMigrationsHistory`.
- `seed.sql` lägger in scener, sektioner, event, priser och stolar.
- `seed-seating-maps.sql` lägger in platskartornas metadata. Själva bildfilerna lagras inte av filen.

Förutsättning: PostgreSQL-containern körs och migrationerna är applicerade (se "Första starten").

### Kör filerna

Kör från repositoryts rotmapp. Filerna kopieras in i containern och läses av `psql` direkt. Använd **inte** `Get-Content ... | docker compose exec ...`: PowerShell kodar om texten på vägen och å, ä och ö blir trasiga.

```powershell
$compose = "src/Compose.yaml"
$dir = "src/services/EventCatalog/Database"

foreach ($f in "reset.sql", "seed.sql", "seed-seating-maps.sql") {
  docker compose -f $compose cp "$dir/$f" "eventcatalog-db:/tmp/$f"
}

docker compose -f $compose exec eventcatalog-db psql -U postgres -d eventcatalog_dev -v ON_ERROR_STOP=1 -f /tmp/reset.sql
docker compose -f $compose exec eventcatalog-db psql -U postgres -d eventcatalog_dev -v ON_ERROR_STOP=1 -f /tmp/seed.sql
docker compose -f $compose exec eventcatalog-db psql -U postgres -d eventcatalog_dev -v ON_ERROR_STOP=1 -f /tmp/seed-seating-maps.sql
```

Hoppa över `reset.sql` om du vill behålla befintlig data. Kör den däremot alltid innan en ny seed, annars får du dubbletter eller primärnyckelfel.

### Kontrollera teckenkodningen

Kör i `psql` (`docker compose -f src/Compose.yaml exec eventcatalog-db psql -U postgres -d eventcatalog_dev`):

```sql
SELECT "VenueName", encode(convert_to("VenueName", 'UTF8'), 'hex') AS hex
FROM "Venues" WHERE "VenueId" = 'a1000000-0000-4000-8000-000000000001';
```

`Hälsinge Arena` ska ha `c3a4` efter `48`. Ser du `c383c2a4` är texten dubbelkodad: kör `reset.sql` och seeden igen enligt stegen ovan.

SQL-filerna ska vara sparade som UTF-8. I VS Code visas kodningen längst ner till höger.

### Platskartor

Bildfilerna måste läggas manuellt under respektive `StorageKey` tills backend har fillagring för platskartor.
