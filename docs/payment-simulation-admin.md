# Payment simulation admin API

PaymentSimulator exposes runtime settings for AdminWeb:

| Method | Path | Result |
| --- | --- | --- |
| GET | `/api/admin/payment-simulation` | Current effective settings |
| PUT | `/api/admin/payment-simulation` | Replace settings and return the saved values |

Both endpoints use the same Keycloak `AdminOnly` policy as the other admin APIs.
Send `Authorization: Bearer <access-token>` using the logged-in admin's Keycloak token.
Missing or invalid tokens return 401; authenticated users without the `admin` role return 403.
No internal API key is required.

In development AdminWeb, click **Betalningssimulator** beside the seed buttons in
the sidebar. The dialog loads current settings and lets an admin save an outcome,
delay, and timeout. It is hidden in production builds, like the seed controls.
The dialog explains that only new payment attempts are affected.

The Vite development proxy routes `/payment-api` to PaymentSimulator, retaining
the backend's `/api` prefix. Its default target is `http://localhost:5111`;
override with `PAYMENT_API_PROXY_TARGET` when needed. `Compose.dev.yaml` sets
`http://payment-simulator:8080` for the AdminWeb container. Restart Vite or recreate
the development container after changing proxy configuration. Production Nginx
does not expose this proxy.

Authentication uses audience `eventcatalog` and the top-level `roles` claim, matching
Booking and TicketService. The default authority is
`http://localhost:8180/realms/halsingelive`. Override it with `Authentication__Authority`;
`Authentication__MetadataAddress` can specify an internal discovery URL for containers.
Compose configures both URLs to match the other services. HTTPS metadata is required
outside Development. Existing payment endpoints remain unchanged.

Example PUT body (all three fields are required):

```json
{
  "outcome": "Failed",
  "delayMs": 500,
  "timeoutMs": 5000
}
```

`outcome` accepts `Succeeded`, `Failed`, `TimedOut`, or `Random`. Random gives each
new payment an equal chance of Succeeded, Failed, or TimedOut. The selected result
is saved on creation; retries and recovery reuse it. `delayMs` must be nonnegative;
`timeoutMs` must be between 1 and 30000. Invalid settings return 400.
If the delay exceeds the timeout, a new payment times out at the timeout deadline,
regardless of the selected outcome. Explicit `TimedOut` uses the earlier of delay and timeout.
Restore normal behavior with `Succeeded`, delay 500, and timeout 5000 through PUT.

Settings are global to new payment attempts, stored in PostgreSQL, and survive restarts.
Existing pending and completed attempts retain their saved plan and result. Use a new
booking to test a changed outcome; retrying a booking reuses its payment.
Concurrent updates use last-write-wins semantics.

Until the first successful PUT, the service uses `PaymentSimulation` configuration
(default: Succeeded / 500 / 5000). Once settings are saved, they take precedence over
configuration and Compose simulation environment variables. Changing those variables
or recreating the container does not reset saved settings.

Startup automatically applies the included EF migration. Examples are also available
in `src/Services/PaymentSimulator/Payments.http`.
