"""Synthetic catalog only. Booking, its HTTP catalog adapter and storage are real."""
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from uuid import UUID

SECTION = "72000000-0000-0000-0000-000000000001"
VENUE = "71000000-0000-0000-0000-000000000001"
EVENTS = {f"73000000-0000-0000-0000-{i:012}" for i in range(1, 6)}


class Catalog(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path.startswith("/GetEvent/") and self.path.rsplit("/", 1)[-1] in EVENTS:
            body = {"eventId": str(UUID(self.path.rsplit("/", 1)[-1])), "name": "Reservation chain fixture",
                    "startDate": "2099-01-01T18:00:00Z", "venueId": VENUE,
                    "sectionPrices": [{"sectionId": SECTION, "price": 100}]}
        elif self.path == f"/GetVenue/{VENUE}":
            body = {"venueId": VENUE, "name": "Synthetic hall", "streetAddress": "Test street",
                    "postalCode": "82400", "city": "Hudiksvall", "capacity": 100,
                    "sections": [{"sectionId": SECTION, "name": "Standing", "capacity": 100, "seatingType": 0}]}
        else:
            self.send_error(404)
            return
        payload = json.dumps(body).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, *_args):
        pass


ThreadingHTTPServer(("0.0.0.0", 8080), Catalog).serve_forever()
