# Sales statistics for the admin dashboard

`GET /GetSalesStatistics?from=2026-10-01&to=2026-10-31` on Booking requires the
existing `AdminOnly` policy. Through admin-web use `/booking-api/GetSalesStatistics`.
The typed client is `features/dashboard/api/salesStatistics.ts`. The dashboard
renders the net-revenue and sold-ticket cards, comparisons and daily sales chart
from this endpoint. The existing active-event card and upcoming-event queries
retain their behavior.

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
next refresh. Admin cards and the API client contract remain unchanged.

## Admin frontend

`DashboardPage` owns the applied `DashboardPeriod` (`from`, `to`). The default is
30 Swedish calendar days. `DashboardPeriodFilter` keeps only an unsubmitted draft
and calls the page's setter when **Visa period** or **Senaste 30 dagarna** is used.
It remounts with the applied dates when the shared period changes.

The page passes `period` to `SalesStatisticsSection` via props. Future dashboard
components, such as venue occupancy, can receive the same prop without their own
period state or a context provider. Sales uses payment dates; occupancy uses event
dates, so each component must explain that distinction. Active events and upcoming
events retain their existing independent queries.

`SalesStatisticsSection` fetches totals and daily values for its `period` prop.
Each range has its own query cache entry. Superseded requests can be aborted and
the active range is refreshed every minute. `SalesMetricCard` formats totals and
changes; when the preceding value is zero, it shows that value instead of a
fabricated percentage.

`TicketSalesChart` is a simple SVG line chart with an accessible summary. There
is no day-selection state, slider or daily-values table. The 7-day and current-month
shortcuts have been removed; custom dates and the last-30-days shortcut remain.
Empty, loading and API-error states are still handled. No chart dependency is
required. Backend calculations and refund rules are unchanged by this frontend
simplification.

Frontend checks from `src/frontend/admin-web`:

```sh
npm run lint
npm run build
node --experimental-strip-types --test tests/dashboardPeriod.test.mjs
```

Date tests cover Swedish midnight, DST changes, leap days, invalid dates and the
366-day limit. Browser QA uses temporary simulated API responses; it does not
replace authenticated end-to-end backend verification.
