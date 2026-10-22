# customer-web

Customer-facing React application for Hälsinge Live. Built with Vite, TypeScript, MUI, React Router, and TanStack Query.

## Structure

- `src/app/` — application setup, routing, and shared page layout
- `src/features/` — customer capabilities grouped by domain (events, bookings, account)
- `src/shared/` — reusable API, UI, and utility code shared across features

During local development, Vite proxies `/api/*` to EventCatalog. Set `CATALOG_API_PROXY_TARGET` to change the API host; the default is `http://localhost:8080`.

Install dependencies with `npm install`, then run `npm run dev`.
