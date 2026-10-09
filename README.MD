# HalsingeLiveAB

## Testa EventCatalog lokalt

1. Starta Docker Desktop och starta PostgreSQL från repots rot:

   ```powershell
   docker compose -f src/services/Compose.yaml up -d eventcatalog-db
   ```

2. Öppna `HalsingeLiveAB.sln` i Visual Studio och välj `EventCatalog` som startprojekt.
   Startprofilen `EventCatalog` öppnar Swagger på `http://localhost:5080/swagger`.

   Vid första starten med en tom testdatabas, kör från repots rot:

   ```powershell
   dotnet run --project src/services/EventCatalog/EventCatalog.csproj --launch-profile EventCatalog -- --InitializeDatabase=true
   ```

   Flaggan skapar tabeller i en tom databas i Development. Den uppdaterar inte ett
   befintligt schema; projektet har ännu inga EF Core-migrationer.

3. Vid senare starter kan du använda F5 i Visual Studio eller:

   ```powershell
   dotnet run --project src/services/EventCatalog/EventCatalog.csproj --launch-profile EventCatalog
   ```

I Swagger, testa `POST /venues` med exempelvis:

```json
{
  "name": "Testlokal",
  "address": "Testgatan 1",
  "capacity": 100,
  "seatingType": 0
}
```

`seatingType` är `0` för fri placering och `1` för numrerade platser.
Använd lokalens returnerade id för att testa GET, PUT och DELETE.

Bygg hela solutionen med `dotnet build HalsingeLiveAB.sln`.
