# Checkout and ticket-release load tests

Requires Docker Desktop running with Linux containers. Run from the repository root;
no k6 installation, `.env` changes, or admin login is needed.

Recorded validation: [local release and regression results, 2026-11-09](validation-2026-11-09.md).

1. Run the small test:

   ```powershell
   .\tests\load\run.ps1
   ```

2. Watch the four printed steps: start a fresh stack, run the test, verify, then clean up.
   The first run downloads k6 and builds the services, so it takes longer.

3. Read the terminal summary or open `tests/load/results/<run>/summary.json`.
   Try a little more traffic with:

   ```powershell
   .\tests\load\run.ps1 -Vus 5 -Iterations 25
   ```

The default `Checkout` scenario is **10 purchases across 2 virtual users**, plus one warm-up purchase.
Each purchase reserves one ticket, starts payment, and polls every 500 ms until
the booking is confirmed and exactly one valid ticket exists. A journey times out
after 30 seconds of polling. All purchases must succeed; interrupted or missing
iterations also fail the test. Initial thresholds check correctness; this first
version measures latency without setting an arbitrary latency target.

Look at `checkout_succeeded` (must be 100%), `completed_checkouts` (must equal the
requested purchases), and `checkout_duration` (time from reservation to tickets,
including polling). HTTP timings alone do not measure the asynchronous checkout.
Warm-up HTTP calls appear in built-in HTTP metrics; custom checkout metrics include
only the measured purchases. `run.json` records the commit and workload, and
`images.json` records the service images used. Runs also save `services.log`.
`outcome.json` is the overall verdict: both k6 and the final verification must pass.
`verification.json` contains the last database, Redis, and queue snapshots and any
failed consistency checks. A successful k6 summary alone is not an overall pass.

`checkout.js` contains the three HTTP steps. `fixture.sql` creates a measured event
with configurable capacity (1,000 by default for Checkout) and a separate warm-up event
directly in the disposable databases after the real migrations run;
all measured purchases use the normal APIs, Redis, outboxes, RabbitMQ, and consumers.
The event is always one year in the future. By default, payments succeed after a
planned 500 ms delay; worker intervals add to the observed checkout time.

Every run gets a unique Compose project with fresh PostgreSQL, Redis, and RabbitMQ.
Their data uses isolated disk-backed volumes; Redis has AOF enabled. Cleanup removes
these volumes. This differs from the older memory-backed baseline, so its timings
must not be compared directly with these runs.
Only RabbitMQ's management UI is published, on a free port bound to `127.0.0.1`;
no development volumes are mounted. The script removes
only that run's containers and volumes, even if the test fails. Results stay on disk.

## Ticket-release scenario

```powershell
.\tests\load\run.ps1 -Scenario Release
```

The defaults deliberately oversubscribe **100 tickets with 1,000 purchase attempts**:

| Phase | Workload |
| --- | --- |
| First 30 seconds | Five browsing journeys/second, each reading the event list, event and inventory. |
| Next 20 seconds | 30 new purchase journeys/second, plus browsing. |
| Next 40 seconds | 10 new purchase journeys/second, plus browsing. |
| Settlement | Wait for bookings, payments, tickets, SQL/Redis stock, and queues to reconcile. |

Each purchase journey starts by trying to reserve 1–3 tickets. Of the offered
journeys, deterministic buckets assign 10% to abandon a reservation, 10% to cancel,
and 80% to pay after 0–3 seconds of thinking. Under contention, the accepted cohort
percentages can differ. All measured HTTP traffic uses the gateway's client-facing
paths; SQL is used only for fixture setup, simulation policy and final observation.
Warm-up purchases use a different event and always succeed before the selected
payment policy is applied. They are saved separately in `warmup.json`.

**Allow about 11–13 minutes for the default run after startup.** Abandoned holds
keep the application's real ten-minute lifetime. The verifier waits for expiry;
it does not shorten, delete, or manually release them. It checks at five-second
intervals and requires two consecutive passing observations. The default settlement
timeout is 720 seconds after k6 ends. Progress is printed every 30 seconds.

