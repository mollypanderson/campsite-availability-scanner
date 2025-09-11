#!/usr/bin/env python3
import hmac
import hashlib
import json
from http.server import BaseHTTPRequestHandler, HTTPServer
import subprocess
import os

GITHUB_WEBHOOK_SECRET = os.environ.get("GITHUB_WEBHOOK_SECRET")
WEBHOOK_PORT = 9000

class Handler(BaseHTTPRequestHandler):
    def do_POST(self):
        length = int(self.headers.get('content-length', 0))
        body = self.rfile.read(length)

        # Optional: validate signature
        signature = self.headers.get('X-Hub-Signature-256', '')
        if GITHUB_WEBHOOK_SECRET:
            mac = hmac.new(GITHUB_WEBHOOK_SECRET.encode(), body, hashlib.sha256)
            if not hmac.compare_digest("sha256=" + mac.hexdigest(), signature):
                self.send_response(403)
                self.end_headers()
                return

        # Trigger deployment
        subprocess.Popen(["/home/molly/scripts/deploy.sh"])

        self.send_response(200)
        self.end_headers()

httpd = HTTPServer(("", WEBHOOK_PORT), Handler)
print(f"Listening on port {WEBHOOK_PORT}")
httpd.serve_forever()
