# Sales statistics API and service integration

`GET /GetSalesStatistics?from=2026-10-01&to=2026-10-31` on Booking requires the
existing `AdminOnly` policy. Through admin-web use `/booking-api/GetSalesStatistics`.
Dashboard behavior and frontend implementation are documented in
[Admin-dashboarden](admin_dashboard.md). This document covers API contracts,
calculation rules, service integration and deployment.

## Meaning of the numbers

- Dates are inclusive Swedish calendar dates, `Europe/Stockholm`. Actual UTC
  boundaries vary across daylight-saving transitions. The default is today and
  the preceding 29 days; maximum requested range is 366 days.
- Only confirmed bookings with matching successful payments contribute. Temporary
  reservations, cancelled/expired bookings and unsuccessful payments are excluded.
- `PaidAt` determines the sale date, never booking creation time or event time.
- Only SEK is supported, matching the existing booking/payment contract. Amounts
  are the paid booking totals; no VAT breakdown or currency conversion is inferred.
- `grossRevenue` includes successful payments for those bookings. Full refunds
  reduce `netRevenue` on the **original sale date**, even if refunded later.
  Historical results therefore change after refunds, as requested.
- `soldTickets` is the net ticket quantity for non-refunded sales;
  `refundedTickets` records the removed quantity. There is no partial-refund model.
- `daily` includes every day, with zero values where applicable. Its totals equal
  `summary`. `previousPeriod` covers an immediately preceding equally long range.
- Percentage changes are `null` when the preceding value is zero (undefined),
  rather than a fabricated 100% increase.

Example response shape:

```json
{
  "from": "2026-10-01",
  "to": "2026-10-31",
  "currency": "SEK",
  "timeZone": "Europe/Stockholm",
  "summary": { "grossRevenue": 450, "refundedRevenue": 200, "netRevenue": 250, "soldTickets": 2, "refundedTickets": 3 },
  "previousPeriod": { "from": "2026-08-31", "to": "2026-09-30", "summary": { "grossRevenue": 0, "refundedRevenue": 0, "netRevenue": 0, "soldTickets": 0, "refundedTickets": 0 } },
  "changes": { "netRevenuePercent": null, "soldTicketsPercent": null },
  "daily": []
}
```

`daily` is abbreviated above; the real response contains 31 entries.

## Service integration and limits

PaymentSimulator provides `GET /api/payments/sales` with `from` (inclusive UTC),
`toExclusive` (exclusive UTC) and cursor parameters `afterPaidAt` /
`afterPaymentId`. Both statistics endpoints require `AdminOnly`. Booking forwards
its incoming bearer token per request; PaymentSimulator validates the same issuer,
audience and admin role. Existing payment processing endpoints keep their access
behavior. Authentication authority/metadata settings are configured in Compose.

PaymentSimulator applies the date predicate in PostgreSQL using a filtered
`(PaidAt, Id)` index. It returns up to 500 rows per page, ordered by payment date
and ID. Booking reads only those booking IDs and retains confirmed bookings;
unconfirmed or deleted bookings do not contribute. There is no scan or remote
lookup of the entire historical booking list. Errors in older payments outside
the selected and comparison periods cannot block the report.

The payment date is the source of range membership. Payments without `PaidAt`
do not belong to a dated report; the existing PaymentPaidAt migration backfills
legacy successful payments from CompletedAt. Invalid returned pages (including
missing dates, duplicates, an out-of-range row or a non-advancing cursor) fail
with 502, as do inconsistent payments for confirmed bookings in the report range.
Payment service timeouts return 502. Caller cancellation is propagated. Invalid
calendar ranges return 400.

`PaymentSalesIndex` adds the index without changing existing data. Deploy/migrate
PaymentSimulator before the updated Booking service. Local PaymentSimulator
applies migrations at startup as before. No Booking migration is required.

The cursor uses immutable payment dates and an ID tie-breaker. Refunds are read
at request time and attributed to the original sale date; there is no distributed
snapshot. A payment or refund committed during a report can become visible on the
next request.

## Sales notification hub

Booking exposes an authenticated SignalR hub at `/hubs/sales`. Admin-web connects
through API Gateway at `/booking-api/hubs/sales`. The hub requires the existing
`AdminOnly` policy and uses the admin's Keycloak access token. For browser
WebSocket connections, Booking accepts the SignalR `access_token` query parameter
only on this hub path. Nginx disables access logging for the hub requests, and
Gateway/Booking suppress detailed request and proxy logs that could include the
query string.

After Booking processes a successful sale or refund, it broadcasts the event
`SalesUpdated` to connected admin clients. The event has no arguments and contains
no booking, payment, ticket, customer, or sales data. Clients use it as an
invalidation signal and retrieve data from the authenticated
`GetSalesStatistics` endpoint, which remains the source of truth.

The admin-web Nginx proxy forwards WebSocket upgrade headers for
`/booking-api/`; API Gateway routes that path to Booking. Client reconnection,
polling and cache behavior are described in [Admin-dashboarden](admin_dashboard.md).

The current deployment runs Booking as a single service instance. If Booking is
scaled to multiple instances, configure a SignalR backplane or managed SignalR
service so a notification from one instance can reach admin clients connected to
another instance.