`burst` and `tail` use `constant-arrival-rate`: new journeys start independently
of previous journeys finishing. A journey contains multiple HTTP requests, so
journeys/second is not requests/second. Each phase preallocates 128 VUs (browsing
uses 16 more). Increase `-PreAllocatedVus` if needed. Any `dropped_iterations`, missing
purchase attempts, unexpected error, or interrupted journey fails the test.

A `CreateBooking` HTTP 409 counts as expected stock rejection only when its problem
title is `Not enough tickets are available.` Other conflicts still fail. Release
requires at least one stock rejection and at least one completed payment outcome;
it does not require every visitor to buy a ticket.

Look at these metrics separately:

- `release_reservation_duration`: HTTP duration of the reservation request, including stock rejections.
- `release_paid_duration`: reservation through thinking/payment/polling to valid tickets,
  only for completed purchases. Stock rejections do not dilute this distribution.
- `release_reserved`, `release_sold_out`, `release_abandoned`, `release_cancelled`,
  `release_paid`, `release_payment_rejected`: journey outcome counts.
- `release_unexpected_error`: must be zero; `release_attempts` must match the planned workload.

Final verification cross-checks the measured counters against database rows. It
rejects overselling, wrong/missing/duplicate tickets, orphan payments or tickets,
unexpected charges, nonterminal bookings, unreturned stock, active Redis holds,
pending outbox/inbox work, rejected inbox messages, missing queues and nonempty
queues (including dead-letter queues). Snapshots exclude the separate warm-up
event when reconciling measured capacity. Successful completion also requires
the k6 process to exit successfully.

For a shorter development check without abandoned holds:

```powershell
.\tests\load\run.ps1 -Scenario Release -Capacity 10 `
  -BurstRate 5 -BurstSeconds 4 -TailRate 2 -TailSeconds 4 `
  -PreSaleSeconds 2 -PreAllocatedVus 32 -AbandonPercent 0 `
  -CancelPercent 20 -MaxQuantity 2 -ThinkMaxSeconds 1
```

Exercise planned payment failure and timeout in separate runs:

```powershell
.\tests\load\run.ps1 -Scenario Release -PaymentOutcome Failed -AbandonPercent 0
.\tests\load\run.ps1 -Scenario Release -PaymentOutcome TimedOut -AbandonPercent 0
```

These policies apply to all paying customers in that run. The test expects cancelled
bookings, no tickets, and returned inventory for these customers. It fails if a
payment succeeds unexpectedly. Weighted payment mixtures and variable provider
latencies are not implemented; the existing `Random` mode's equal thirds are not
used. `-PaymentDelayMs` and `-PaymentTimeoutMs` configure the existing simulator.

## Sustained checkout traffic

A release may sell out quickly and then mainly exercise rejection responses.
Use `Sustained` to keep the complete payment/ticket path busy with ample inventory:

```powershell
.\tests\load\run.ps1 -Scenario Sustained -Capacity 100000 `
  -BurstRate 10 -BurstSeconds 60 -TailRate 10 -TailSeconds 240 `
  -AbandonPercent 0 -CancelPercent 0 -PreAllocatedVus 256
