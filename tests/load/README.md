# First load test

Requires Docker Desktop running with Linux containers. Run from the repository root;
no k6 installation, `.env` changes, or admin login is needed.

1. Run the small test:

   ```powershell
   .\tests\load\run.ps1
   ```

2. Watch the three printed steps: start a fresh stack, run the test, then clean up.
   The first run downloads k6 and builds the services, so it takes longer.

3. Read the terminal summary or open `tests/load/results/<run>/summary.json`.
   Try a little more traffic with:

   ```powershell
   .\tests\load\run.ps1 -Vus 5 -Iterations 25
   ```

The default is **10 purchases across 2 virtual users**, plus one warm-up purchase.
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

`checkout.js` contains the three HTTP steps. `fixture.sql` creates one event and
1,000 tickets directly in the disposable databases after the real migrations run;
all measured purchases use the normal APIs, Redis, outboxes, RabbitMQ, and consumers.
The event is always one year in the future. Payments always succeed after a planned
500 ms delay; worker intervals add to the observed checkout time.

Every run gets a unique Compose project with fresh PostgreSQL, Redis, and RabbitMQ.
Only RabbitMQ's management UI is published, on a free port bound to `127.0.0.1`;
no development volumes are mounted. The script removes
only that run's containers and volumes, even if the test fails. Results stay on disk.

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
shares one PostgreSQL container across four databases and uses memory-backed data;
it is a learning baseline, not a production capacity measurement. It exercises the
guest checkout through the gateway; Keycloak, browser rendering, and PDF generation
are outside this first test.

References: [k6 shared iterations](https://grafana.com/docs/k6/latest/using-k6/scenarios/executors/shared-iterations/)
and [thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/).
