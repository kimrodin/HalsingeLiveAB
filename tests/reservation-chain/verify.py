"""Real HTTP requests and Redis snapshots; only Python's standard library is used."""
import hashlib
import hmac
import ipaddress
import json
import os
from pathlib import Path
import socket
import time
import urllib.error
import urllib.request

PREFIX = "booking:reservation-limits:v1"
SECTION = "72000000000000000000000000000001"
CASE = os.environ["CASE"]
SOURCE = {"frequency": ".100", "recovery": ".100", "other": ".101", "active": ".102", "quantity": ".103"}[CASE]
SOURCE = "172.30.153" + SOURCE
CLIENT_KEY = hmac.new(bytes.fromhex("aa" * 32), ipaddress.ip_address(SOURCE).packed, hashlib.sha256).hexdigest()
ATTEMPTS = f"{PREFIX}:attempts:{CLIENT_KEY}"
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
report = {"case": CASE, "source": SOURCE, "requests": [], "snapshots": []}


def redis(*parts):
    def read(stream):
        kind, line = stream.read(1), stream.readline().removesuffix(b"\r\n")
        if kind == b"+": return line.decode()
        if kind == b"-": raise RuntimeError(line.decode())
        if kind == b":": return int(line)
        if kind == b"$":
            size = int(line)
            if size == -1: return None
            value = stream.read(size)
            assert stream.read(2) == b"\r\n"
            return value.decode()
        if kind == b"*": return [read(stream) for _ in range(int(line))]
        raise RuntimeError(f"Invalid Redis response: {kind!r}")
    encoded = [str(part).encode() for part in parts]
    request = f"*{len(encoded)}\r\n".encode() + b"".join(
        f"${len(part)}\r\n".encode() + part + b"\r\n" for part in encoded)
    with socket.create_connection(("redis", 6379), timeout=5) as connection:
        connection.sendall(request)
        return read(connection.makefile("rb"))


def snapshot(inventory):
    event = f"73000000000000000000{inventory:012}"
    active = f"{PREFIX}:active:{CLIENT_KEY}:{event}"
    state = {"inventory": inventory, "stock": redis("GET", f"inventory:{event}:{SECTION}:available"),
             "active": redis("ZRANGE", active, 0, -1, "WITHSCORES"),
             "quantities": redis("HGETALL", active + ":quantities"),
             "attempts": redis("ZCARD", ATTEMPTS)}
    report["snapshots"].append(state)
    return state


def post(inventory, expected, quantity=1, proxy="customer", code=None):
    request = urllib.request.Request(f"http://{proxy}/booking-api/CreateBooking?privacyProbe=198.51.100.88",
        data=json.dumps({"inventoryId": f"74000000-0000-0000-0000-{inventory:012}", "quantity": quantity,
                         "customerName": "Synthetic customer", "customerEmail": "fixture@example.com"}).encode(),
        headers={"Content-Type": "application/json", "X-Forwarded-For": "198.51.100.88",
                 "X-Real-IP": "203.0.113.99", "User-Agent": "privacy-probe-203.0.113.99",
                 "Referer": "http://example.invalid/198.51.100.88"})
    try:
        response = opener.open(request, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = json.load(response)
        result = {"proxy": proxy, "inventory": inventory, "quantity": quantity, "status": response.status,
                  "retryAfter": response.headers.get("Retry-After"), "code": body.get("code"),
                  "bookingId": body.get("bookingId")}
    report["requests"].append(result)  # Never persist guest access tokens.
    assert result["status"] == expected, result
    if code:
        assert result["code"] == code, result
        assert 1 <= int(result["retryAfter"]) <= 600, result
    return result


def denied_without_new_hold(inventory, code, quantity=1):
    before = snapshot(inventory)
    post(inventory, 429, quantity=quantity, proxy="admin", code=code)
    after = snapshot(inventory)
    assert before["stock"] == after["stock"]
    assert before["active"] == after["active"]  # Includes original deadlines.
    assert before["quantities"] == after["quantities"]
    return before, after


try:
    if CASE == "frequency":
        for inventory in (1, 2, 3): post(inventory, 202)
        before, after = denied_without_new_hold(1, "AttemptLimitExceeded")
        assert before["attempts"] == after["attempts"] == 3
    elif CASE == "other":
        post(1, 202)
        assert len(snapshot(1)["active"]) == 2  # One member and one score.
    elif CASE == "active":
        post(4, 202)
        post(4, 202, proxy="admin")
        before, after = denied_without_new_hold(4, "ActiveReservationLimitExceeded")
        assert len(after["active"]) == 4 and after["stock"] == "98"
    elif CASE == "quantity":
        post(5, 202, quantity=6)
        before, after = denied_without_new_hold(5, "ReservedQuantityLimitExceeded", quantity=5)
        assert after["stock"] == "94"
        post(5, 202, quantity=4, proxy="admin")
        final = snapshot(5)
        assert final["stock"] == "90"
        assert sum(map(int, final["quantities"][1::2])) == 10
    elif CASE == "recovery":
        ttl = redis("PTTL", ATTEMPTS)
        if ttl > 0:
            print(f"Waiting {ttl / 1000:.1f}s for the real 60-second window", flush=True)
            time.sleep(ttl / 1000 + 0.2)
        post(1, 202)
        assert snapshot(1)["attempts"] == 1
    report["passed"] = True
finally:
    Path(f"/results/{CASE}.json").write_text(json.dumps(report, indent=2) + "\n")
print(f"PASS: {CASE}", flush=True)
