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
docker compose logs --tail=100 keycloak api-gateway
curl --fail http://localhost:8180/realms/halsingelive/.well-known/openid-configuration
```

Använd `curl.exe` i Windows PowerShell 5. Vid `401`, kontrollera issuer och
discovery; vid `403`, adminrollen. Redirect-fel kräver att appens adress matchar
klientinställningarna.

## Kodreferenser

- [Realmimport: klient, roller och tokeninställningar](../infrastructure/keycloak/halsingelive-realm.json)
- [Keycloak-serverns start och importkonfiguration](../src/Compose.yaml)
- [Adminappens Keycloak-klient](../src/frontend/admin-web/src/app/auth/keycloak.ts)
- [Inloggningsstart, PKCE och kontroll av adminroll](../src/frontend/admin-web/src/main.tsx)
- [Tokenförnyelse och autentiserade anrop](../src/frontend/admin-web/src/shared/api/http.ts)
- [Bookings JWT-validering och adminpolicy](../src/services/Booking/program.cs)
- [PaymentSimulators adminautentisering](../src/services/PaymentSimulator/Authentication/AdminAuthentication.cs)
