"""Issue a token request from an independent client IP, without forged headers."""
import http.client
from http.server import BaseHTTPRequestHandler, HTTPServer


class Other(BaseHTTPRequestHandler):
    def do_GET(self):
        connection = http.client.HTTPConnection("proxy", timeout=10)
        try:
            connection.request("POST", "/realms/halsingelive/protocol/openid-connect/token",
                "client_id=admin-web&grant_type=authorization_code&code=invalid",
                {"Content-Type": "application/x-www-form-urlencoded"})
            response = connection.getresponse()
            response.read()
            self.send_response(200)
            self.end_headers()
            self.wfile.write(str(response.status).encode())
        finally:
            connection.close()


HTTPServer(("0.0.0.0", 9000), Other).serve_forever()
