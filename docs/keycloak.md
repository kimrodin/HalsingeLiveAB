# Keycloak och admininloggning

Keycloak startas och realmfilen `infrastructure/keycloak/halsingelive-realm.json`
importeras automatiskt enligt [snabbstarten](../README.md).

## Konton

| Användning | Adress | Konto och lösenord |
| --- | --- | --- |
| Adminappen | `http://localhost:5173` | `testadmin`, lösenord från `KEYCLOAK_APP_ADMIN_PASSWORD` |
| Keycloak-konsolen | `http://localhost:8180/admin` | `KC_BOOTSTRAP_ADMIN_USERNAME` och `KC_BOOTSTRAP_ADMIN_PASSWORD` |

Adminappen öppnar Keycloaks inloggning automatiskt. Realmimporten skapar rollen
`admin` och klienten `admin-web` med PKCE S256. API-token använder audience
`eventcatalog` och rollclaimen `roles`. Kundköp använder gästtoken.

## Adresser och befintliga installationer

`AUTHENTICATION_AUTHORITY` är den publika issuer-adressen på localhost:8180.
`AUTHENTICATION_METADATA_ADDRESS` använder `keycloak:8080` för intern discovery.
Exakta värden finns i [konfigurationsguiden](configuration.md); API-anrop går
via [gatewayens frontendproxy](frontend-guide.md).

Realmimporten hoppar över en befintlig realm. Ändrade lösenord i `.env` ändrar
inte befintliga app- eller konsolkonton i volymen `keycloak-data`.

Om adminport 5173 ändras behöver realmets `redirectUris`, `webOrigins` och
`post.logout.redirect.uris` uppdateras. Vid ändrad Keycloak-port uppdateras även
`KC_HOSTNAME`, `AUTHENTICATION_AUTHORITY` och adressen i
`src/frontend/admin-web/src/app/auth/keycloak.ts`. I en befintlig installation
ändras realmets adresser via Keycloak-konsolen. Använd `localhost` i webbläsaren.

## Administratörstoken vid lagerinitiering

Bookings `CreateEventInventoryFromCatalog` kräver den befintliga policyn
`AdminOnly`. EventCatalog vidarebefordrar den inloggade administratörens
Bearer-token vid skapande av evenemang och vid seedning. Booking validerar själv
tokenens signatur, issuer, audience `eventcatalog`, giltighet och rollen `admin`.
Anonyma anrop får `401`; autentiserade användare utan adminrollen får `403`.

Ingen extra Keycloak-klient, klienthemlighet eller provisionering behövs.
Tokenen läggs på varje utgående anrop och delas inte mellan administratörer.
Lagerläsningar är fortsatt publika; endpointen för anroparstyrt pris och antal
är borttagen och lagrets värden hämtas från katalogen.

Lagerinitiering kräver en aktiv administratörsförfrågan med en giltig token och
kan därför inte köras fristående i ett bakgrundsjobb. Om tokenen har gått ut under
en lång seedkörning behöver administratören förnya inloggningen och köra seedningen
igen. Ett `401` eller `403` från Booking avbryter det aktuella lagrets återförsök.

## Felsökning

```sh
docker compose logs --tail=100 keycloak keycloak-proxy api-gateway
curl --fail http://localhost:8180/realms/halsingelive/.well-known/openid-configuration
```

Använd `curl.exe` i Windows PowerShell 5. Vid `401`, kontrollera issuer och
discovery; vid `403`, adminrollen. Redirect-fel kräver att appens adress matchar
klientinställningarna.

## Skydd av inloggning

Keycloak kör `start --import-realm` med `KC_HTTP_ENABLED=true` för lokal HTTP
bakom proxyn. `KC_DB=dev-file` behåller utvecklingsdatabasen i `keycloak-data`.
Detta är fortfarande en lokal utvecklingskonfiguration: inför publik drift
behövs PostgreSQL och HTTPS. Att byta startkommando aktiverar inte kryptering.

