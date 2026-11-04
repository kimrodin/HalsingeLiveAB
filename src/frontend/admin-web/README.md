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

During local development, Vite proxies requests to the backend services. The defaults target host ports; override them with environment variables when the services run elsewhere.

| Prefix | Service | Default target | Environment variable |
| --- | --- | --- | --- |
| `/api` | EventCatalog | `http://localhost:8080` | `CATALOG_API_PROXY_TARGET` |
| `/booking-api` | Booking | `http://localhost:8081` | `BOOKING_API_PROXY_TARGET` |
| `/ticket-api` | TicketService | `http://localhost:8082` | `TICKET_API_PROXY_TARGET` |
| `/payment-api` | PaymentSimulator | `http://localhost:5111` | `PAYMENT_API_PROXY_TARGET` |

The proxy removes the prefix before forwarding the request. In Docker Compose development, the targets are set to the corresponding Compose service names.

## Code layout and API contracts

- `src/app/` contains application setup, authentication, routing, and layout.
- `src/features/` groups administrative capabilities by domain, including events, venues, bookings, check-in, seating maps, payment simulation, and seed operations.
- `src/shared/` contains admin-app utilities, including its authenticated API transport helper.

Backend services own their API contracts. The frontend API modules and types under each feature describe the response or request shapes that app consumes. See [frontend architecture and contract conventions](../../../docs/frontend-architecture.md) for service ownership and guidance on shared or generated types.
