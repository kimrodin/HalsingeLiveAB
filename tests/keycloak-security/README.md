# Keycloak security integration tests

```powershell
./tests/keycloak-security/run.ps1
```

Uses the production realm import and Nginx template with real Keycloak 26.8.2.
Runs `start --import-realm` with explicit HTTP and `dev-file`, matching the
local Compose configuration; it does not require TLS certificates.
No application `.env`, host ports or persistent volumes. Credentials are public
fixture values. Uses subnet `172.30.154.0/24`; run only one copy at a time.
The runner uses a unique project and always removes its containers, including on failure.
It warms the proxy DNS cache, changes Keycloak's IP and runs all checks without
restarting the proxy. This catches stale upstream addresses after container recreation.
After the IP change it waits six seconds for the template's five-second DNS
cache to expire on all Nginx workers before making authentication or admin writes.
It does not retry mutating requests or restart the proxy to hide recovery failures.

Checks browser authorization code login with PKCE, token refresh, five failed
passwords followed by temporary account lock, recovery after the real 60-second
lock, login and token IP limits, another source IP, discovery during a limit,
and spoofed forwarding headers through the proxy and directly to Keycloak.
Login events are enabled only in the test realm to inspect verified client IPs.
No tokens or passwords are printed. Account recovery deliberately takes 62 seconds.
CI runs the same command and always removes the containers afterward.
On failure, Keycloak and proxy logs are saved to the ignored `results/services.log`
before teardown. CI retains them as `keycloak-security-diagnostics` for seven days.
Logs contain synthetic fixture data and may include client IPs; delete local
diagnostics after troubleshooting.
