# Keycloak utan API-gateway

Guide för en första lokal integration i Hälsinge Live. Keycloak och PostgreSQL
körs i Docker. EventCatalog körs med `dotnet run` och admin-webben med Vite.

Admin-webben loggar in via Keycloak och skickar en access-token till EventCatalog.
API:t kontrollerar token och kräver admin-behörighet för anrop som ändrar information.

## 1. Starta Keycloak

Lägg till följande under `services:` i `src/services/Compose.yaml`:

```yaml
  keycloak:
    image: quay.io/keycloak/keycloak:26.8.1
    command: start-dev
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: keycloak-admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: HalsingeLive
    ports:
      - "127.0.0.1:8180:8080"
    volumes:
      - keycloak-data:/opt/keycloak/data
```

Lägg till volymen under det befintliga `volumes:` längst ner i filen:

```yaml
  keycloak-data:
```

Detta använder Keycloaks inbyggda utvecklingsdatabas med sparad volym.
`start-dev` och lösenordet ovan är bara för lokal utveckling. I produktion ska
HTTPS användas och hemligheter hållas utanför repot, exempelvis i miljövariabler.
Bootstrap-inställningarna ändrar inte lösenordet på ett redan befintligt konto.

Kör från projektets rot:

```bash
docker compose -f src/services/Compose.yaml up -d keycloak eventcatalog-db
```

Öppna <http://localhost:8180/admin> och logga in med:

- Användarnamn: `keycloak-admin`
- Lösenord: `HalsingeLive`

Detta konto administrerar Keycloak. Det är separat från administratören som
hanterar lokaler och evenemang i biljettplattformen.

## 2. Konfigurera realm, klient och användare

I Keycloaks admin-konsol:

1. Skapa ett realm med namnet `halsingelive` och välj det.
2. Skapa realm-rollen `admin`.
3. Skapa användarna `testadmin` och `testuser`.
4. Sätt egna testlösenord under **Credentials**, med **Temporary = Off**.
5. Ge endast `testadmin` realm-rollen `admin` under **Role mapping**.

Skapa en OpenID Connect-klient:

| Inställning | Värde |
| --- | --- |
| Client ID | `admin-web` |
| Client authentication | Off |
| Standard flow | On |
| Direct access grants | Off |
| Valid redirect URIs | `http://localhost:5173/*` |
| Valid post logout redirect URIs | `http://localhost:5173/*` |
| Web origins | `http://localhost:5173` |
| PKCE Code Challenge Method | `S256` |

Webbklienten är publik och använder Authorization Code med PKCE. Ingen
klienthemlighet ska finnas i frontend-koden.

Under **Clients → admin-web → Client scopes → admin-web-dedicated → Mappers**,
lägg till två mappers via **Configure a new mapper**:

| Mapper | Inställningar |
| --- | --- |
| Audience | Name: `eventcatalog-audience`. Included Custom Audience: `eventcatalog`. Add to access token: On. |
| User Realm Role | Name: `app-roles`. Token Claim Name: `roles`. Multivalued: On. Claim JSON Type: `String`. Add to access token: On. |

Lämna standard-scope `roles` kvar. Den extra rollmappen gör rollerna enkla att läsa
i ASP.NET Core. Den vanliga realm-rollmappen används av webbklientens
`hasRealmRole('admin')`.

## 3. Lägg till JWT-autentisering i EventCatalog

Installera paketet från projektets rot:

```bash
dotnet add src/services/EventCatalog/EventCatalog.csproj \
  package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.12
```

Lägg till överst i `src/services/EventCatalog/program.cs`:

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
```

Lägg följande före `var app = builder.Build();`, tillsammans med övriga
registreringar. Behåll befintliga registreringar för databas, controllers och handlers.

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8180/realms/halsingelive";
        options.Audience = "eventcatalog";

        // HTTP tillåts bara för den lokala utvecklingsmiljön.
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("admin");
    });
});
```

Placera autentisering och behörighetskontroll före befintliga `app.MapControllers()`:

```csharp
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
```

API:t kontrollerar tokenens signatur, utfärdare, audience och giltighetstid.
Policyn kräver därefter rollen `admin`.

## 4. Skydda metoderna som ändrar information

Lägg till i `VenuesController.cs` och `EventsController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
```

Sätt följande attribut på varje metod som skapar, uppdaterar eller raderar:

```csharp
[Authorize(Policy = "AdminOnly")]
```

Exempel på placering, inte en ersättning för hela metoden:

