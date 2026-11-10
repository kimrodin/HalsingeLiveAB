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

Both Vite and the base Compose Nginx configuration forward `/payment-api` to
ApiGateway using `API_GATEWAY_PROXY_TARGET` (in Compose: `http://api-gateway:8080`).
The gateway removes `/payment-api`, retaining the backend's `/api` prefix.
From the host, use `http://localhost:5212/payment-api/api/admin/payment-simulation`,
or the same path on the admin app's origin. For separate host-side Vite, the
gateway target defaults to `http://localhost:5212`. Restart Vite or recreate the
container after changing proxy configuration. The dialog is available in Vite
development mode; the base Nginx build still proxies these API paths.

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
new payment an 80% chance of Succeeded, 15% of Failed, and 5% of TimedOut. The selected result
is saved on creation; retries and recovery reuse it. `delayMs` must be nonnegative;
`timeoutMs` must be between 1 and 30000. Invalid settings return 400.
If the delay exceeds the timeout, a new payment times out at the timeout deadline,
regardless of the selected outcome. `TimedOut`, whether selected explicitly or through
Random, remains pending until the full timeout has elapsed. Succeeded and Failed use the delay.
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