```

It uses the same journey logic but **fails on any stock rejection**. Its default
capacity is 100,000; other rate/timing/cohort defaults match Release. Rates and
customer distributions are test assumptions, not a demand forecast. Both scenarios
report latency without an arbitrary p95 target; agreed performance targets should
be added once the intended hardware and service objectives are defined.

## Compose commands and verification

`run.ps1` supplies a unique project name, fixture parameters, environment and results
mount. The underlying command sequence is shown below; placeholders denote the
values constructed by the runner, not commands to paste without preparing them:

```text
docker compose -p <run-project> -f tests/load/compose.yaml up -d --build --wait --wait-timeout 180
docker compose -p <run-project> -f tests/load/compose.yaml exec -T postgres psql -U loadtest -d loadtest -v ON_ERROR_STOP=1 -v capacity=<capacity> -f /fixture.sql
docker compose -p <run-project> -f tests/load/compose.yaml run --rm --no-deps --volume <absolute-results-path>:/results --env VUS=1 --env ITERATIONS=1 --env SKIP_SETUP_WARMUP=true --env INVENTORY_ID=74000000-0000-0000-0000-000000000002 k6 run --summary-export=/results/warmup.json /scripts/checkout.js
docker compose -p <run-project> -f tests/load/compose.yaml exec -T postgres psql -U loadtest -d payments -v ON_ERROR_STOP=1 -v payment_outcome=Succeeded -v payment_delay=500 -v payment_timeout=5000 -f /payment-policy.sql
docker compose -p <run-project> -f tests/load/compose.yaml run --rm --no-deps --volume <absolute-results-path>:/results <workload-env-arguments> k6 run --summary-export=/results/summary.json /scripts/release.js
docker compose -p <run-project> -f tests/load/compose.yaml down --volumes --remove-orphans
```

The runner also waits for the gateway before warm-up and runs `verify.ps1` before
cleanup. Do not stop after the k6 command when assessing the result. `-SkipBuild`
reuses existing local application images for development; omit it to test current
source. `run.json` records the working tree; `images.json` records image IDs.

Validation without a load run:

```powershell
docker compose -f tests/load/compose.yaml config --quiet
powershell -NoProfile -File tests/load/verify.tests.ps1
node --experimental-vm-modules --test tests/load/release.tests.cjs
```

## Watch RabbitMQ while the test runs

Run a slightly longer test to give yourself time to open the UI:

```powershell
.\tests\load\run.ps1 -Vus 5 -Iterations 100
```

After startup, the script prints **RabbitMQ UI: http://127.0.0.1:<port>/#/queues**.
Open that address and log in with username **loadtest** and password **loadtest**.
Each run uses a free port, so copy the URL printed for that run. The UI is removed
when the test finishes and cleanup runs.

On **Queues and Streams**, inspect:

- `payments.payment-requested.v1`: Booking to PaymentSimulator.
- `booking.payment-results.v1`: PaymentSimulator to Booking.
- `tickets.booking-confirmed.v1`: Booking to TicketService.

**Ready** counts messages waiting for delivery; **Unacked** counts delivered messages
awaiting acknowledgement. Watch publish, delivery, and acknowledgement rates as well:
messages may be consumed too quickly to see a backlog. In this project, PaymentSimulator
acknowledges requests after saving the attempt, then completes payments in the background,
so an empty request queue can still mean payments are in progress.
See the [RabbitMQ management guide](https://www.rabbitmq.com/docs/management).

## Database connections at higher loads

All four service databases share one PostgreSQL server in this small test stack.
The server permits 100 connections, and each service pool is capped at 20, allowing
at most 80 pooled connections in total. The remaining slots provide headroom for
administration and diagnostics. More virtual users can share these connections;
one virtual user does not require its own permanently open database connection.
The limits are recorded in `run.json`, and connections are labelled by service
using `Application Name`.

Without explicit pool limits, Npgsql permits up to 100 connections per pool.
Four services can then exhaust the shared server and produce PostgreSQL error
`53300: sorry, too many clients already`. Those defaults were unsuitable for this
shared test server. The regular application Compose stack uses separate database
servers; these pool limits apply only to the load-test stack. Higher loads can still
fail on latency or other limits; the test's success criteria remain the same.
See [PostgreSQL connection limits](https://www.postgresql.org/docs/18/runtime-config-connection.html)
and [Npgsql pooling settings](https://www.npgsql.org/doc/connection-string-parameters.html#pooling).

For comparable runs, use the same machine, Docker resources, image versions, and
background workload. k6 is pinned; infrastructure uses the application's major-version
image tags, so compare `images.json` if those tags are updated. This small stack
shares one PostgreSQL container across four databases. k6 shares the Docker host
with the services; CPU/RAM budgets, instance counts, production ingress/TLS and
network latency are not modeled. Before estimating production capacity, use the
intended database topology and resource limits, and run k6 on a separate machine.
The scenarios exercise guest checkout; Keycloak login, browser rendering, PDF
generation and real payment providers are outside these tests. Browsing precedes
the purchase phases, but the fixture event is already on sale: server-side
enforcement of `OnSaleFrom` is not implemented or verified by this test change.

References: [k6 shared iterations](https://grafana.com/docs/k6/latest/using-k6/scenarios/executors/shared-iterations/)
and [thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/).