Porten `KEYCLOAK_PUBLISHED_PORT` (normalt `127.0.0.1:8180`) går till
`keycloak-proxy`. Keycloak har ingen publicerad port. Den publika adressen,
issuer, adminwebbens inställning och intern discovery på `keycloak:8080` behålls.
Vid lokal körning ska även webbläsarens Keycloak-anrop gå genom proxyn.

Nginx begränsar per anslutande IP med två separata buckets: auth/login-actions
med 30 anrop/minut och tokenanrop med 60 anrop/minut. Båda tillåter burst 20
utan väntan och svarar sedan 429. Det är en leaky bucket, inte ett rullande
minutfönster som i Bookings Redis-limiter. Gränserna kan ändras med
`KEYCLOAK_LOGIN_RATE` och `KEYCLOAK_TOKEN_RATE`. Discovery, statiska resurser
och admin-API omfattas inte av dessa IP-kvoter. Tokenförnyelser räknas i tokenkvoten.
NAT-användare delar kvot. En proxyinstans delar räknare mellan sina workers;
flera proxyinstanser delar inte räknare. Omstart av proxyn återställer IP-kvoterna.

Proxyn ersätter forwardingheaders med anslutningens adress och protokoll.
Keycloak litar bara på `KEYCLOAK_PROXY_IP` (standard `172.30.151.5`). Ändras
Compose-subnätet måste denna adress också väljas inom nätverket och utanför
den dynamiska poolen. En extra ingress/CDN framför proxyn kräver separat
konfiguration och verifiering; annars delar kunder ingressens IP-kvot.
Proxyn slår upp Keycloak via Dockers DNS med fem sekunders cache, så att den
återhämtar sig vid byte av intern Keycloak-adress utan omstart av proxyn.

Realm-filen aktiverar tillfällig kontolåsning efter fem misslyckade inloggningar:
60 sekunders ökande väntan, högst 900 sekunder. Snabba upprepade fel kan ge
60 sekunders väntan redan tidigare. Felräknaren har ett återställningsintervall
på 12 timmar. Kontolåsning kompletterar IP-gränsen även vid byte av IP.
Skyddet gäller appens realm `halsingelive`; master-realmens kontoskydd
konfigureras separat. Dess inloggningsanrop omfattas av proxykvoterna.

Inställningarna importeras i en ny utvecklingsrealm. Befintliga realms importeras
inte om. Ingen uppdateringsmigration för befintliga realms ingår.
Vid tillfällig låsning: vänta ut låsningen eller häv den via Keycloaks
administration för användaren. IP-begränsning kräver att bucket återhämtas.

Rutinaccessloggar följer PR 152:s format utan IP, querysträng eller user agent.
Nginx fel- och Keycloak-diagnostik kan innehålla IP; containerloggar kräver
behörighet till Docker-värden. Proxyloggar roteras med samma Compose-inställning
som webbproxyernas. Keycloaks egen loggning har separat retention.

Verifiering: [Keycloak-säkerhetstester](../tests/keycloak-security/README.md).

## Kodreferenser

- [Realmimport: klient, roller och tokeninställningar](../infrastructure/keycloak/halsingelive-realm.json)
- [Keycloak-serverns start och importkonfiguration](../src/Compose.yaml)
- [Adminappens Keycloak-klient](../src/frontend/admin-web/src/app/auth/keycloak.ts)
- [Inloggningsstart, PKCE och kontroll av adminroll](../src/frontend/admin-web/src/main.tsx)
- [Tokenförnyelse och autentiserade anrop](../src/frontend/admin-web/src/shared/api/http.ts)
- [Bookings JWT-validering och adminpolicy](../src/services/Booking/program.cs)
- [PaymentSimulators adminautentisering](../src/services/PaymentSimulator/Authentication/AdminAuthentication.cs)
