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

## Felsökning

```sh
docker compose logs --tail=100 keycloak api-gateway
curl --fail http://localhost:8180/realms/halsingelive/.well-known/openid-configuration
```

Använd `curl.exe` i Windows PowerShell 5. Vid `401`, kontrollera issuer och
discovery; vid `403`, adminrollen. Redirect-fel kräver att appens adress matchar
klientinställningarna.