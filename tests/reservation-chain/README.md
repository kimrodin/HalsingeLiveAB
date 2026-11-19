# Real reservation proxy chain

Run from the repository root with Docker Desktop (Linux containers) and PowerShell:

```powershell
.\tests\reservation-chain\run.ps1
```

The suite starts a uniquely named disposable Compose stack, migrates the real
Booking database and inserts five synthetic inventories with 100 tickets each.
It uses both production Nginx templates, the production gateway and Booking
entry points, PostgreSQL, Redis and RabbitMQ. Only EventCatalog's read responses
are synthetic; payments and ticket issuance are not initiated.

Four client containers have distinct socket source IPs. Tests send forged client
headers, a query string, referrer and user agent containing sentinel IP addresses.
They verify:

- Three attempts in 60 seconds, a fourth returning 429 and Retry-After, another
  real source IP succeeding, and recovery after the actual Redis window expires.
- Active reservation and ticket quotas across both frontend proxies.
- Unchanged stock, active quota and original deadlines on rejected calls.
- Exactly nine PostgreSQL bookings, correct quantities and unchanged persistent
  inventory (temporary holds affect Redis only).
- Twelve JSON access records containing only method, path, status, bytes and
  duration; no client/forwarding address, query, referrer or user agent fields.

`results/` is ignored by Git. Every run overwrites its configuration, source
hashes, scenario results, before/after database snapshots, access records and
outcome. Guest access tokens are never included in scenario reports. Service
logs remain local diagnostics and can contain error information. The stack has
no host ports, persistent volumes or application `.env` dependency; its explicit
credentials and HMAC key are public fixture values. Containers are removed even
on failure. It uses subnet `172.30.153.0/24`; avoid concurrent runs of this suite.

The separate `Booking.IntegrationTests` cover lifecycle transitions, two-instance
concurrency and Redis failures with an existing reservation. This suite completes
the production proxy chain check rather than duplicating every domain scenario.
