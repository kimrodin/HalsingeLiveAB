# Administrations-API för betalningssimulering

PaymentSimulator erbjuder inställningar som kan ändras under körning via adminwebben:

| Metod | Sökväg | Resultat |
| --- | --- | --- |
| GET | `/api/admin/payment-simulation` | De inställningar som gäller just nu |
| PUT | `/api/admin/payment-simulation` | Ersätter inställningarna och returnerar de sparade värdena |

Båda endpointsen använder samma Keycloak-policy, `AdminOnly`, som övriga admin-API:er.
Skicka `Authorization: Bearer <access-token>` med den inloggade administratörens
Keycloak-token. Saknad eller ogiltig token ger 401. En autentiserad användare utan
rollen `admin` får 403. Ingen intern API-nyckel krävs.

Klicka på **Betalningssimulator** bredvid seedknapparna i sidomenyn i adminwebbens
utvecklingsläge. Dialogen hämtar aktuella inställningar och låter administratören
spara utfall, fördröjning och tidsgräns. Den döljs i produktionsbyggen, liksom
seedverktygen. Dialogen förklarar att bara nya betalningsförsök påverkas.

Både Vite och Nginx i grundkonfigurationen för Compose skickar `/payment-api`
vidare till ApiGateway med `API_GATEWAY_PROXY_TARGET`, som i Compose är
`http://api-gateway:8080`. Gatewayen tar bort `/payment-api` och behåller
backendens `/api`-prefix. Från värddatorn används
`http://localhost:5212/payment-api/api/admin/payment-simulation`, eller samma
sökväg på adminappens adress. När Vite körs separat på värddatorn är gatewayens
standardadress `http://localhost:5212`. Starta om Vite eller återskapa containern
om proxykonfigurationen ändras. Dialogen finns i Vites utvecklingsläge, medan
även det byggda Nginx-läget vidarebefordrar dessa API-sökvägar.

Autentiseringen använder målgruppen (`audience`) `eventcatalog` och tokenfältet
`roles` på översta nivån, precis som Booking och TicketService. Standardadressen
för identitetsutfärdaren (`authority`) är `http://localhost:8180/realms/halsingelive`.
Den kan ersättas med `Authentication__Authority`. `Authentication__MetadataAddress`
kan ange en intern adress för identifieringsmetadata i containrar. Compose
konfigurerar båda adresserna så att de stämmer med övriga tjänster. Utanför
`Development` krävs HTTPS för metadata. Befintliga betalningsendpoints behåller sitt beteende.

Exempel på innehåll i ett PUT-anrop, där alla tre fält krävs:

```json
{
  "outcome": "Failed",
  "delayMs": 500,
  "timeoutMs": 5000
}
```

`outcome` accepterar `Succeeded`, `Failed`, `TimedOut` eller `Random`. `Random` ger
varje ny betalning lika stor sannolikhet för `Succeeded`, `Failed` och `TimedOut`.
Det valda utfallet sparas när försöket skapas och återanvänds vid nya försök och
återhämtning. `delayMs` får inte vara negativt. `timeoutMs` måste vara mellan 1
och 30000. Ogiltiga inställningar ger 400.

Om fördröjningen överstiger tidsgränsen får en ny betalning timeout när tidsgränsen
nås, oavsett valt utfall. Vid uttryckligt `TimedOut` används den tidigaste av
fördröjningen och tidsgränsen. Återställ normalt beteende genom PUT med
`Succeeded`, fördröjning 500 och tidsgräns 5000.

Inställningarna gäller globalt för nya betalningsförsök, sparas i PostgreSQL och
finns kvar efter omstart. Befintliga väntande och avslutade försök behåller sin
sparade plan och sitt resultat. Använd en ny bokning för att testa ett ändrat
utfall. Ett nytt försök med samma bokning återanvänder betalningen. Vid samtidiga
uppdateringar gäller den senast sparade ändringen.

Fram till första lyckade PUT används konfigurationen `PaymentSimulation`, med
standardvärdena `Succeeded` / 500 / 5000. När inställningar har sparats har de
företräde framför konfigurationen och Composes miljövariabler för simulering.
Att ändra dessa variabler eller återskapa containern återställer inte sparade inställningar.

Vid uppstart tillämpas den medföljande EF-migreringen automatiskt. Exempel finns
även i `src/services/PaymentSimulator/Payments.http`.

## Kodreferenser

- [Endpoints för administrativa inställningar](../src/services/PaymentSimulator/features/payments/Simulation/SimulationSettingsController.cs)
- [Lagring, validering och standardkonfiguration för inställningar](../src/services/PaymentSimulator/features/payments/Simulation/SimulationSettingsService.cs)
- [Lagring och körning av betalningsförsök](../src/services/PaymentSimulator/features/payments/ProcessPayment/ProcessPaymentHandler.cs)
- [Autentiseringspolicy för administratörer](../src/services/PaymentSimulator/Authentication/AdminAuthentication.cs)
- [Inställningsdialog i adminappen](../src/frontend/admin-web/src/features/payment-simulation/components/PaymentSimulationDialog.tsx)
- [Typad API-klient i frontend](../src/frontend/admin-web/src/features/payment-simulation/api/paymentSimulation.ts)
- [Exempel på HTTP-anrop](../src/services/PaymentSimulator/Payments.http)
- [Integrationstester för inställningarnas API](../tests/PaymentSimulator.IntegrationTests/SimulationSettingsHttpTests.cs)
