import json
import time
import urllib.request

opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
forged = {
    "X-Forwarded-For": "203.0.113.90, 203.0.113.91",
    "X-Forwarded-Proto": "https",
    "X-Real-IP": "203.0.113.92",
    "Forwarded": "for=203.0.113.93",
    "X-Original-For": "203.0.113.94",
}


def probe(host, path, headers):
    request = urllib.request.Request(f"http://{host}{path}", headers=headers)
    with opener.open(request, timeout=3) as response:
        return json.load(response)


# Wait for all three application hops, not just for open container ports.
for attempt in range(60):
    try:
        for frontend in ("172.30.152.3", "172.30.152.4"):
            probe(frontend, "/booking-api/probe", {})
        break
    except (OSError, ValueError):
        time.sleep(1)
else:
    raise RuntimeError("Proxy chain did not become ready")

checks = 0
for frontend in ("172.30.152.3", "172.30.152.4", "[fd00:151:152::3]", "[fd00:151:152::4]"):
    expected = "fd00:151:152::100" if frontend.startswith("[") else "172.30.152.100"
    for headers in ({}, forged, {**forged, "X-Forwarded-For": "::ffff:203.0.113.90"}):
        result = probe(frontend, "/booking-api/probe", headers)
        assert result == {"address": expected, "scheme": "http", "hasForwardingHeaders": False}, result
        checks += 1

# Bypassing the frontend or gateway must not make a supplied identity authoritative.
for host, path in (("172.30.152.2:8080", "/booking-api/probe"),
                   ("172.30.152.5:8080", "/probe"),
                   ("[fd00:151:152::2]:8080", "/booking-api/probe")):
    expected = "fd00:151:152::100" if host.startswith("[") else "172.30.152.100"
    result = probe(host, path, forged)
    assert result == {"address": expected, "scheme": "http", "hasForwardingHeaders": False}, result
    checks += 1

print(f"PASS: {checks} proxy-chain checks (both Nginx templates, IPv4/IPv6, spoofing and direct calls)", flush=True)
