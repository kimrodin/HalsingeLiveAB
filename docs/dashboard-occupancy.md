# Dashboard occupancy

The dashboard shares one period for sales and occupancy (last 30 days by default).
Sales use payment dates; occupancy uses event start dates in Europe/Stockholm,
including both selected calendar days and daylight-saving transitions.

`OccupancySection` shows one bar per event. Categories use event IDs; labels show
names and Swedish start dates and times. Tooltips include sold places, capacity
and venue. The report refreshes every minute, supports cancellation and retries,
and retains the latest values with a warning if a refresh fails.

Booking exposes two admin-only endpoints with optional `from` and `to` dates:

- `/GetEventOccupancy`: eventId, eventName, eventStartsAt, venueId, venueName,
  capacity, sold and occupancyPercent.
- `/GetVenueOccupancy`: retains its venue-level contract (venueId, venueName,
  capacity, sold and occupancyPercent). Percentages use summed sold/capacity,
  rather than averaging event percentages.

Booking fetches catalog data for inventory event IDs in batches of 500. The
catalog's inclusive UTC-day filters restrict results to days touching the Swedish
interval; Booking then applies exact Swedish calendar-date filtering. This avoids
fetching the entire event history while retaining correct midnight and DST boundaries. Deleted events are excluded. Sold counts retain the
existing inventory calculation: total quantity minus available quantity.

The occupancy branch was integrated selectively. Its tracked `env` file and
container-start `npm ci` command were excluded. Existing local changes were kept.
No merge commit was created. `env` and `.env` are ignored; `.env.example` contains
no key. Copy the template to `src/.env` for Compose and generate a local key.
Any previously committed key still exists in the source branch/history and must
be rotated wherever it is in use; excluding the file does not revoke that key.

Validation retains one added Booking test for separate same-name event rows and
Swedish date boundaries on the autumn DST transition day. Existing venue-total,
deleted-event, cancellation and timeout tests are kept.
