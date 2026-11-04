# Seed events and purchased tickets

The admin sidebar's **Skapa seeddata** button runs two steps in development:

1. EventCatalog `POST /seed` creates/reuses the three venues, their sections and
   forty events, then provisions their Booking inventory.
2. Booking `POST /SeedPurchases` creates/reuses paid, confirmed bookings for those
   exact seed event IDs. It ensures matching successful PaymentSimulator payments
   using `POST /api/payments/seed`, and queues the usual ticket-issuance outbox.

Both new endpoints require an admin token and Development. Booking forwards the
admin token to PaymentSimulator. No real payment provider is called. TicketService
uses the existing internal API key and regular idempotent issuance workflow.

The original ten 2027 events retain their IDs and definitions. Thirty additional
events use ordinals 11–40: two per month throughout 2026, plus six October concerts.
No new venues are needed. Seeded inventory uses the catalog's section capacities.

Purchases fill approximately 35–84% of each section (at least eight places where
capacity permits). Each purchase contains at most eight tickets. Purchase dates
are deterministically spread over eight days in the six weeks before the event,
with varying times. Future seed events also have future synthetic payment dates.
The data supports sales, revenue, occupancy, booking lists and ticket PDFs.

Event IDs use `10000000-…`, bookings `51000000-…`, booking items `52000000-…`,
outbox messages `53000000-…`, and payments `54000000-…`. The event, section and
purchase ordinal form stable IDs. Repeated or concurrent seed requests reuse
existing rows. Changed bookings/payments cause a conflict instead of being reset.

The seed tools are development utilities. Run creation and deletion one at a time,
without customer bookings or active reservations on the seed events. They do not
coordinate concurrent customer traffic across EventCatalog and Booking.

Each event is created in a regular database transaction. Paid bookings and ticket
outbox messages are saved together; matching simulated payments are idempotent.
Redis inventory is rebuilt from committed PostgreSQL quantities afterward, instead
of creating temporary reservations and compensating them. Rerunning creation does
not add purchases or deduct stock again, and requeues existing ticket messages to
repair issuance after an interrupted operation.

**Ta bort seeddata** first calls Booking `DELETE /SeedPurchases`, then removes
catalog events, sections and venues. Booking restores stock and removes owned
bookings, items and queue messages in one database transaction, after idempotent
payment and ticket deletion. If a dependency fails, the database remains unchanged
and deletion can be retried. Once committed, Redis is reloaded from the database.
The same cache reload also runs when deletion is retried with no bookings left.

Only the deterministic `51000000-…` purchase namespace is removed. Existing ordinary
bookings on seed events block deletion; unrelated ordinary bookings and payments
are retained. Older interrupted `DeletingSeed` rows can still be removed without
restoring their stock twice; new deletions do not create this intermediate state.

A short transaction advisory lock remains around seed operations and seed ticket
dispatch because the ticket worker runs automatically. The worker reloads queued
messages under that lock, so a stale batch cannot recreate deleted seed tickets.
Seed tickets use synchronous HTTP issuance while this lock is held; ordinary customer
tickets use RabbitMQ. A broker confirmation alone would leave seed messages able to
recreate tickets after deletion. Historical seed payments are created through the
seed HTTP API as already settled payments and do not enter the payment queues.
No Redis restoration ledger or session advisory lock is needed.

After updating, rebuild EventCatalog, Booking, PaymentSimulator and TicketService before using the
button. No database schema migration is needed for this feature.

Validation keeps two focused seed integration tests: one for catalog month coverage
and stable IDs, and one for the purchase lifecycle (creation, repeat creation,
deletion, ordinary-record preservation and reseeding with restored stock). Payment
seed creation/deletion is covered by a separate PaymentSimulator integration test.