```csharp
[Authorize(Policy = "AdminOnly")]
[HttpPost]
public async Task<IActionResult> CreateVenue(
    [FromBody] CreateVenueRequest request,
    CancellationToken cancellationToken)
{
    // Behåll metodens befintliga kod.
}
```

Skydda även sektioner och uppladdningar som ändrar information. Listning och
hämtning kan fortsätta vara publika. Handlers och modeller behöver inte hantera
inloggningen: behörighetskontrollen sker innan controller-metoden körs.

## 5. Lägg till Keycloak i admin-webben

Kör från projektets rot:

```bash
cd src/frontend/admin-web
npm install keycloak-js@26.2.4
```

Skapa `src/frontend/admin-web/src/auth/keycloak.ts`:

```typescript
import Keycloak from 'keycloak-js';

const keycloak = new Keycloak({
  url: 'http://localhost:8180',
  realm: 'halsingelive',
  clientId: 'admin-web',
});

export default keycloak;
```

Ersätt innehållet i `src/frontend/admin-web/src/main.tsx` med:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router/dom';
import { CssBaseline } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { router } from './router';
import keycloak from './auth/keycloak';

const queryClient = new QueryClient();
const root = createRoot(document.getElementById('root')!);

async function start() {
  try {
    const authenticated = await keycloak.init({
      onLoad: 'check-sso',
      pkceMethod: 'S256',
      checkLoginIframe: false,
    });

    if (!authenticated) {
      root.render(
        <main>
          <h1>Hälsinge Live – Admin</h1>
          <button onClick={() => void keycloak.login()}>
            Logga in
          </button>
        </main>,
      );
      return;
    }

    if (!keycloak.hasRealmRole('admin')) {
      root.render(
        <main>
          <p>Du saknar administratörsbehörighet.</p>
          <button onClick={() => void keycloak.logout({
            redirectUri: window.location.origin,
          })}>
            Logga ut
          </button>
        </main>,
      );
      return;
    }

    root.render(
      <StrictMode>
        <CssBaseline />
        <QueryClientProvider client={queryClient}>
          <RouterProvider router={router} />
        </QueryClientProvider>
      </StrictMode>,
    );
  } catch {
    root.render(
      <p>Inloggningstjänsten kunde inte nås. Ladda om och försök igen.</p>,
    );
  }
}

void start();
```

Keycloak initieras en gång innan React-vyerna startas.

## 6. Skicka token från fetch-hjälparen

I `src/frontend/admin-web/src/api/http.ts`, lägg till:

```typescript
import keycloak from '../auth/keycloak';
```

Ersätt bara funktionen `request` med:

```typescript
export async function request<T = void>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  if (!keycloak.authenticated) {
    throw new ApiError(401, 'Du behöver logga in.');
  }

  try {
    await keycloak.updateToken(30);
  } catch {
    keycloak.clearToken();
    throw new ApiError(401, 'Din session har gått ut. Logga in igen.');
  }

  if (!keycloak.token) {
    throw new ApiError(401, 'Ingen giltig inloggning finns.');
  }

  const headers = new Headers(init.headers);
  headers.set('Authorization', `Bearer ${keycloak.token}`);

  const res = await fetch(`${BASE_URL}${path}`, {
    ...init,
    headers,
  });

  if (!res.ok) {
    throw new ApiError(res.status, await readErrorMessage(res));
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}
```

Behåll `BASE_URL`, `ApiError`, `readErrorMessage` och `sendJson`.
Token hålls i minnet och förnyas före anrop. Spara inte token i localStorage.
Om en uppladdning använder `fetch` direkt behöver den också skicka token.

## 7. Lägg till utloggning

I `src/frontend/admin-web/src/layouts/AdminLayout.tsx`, lägg till importerna
nedan. Om `Button` redan importeras från MUI behöver den inte importeras igen.

```typescript
import keycloak from '../auth/keycloak';
import { Button } from '@mui/material';
import { useQueryClient } from '@tanstack/react-query';
```

Inuti `AdminLayout`, före `return`:

```tsx
const queryClient = useQueryClient();

async function logout() {
  await queryClient.cancelQueries();
  queryClient.clear();

  await keycloak.logout({
    redirectUri: window.location.origin,
  });
}
```

Lägg knappen i befintlig `Toolbar`:

```tsx
<Button
  color="inherit"
  sx={{ ml: 'auto' }}
  onClick={() => void logout()}
>
  Logga ut
