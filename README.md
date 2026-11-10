# Hälsinge Live

Biljettplattform med separata webbappar för administration och kunder.

## Starta lokalt

Du behöver Git, Docker med Linux-containrar och Compose 2.24.4 eller senare.
.NET och Node behöver inte installeras på värddatorn.

1. Klona projektet och öppna terminalen i projektroten.
2. Skapa en lokal, Git-ignorerad `.env` enligt [konfigurationsguiden](docs/configuration.md).
   Där finns alla obligatoriska variabler, generering av hemligheter och
   Compose-filval för Windows och macOS/Linux. Ingen miljömall ingår.
3. Starta hela systemet:

   ```sh
   docker compose up -d --build
   ```

Migrationerna körs automatiskt i API-containrarna med guidens utvecklingsinställningar.
Första bygget och Vites installation av beroenden kan ta en stund.

| Webbapp | Lokal adress |
| --- | --- |
| Admin | <http://localhost:5173> |
| Kund | <http://localhost:5174> |

## Kontrollera och gör ett testköp

```sh
docker compose ps
curl --fail http://localhost:5212/api/health/ready
```

Health-anropet ska ge HTTP 200 och `Healthy`. I Windows PowerShell 5 använder
du `curl.exe`. Se [health-guiden](docs/health-checks.md) för övriga tjänster.

1. Öppna adminappen och logga in som `testadmin` med lösenordet i
   `KEYCLOAK_APP_ADMIN_PASSWORD`. [Keycloak och inloggning](docs/keycloak.md).
2. Klicka **Skapa seeddata** och vänta tills den är klar. Knappen finns i
   Vite-läget med `Compose.dev.yaml`. [Testdata](docs/seed-purchases.md).
3. Öppna kundappen, välj ett framtida evenemang till försäljning, sektion och
   antal. Fyll i namn och e-post och slutför den simulerade betalningen.
4. Med `PAYMENT_SIMULATION_OUTCOME=Succeeded` ska bokningen bekräftas och
   biljetterna bli tillgängliga som PDF. Kontrollera även bokningen i adminappen.

Platskartor och evenemangsbilder laddas upp i adminappen och sparas automatiskt
i Compose-volymer. Gör ingen seedradering under pågående kundköp.

## Loggar och stopp

```sh
docker compose logs --tail=100
docker compose down
```

`down` behåller data; `down -v` raderar projektets volymer. Behåll samma `.env`
mellan starter: ändrade lösenord uppdaterar inte befintliga konton.

[Alla guider](docs/README.MD) · [Verifiering av snabbstarten](docs/quickstart-verification.md)
