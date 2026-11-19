"""Real browser authorization-code/PKCE flow using only the standard library."""
import base64
import hashlib
import html
import http.client
import http.cookiejar
import json
import re
import time
import urllib.error
import urllib.parse
import urllib.request

PUBLIC = "http://proxy"
REALM = "/realms/halsingelive"
TOKEN = REALM + "/protocol/openid-connect/token"
REDIRECT = "http://localhost:5173/"
PASSWORD = "fixture-password"
SPOOFED = {"X-Forwarded-For": "198.51.100.88", "X-Real-IP": "203.0.113.99",
           "Forwarded": "for=198.51.100.88", "X-Forwarded-Proto": "https",
           "X-Forwarded-Host": "attacker.invalid", "X-Forwarded-Port": "443"}


class StopCallback(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, msg, headers, newurl):
        if newurl.startswith(REDIRECT):
            return None
        return super().redirect_request(request, fp, code, msg, headers, newurl)


def browser():
    return urllib.request.build_opener(urllib.request.ProxyHandler({}), StopCallback(),
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def request(opener, path, data=None, headers=None, method=None, host=PUBLIC):
    if isinstance(data, dict):
        data = urllib.parse.urlencode(data).encode()
    req = urllib.request.Request(host + path, data=data, headers=headers or {}, method=method)
    try:
        response = opener.open(req, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.status, response.headers, response.read()


client = browser()
# Compose service_started does not imply that Nginx has finished its entrypoint.
for attempt in range(30):
    try:
        status, _, body = request(client, REALM + "/.well-known/openid-configuration")
        if status == 200:
            assert json.loads(body)["issuer"] == PUBLIC + REALM
            break
    except (urllib.error.URLError, ConnectionError):
        pass
    time.sleep(1)
else:
    raise AssertionError("Keycloak proxy did not become ready")

status, _, body = request(client, "/realms/master/protocol/openid-connect/token", {
    "client_id": "admin-cli", "grant_type": "password",
    "username": "fixture-admin", "password": PASSWORD})
assert status == 200, (status, body)
admin_headers = {"Authorization": "Bearer " + json.loads(body)["access_token"]}


def admin(path, data=None, method=None):
    headers = dict(admin_headers)
    if data is not None:
        data = json.dumps(data).encode()
        headers["Content-Type"] = "application/json"
    status, _, body = request(client, "/admin/realms/halsingelive" + path, data, headers, method)
    assert status in (200, 204), (status, body)
    return json.loads(body) if body else None


realm = admin("")
assert realm["bruteForceProtected"] and not realm["permanentLockout"]
assert realm["failureFactor"] == 5 and realm["waitIncrementSeconds"] == 60
# Enable events only in this disposable fixture to inspect verified client addresses.
realm["eventsEnabled"] = True
admin("", realm, "PUT")


def login(password):
    session = browser()
    verifier = "v" * 64
    challenge = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).decode().rstrip("=")
    params = urllib.parse.urlencode({"client_id": "admin-web", "response_type": "code",
        "redirect_uri": REDIRECT, "scope": "openid", "state": "fixture-state",
        "code_challenge": challenge, "code_challenge_method": "S256"})
    status, _, body = request(session, REALM + "/protocol/openid-connect/auth?" + params,
                              headers=SPOOFED)
    assert status == 200, (status, body[:200])
    action = html.unescape(re.search(r'<form[^>]+action="([^"]+)"', body.decode()).group(1))
    parsed = urllib.parse.urlsplit(action)
    assert parsed.netloc == "proxy" and parsed.scheme == "http", action
    status, headers, _ = request(session, parsed.path + "?" + parsed.query,
        {"username": "testadmin", "password": password}, SPOOFED)
    if status == 200:
        return None  # Login form with an error, including temporarily disabled account.
    assert status == 302, status
    callback = urllib.parse.urlsplit(headers["Location"])
    assert headers["Location"].startswith(REDIRECT)
    code = urllib.parse.parse_qs(callback.query)["code"][0]
    status, _, body = request(session, TOKEN, {"client_id": "admin-web",
        "grant_type": "authorization_code", "code": code,
        "redirect_uri": REDIRECT, "code_verifier": verifier}, SPOOFED)
    assert status == 200, (status, body)
    return json.loads(body)


tokens = login(PASSWORD)
assert tokens and tokens.get("refresh_token")
status, _, body = request(client, TOKEN, {"client_id": "admin-web",
    "grant_type": "refresh_token", "refresh_token": tokens["refresh_token"]}, SPOOFED)
assert status == 200 and json.loads(body).get("access_token"), (status, body)
print("PASS: browser login, PKCE, public hostname and refresh", flush=True)

# Stay below quick-login detection, then verify that the correct password is locked.
for _ in range(5):
    time.sleep(1.1)
    assert login("wrong-password") is None
time.sleep(1)
assert login(PASSWORD) is None, "Correct password must not bypass temporary account lock"
events = admin("/events?type=LOGIN_ERROR")
assert any(event.get("error") == "user_temporarily_disabled" for event in events), events
assert events and all(event["ipAddress"] == "172.30.154.100" for event in events), events
print("PASS: account lock and verified client IP despite forged headers", flush=True)

# Direct Keycloak requests are from an untrusted container, not the trusted proxy.
def raw(host, path, data, headers=None):
    connection = http.client.HTTPConnection(host, timeout=10)
    try:
        connection.request("POST", path, urllib.parse.urlencode(data),
            {"Content-Type": "application/x-www-form-urlencoded", **(headers or {})})
        response = connection.getresponse()
        result = response.status, response.read()
        return result
    finally:
        connection.close()


invalid = {"client_id": "admin-web", "grant_type": "authorization_code", "code": "invalid"}
statuses = [raw("proxy", TOKEN, invalid, {"X-Forwarded-For": f"198.51.100.{i}"})[0]
            for i in range(30)]
assert 429 in statuses, statuses
assert request(client, "/", host="http://other:9000")[2] in (b"400", b"401")
assert request(client, REALM + "/.well-known/openid-configuration")[0] == 200
# A direct untrusted container must not be able to claim a different client IP.
raw("keycloak:8080", TOKEN, invalid, SPOOFED)
errors = admin("/events?type=CODE_TO_TOKEN_ERROR")
assert any(event["ipAddress"] == "172.30.154.100" for event in errors), errors
assert all(event["ipAddress"] in ("172.30.154.100", "172.30.154.101") for event in errors), errors
statuses = [raw("proxy", REALM + "/login-actions/authenticate", {},
                {"X-Forwarded-For": f"203.0.113.{i}"})[0] for i in range(30)]
assert 429 in statuses, statuses
print("PASS: token rate limit cannot be bypassed with headers; discovery remains available", flush=True)

time.sleep(62)  # Real account lock and proxy buckets must recover without resetting state.
assert login(PASSWORD), "Account must recover after the temporary lock"
print("PASS: account and IP buckets recover", flush=True)
