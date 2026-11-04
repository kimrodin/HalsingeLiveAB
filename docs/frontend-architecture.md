# Frontend architecture and API contract conventions

The repository has two independent frontend applications: `src/frontend/customer-web` and `src/frontend/admin-web`. They use separate API helpers because they have different access requirements. The customer app makes public requests and uses a guest access token for booking-specific actions. The admin app authenticates with Keycloak and sends a Bearer token. Keep those transport and authentication boundaries app-local.

## API ownership

Backend services own the request and response contracts they expose. Frontend API modules should stay with the feature that uses them and map backend DTOs into app-facing models where the app needs a different shape.

| Service | Contract area | Frontend use |
| --- | --- | --- |
| EventCatalog | Events, venues, venue seating maps, and event images | Both apps read catalog data; admin-web also creates, edits, uploads, and deletes catalog data. |
| Booking | Bookings, guest booking access, inventory, admin booking details, and the authenticated admin ticket check-in/cancel proxy | customer-web creates and views guest bookings; admin-web manages bookings and calls the admin ticket actions. |
| TicketService | Ticket records and ticket PDF endpoints | admin-web reads admin booking tickets and PDFs directly; Booking calls TicketService for guest-authorized ticket reads and PDFs. |
| PaymentSimulator | Payments and admin payment-simulation settings | admin-web manages the simulator settings. |

The URL prefixes are routing conveniences, not ownership boundaries. Vite and the Nginx frontend containers proxy `/api` to EventCatalog, `/booking-api` to Booking, `/ticket-api` to TicketService, and (for admin-web) `/payment-api` to PaymentSimulator. The proxy strips the frontend prefix before forwarding.

## Type sharing and generation

Keep request and response types next to the feature API module that consumes them while they are app-specific, serve different views, or are mapped from backend DTOs. Matching names alone do not make two types the same contract. For example, the customer and admin `BookingDetails` models have different shapes and access semantics, so they remain separate.

When the same backend contract is independently declared in both apps and repeated changes cause drift, first confirm the backend remains its source of truth. Then consider generating client types from an API schema or introducing a small, versioned contract package. Share only the contract types that are actually identical; keep each app's transport, authentication, and UI models separate.

At the time of writing, both apps declare the same `VenueSeatingMap` fields. This is one known duplicate and can be consolidated if it changes independently or begins recurring; it does not by itself justify sharing the API helpers or application code. When changing a backend contract, update its owning service and all consuming app API modules in the same change, and review any mappings from DTOs to UI models.