</Button>
```

## 8. Starta och testa

Starta Docker Desktop och kör steg 1 om Keycloak och databasen inte redan körs.
Applicera EventCatalogs vanliga databas-migrationer enligt projektets README vid behov.

Kör API:t från projektets rot i en terminal:

```bash
dotnet run \
  --project src/services/EventCatalog/EventCatalog.csproj \
  --launch-profile EventCatalog
```

Kör webben i en annan terminal, från projektets rot:

```bash
cd src/frontend/admin-web

CATALOG_API_PROXY_TARGET=http://localhost:5080 \
npm run dev -- --port 5173 --strictPort
```

Den befintliga Vite-proxyn skickar `/api`-anrop vidare till EventCatalog på port
5080 och tar bort prefixet `/api`. Ingen extra CORS-konfiguration behövs för
dessa proxade anrop.

### Adresser i webbläsaren

| Vad | Adress | Inloggning |
| --- | --- | --- |
| Admin-webben | <http://localhost:5173> | `testadmin` eller `testuser`, med lösenorden du valde |
| Keycloaks admin-konsol | <http://localhost:8180/admin> | `keycloak-admin` / `HalsingeLive` |
| Swagger | <http://localhost:5080/swagger> | Skyddade anrop kräver token även här |

Börja med admin-webben på port 5173. Klicka **Logga in**; Keycloak öppnar sin
inloggningssida och skickar dig tillbaka till admin-webben efter inloggning.
Att logga in i Keycloaks admin-konsol ger inte automatiskt admin-behörighet i appen.

### Testfall

| Test | Förväntat resultat |
| --- | --- |
| Öppna admin-webben oinloggad | Inloggningsknappen visas |
| Ange fel lösenord | Keycloaks inloggningssida visar ett fel |
| Logga in som `testadmin` | Admin-webben visas |
| Skapa en giltig lokal | 201 och lokalen sparas |
| Klicka Logga ut | Inloggningsknappen visas igen |
| Logga in som `testuser` | Meddelande om saknad admin-behörighet |

Testa API-skyddet utan frontend från terminalen:

```bash
curl -i -X POST http://localhost:5080/CreateVenue \
  -H "Content-Type: application/json" \
  -d '{"name":"Testlokal","address":"Testgatan 1","capacity":100}'
```

Svaret ska vara **401**, eftersom ingen token skickas.

För att verifiera **403**, logga in som `testuser` på admin-webben, öppna
webbläsarens utvecklarkonsol och kör följande. Detta är en lokal Vite-testmetod:

```javascript
const { default: keycloak } =
  await import('/src/auth/keycloak.ts');

const response = await fetch('/api/CreateVenue', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${keycloak.token}`,
  },
  body: JSON.stringify({
    name: 'Testlokal',
    address: 'Testgatan 1',
    capacity: 100,
  }),
});

console.log(response.status);
```

Resultatet ska vara **403**, och ingen lokal ska skapas.

Upprepa motsvarande kontroller för skyddade update/delete-anrop på lokaler,
sektioner och evenemang.

### Vanliga felsökningspunkter

- **401 trots inloggning:** kontrollera tokenens issuer och att audience innehåller
  `eventcatalog`. API:t ska nå Keycloak på port 8180.
- **403 som testadmin:** kontrollera realm-rollen `admin`, rollmappen `roles` och
  att användaren har loggat in igen efter ändrade rollinställningar.
- **Invalid redirect URI:** kontrollera att frontend körs exakt på
  `http://localhost:5173` och att redirect-inställningarna matchar.
- **Keycloaks admin-lösenord fungerar inte:** bootstrap-inställningen ersätter
  inte lösenordet om kontot redan finns i den sparade Keycloak-volymen.
- **Swagger ger 401:** inloggningen i admin-webben skickar inte automatiskt token
  från Swagger. OAuth/Bearer-stöd i Swagger är en separat konfiguration.

## När API:t senare körs i Docker

Guidens Keycloak-adress gäller API:t som körs lokalt med `dotnet run`.
Inuti EventCatalog-containern pekar `localhost` på själva API-containern.
Därför behöver discovery/backchannel-adresserna och issuer-konfigurationen
anpassas innan denna setup används med API:t i Docker.

Admin-webb och kundwebb kan köras i separata containrar senare. De behöver inte
egna containrar för att testa denna lokala integration.

## Dokumentation

- [Keycloak i container](https://www.keycloak.org/server/containers)
- [Keycloak JavaScript-klient](https://www.keycloak.org/securing-apps/javascript-adapter)
- [Keycloak administrationsguide](https://www.keycloak.org/docs/latest/server_admin/index.html)
- [JWT-autentisering i ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)
