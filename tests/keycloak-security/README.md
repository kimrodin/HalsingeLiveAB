# Keycloak security integration tests

```sh
docker compose -p halsingelive-keycloak-tests -f tests/keycloak-security/compose.yaml up --abort-on-container-exit --exit-code-from client
docker compose -p halsingelive-keycloak-tests -f tests/keycloak-security/compose.yaml down
```

Uses the production realm import and Nginx template with real Keycloak 26.8.2.
No application `.env`, host ports or persistent volumes. Credentials are public
fixture values. Uses subnet `172.30.154.0/24`; run only one copy at a time.
Always run `down` after local verification, including after a failure.

Checks browser authorization code login with PKCE, token refresh, five failed
passwords followed by temporary account lock, recovery after the real 60-second
lock, login and token IP limits, another source IP, discovery during a limit,
and spoofed forwarding headers through the proxy and directly to Keycloak.
Login events are enabled only in the test realm to inspect verified client IPs.
No tokens or passwords are printed. Account recovery deliberately takes 62 seconds.
CI runs the same command and always removes the containers afterward.
