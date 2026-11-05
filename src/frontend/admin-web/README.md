# admin-web

Administrative React application for Hälsinge Live. The app uses Vite, TypeScript, React Router, MUI, and TanStack Query. It authenticates administrators with Keycloak and sends the resulting Bearer token with API requests.

## Run locally

From this directory, install dependencies and start Vite:

```bash
npm install
npm run dev
```

Vite uses port `5173` by default and may choose another port if it is occupied. The local Keycloak client is configured for `http://localhost:8180`. To run Keycloak, APIs, and both web apps with Docker, follow [the repository's local development guide](../../../docs/README.MD#frontend-appar-utveckla-frontend-med-docker-compose).

Available scripts:

```bash
npm run build
npm run lint
npm run preview
```

## API proxy

During local development, Vite forwards `/api`, `/booking-api`, `/ticket-api` and `/payment-api` to ApiGateway at `http://localhost:5212`, preserving the complete path. YARP selects the backend service and removes the prefix.

Start the gateway from the repository root, along with the backend services:

```bash
dotnet run --project src/ApiGateway --launch-profile http
```

To use another gateway address, set `API_GATEWAY_PROXY_TARGET` in this app's `.env.local`, then restart Vite:

```dotenv
API_GATEWAY_PROXY_TARGET=http://localhost:5212
```

The previous service-specific `*_API_PROXY_TARGET` variables are no longer used. When Vite runs in Docker, set `API_GATEWAY_PROXY_TARGET` to a gateway address reachable from that container. The gateway must also use backend addresses reachable from its own environment.

The Vite proxy only applies to the development server. Production hosting must route these API prefixes to ApiGateway separately.

## Code layout and API contracts

- `src/app/` contains application setup, authentication, routing, and layout.
- `src/features/` groups administrative capabilities by domain, including events, venues, bookings, check-in, seating maps, payment simulation, and seed operations.
- `src/shared/` contains admin-app utilities, including its authenticated API transport helper.

Backend services own their API contracts. The frontend API modules and types under each feature describe the response or request shapes that app consumes. See [frontend architecture and contract conventions](../../../docs/frontend-architecture.md) for service ownership and guidance on shared or generated types.
