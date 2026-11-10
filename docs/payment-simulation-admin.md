# Admin-API för betalningssimulatorn

PaymentSimulator tillhandahåller ett API för att ändra inställningarna från AdminWeb medan tjänsten körs:

| Metod | Sökväg | Resultat |
| --- | --- | --- |
| GET | `/api/admin/payment-simulation` | Aktuella inställningar |
| PUT | `/api/admin/payment-simulation` | Ersätt inställningarna och returnera de sparade värdena |

Båda ändpunkterna använder samma Keycloak-policy, `AdminOnly`, som de övriga admin-API:erna.
Skicka `Authorization: Bearer <access-token>` med den inloggade administratörens Keycloak-token.
En saknad eller ogiltig token ger 401; autentiserade användare utan rollen `admin` får 403.
Ingen intern API-nyckel krävs.

I AdminWebs utvecklingsläge klickar du på **Betalningssimulator** bredvid knapparna för
seeddata i sidomenyn. Dialogrutan hämtar de aktuella inställningarna och låter en
administratör spara utfall, fördröjning och timeout. Den är dold i produktionsbyggen,
precis som kontrollerna för seeddata. Dialogrutan förklarar att endast nya betalningsförsök påverkas.

Både Vite och Nginx-konfigurationen i Compose-grundfilen vidarebefordrar `/payment-api`
till ApiGateway via `API_GATEWAY_PROXY_TARGET` (i Compose: `http://api-gateway:8080`).
Gatewayen tar bort `/payment-api` men behåller backendens prefix `/api`.
Från värddatorn använder du `http://localhost:5212/payment-api/api/admin/payment-simulation`
eller samma sökväg på adminappens adress. Om Vite körs separat på värddatorn är
standardadressen till gatewayen `http://localhost:5212`. Starta om Vite eller återskapa
containern efter ändringar i proxykonfigurationen. Dialogrutan är tillgänglig i Vites
utvecklingsläge; Nginx-bygget i grundkonfigurationen vidarebefordrar fortfarande dessa API-sökvägar.

Autentiseringen använder målgruppen (`audience`) `eventcatalog` och tokenfältet
`roles` på översta nivån, precis som Booking och TicketService. Standardadressen
för identitetsutfärdaren (`authority`) är
`http://localhost:8180/realms/halsingelive`. Ändra den med `Authentication__Authority`;
`Authentication__MetadataAddress` kan ange en intern adress för hämtning av
autentiseringsmetadata i containrar. Compose konfigurerar båda adresserna så att de
stämmer överens med de övriga tjänsterna. Utanför `Development` måste metadata hämtas
via HTTPS. Befintliga ändpunkter för betalningar är oförändrade.

Exempel på en PUT-anropskropp (alla tre fälten är obligatoriska):

```json
{
  "outcome": "Failed",
  "delayMs": 500,
  "timeoutMs": 5000
}
```

`outcome` accepterar `Succeeded`, `Failed`, `TimedOut` eller `Random`. Med `Random`
har varje ny betalning 80 % sannolikhet för `Succeeded`, 15 % för `Failed` och 5 % för
`TimedOut`. Det valda utfallet sparas när betalningen skapas och återanvänds vid
återförsök och återhämtning. `delayMs` måste vara 0 eller större; `timeoutMs` måste
vara mellan 1 och 30000. Ogiltiga inställningar ger 400.
Om fördröjningen överstiger timeout får en ny betalning utfallet `TimedOut` när
tidsgränsen nås, oavsett valt utfall. `TimedOut`, oavsett om det väljs direkt eller
genom `Random`, förblir väntande tills hela timeout-tiden har gått.
`Succeeded` och `Failed` använder fördröjningen.
Återställ standardbeteendet via PUT med `Succeeded`, fördröjning 500 ms och timeout 5000 ms.

Inställningarna gäller globalt för nya betalningsförsök, lagras i PostgreSQL och
finns kvar efter omstart. Befintliga väntande och avslutade försök behåller sin
sparade plan och sitt resultat. Använd en ny bokning för att testa ett ändrat utfall;
ett återförsök för samma bokning återanvänder dess betalning.
Vid samtidiga uppdateringar gäller den senast sparade uppdateringen.

Fram till det första lyckade PUT-anropet använder tjänsten konfigurationen
`PaymentSimulation` (standard: `Succeeded` / 500 / 5000). När inställningarna har
sparats har de företräde framför konfigurationen och miljövariablerna för simulering
i Compose. Att ändra dessa variabler eller återskapa containern återställer inte de
sparade inställningarna.

Vid uppstart körs den medföljande EF-migreringen automatiskt. Exempel finns även i
`src/services/PaymentSimulator/Payments.http`.

## Kodreferenser

- [Endpoints för administrativa inställningar](../src/services/PaymentSimulator/features/payments/Simulation/SimulationSettingsController.cs)
- [Lagring, validering och standardkonfiguration för inställningar](../src/services/PaymentSimulator/features/payments/Simulation/SimulationSettingsService.cs)
- [Lagring och körning av betalningsförsök](../src/services/PaymentSimulator/features/payments/ProcessPayment/ProcessPaymentHandler.cs)
- [Autentiseringspolicy för administratörer](../src/services/PaymentSimulator/Authentication/AdminAuthentication.cs)
- [Inställningsdialog i adminappen](../src/frontend/admin-web/src/features/payment-simulation/components/PaymentSimulationDialog.tsx)
- [Typad API-klient i frontend](../src/frontend/admin-web/src/features/payment-simulation/api/paymentSimulation.ts)
- [Exempel på HTTP-anrop](../src/services/PaymentSimulator/Payments.http)
- [Integrationstester för inställningarnas API](../tests/PaymentSimulator.IntegrationTests/SimulationSettingsHttpTests.cs)
