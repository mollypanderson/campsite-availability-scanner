#!/usr/bin/env python3
import hmac
import hashlib
import json
import os
import subprocess
from http.server import BaseHTTPRequestHandler, HTTPServer

# Load the secret from environment variable
GITHUB_WEBHOOK_SECRET = os.environ.get("GITHUB_WEBHOOK_SECRET", "").encode()
DEPLOY_SCRIPT = "/home/molly/campsite-availability-scanner/deploy.sh"
PORT = 9000 

class WebhookHandler(BaseHTTPRequestHandler):
    def do_POST(self):
        if self.path != "/deploy":
            self.send_response(404)
            self.end_headers()
            return

        # Read payload
        content_length = int(self.headers.get("Content-Length", 0))
        payload_bytes = self.rfile.read(content_length)

        # Verify signature
        signature = self.headers.get("X-Hub-Signature-256")
        if signature is None:
            self.send_response(401)
            self.end_headers()
            self.wfile.write(b"No signature header")
            return

        mac = hmac.new(GITHUB_WEBHOOK_SECRET, msg=payload_bytes, digestmod=hashlib.sha256)
        expected_signature = f"sha256={mac.hexdigest()}"

        if not hmac.compare_digest(signature, expected_signature):
            self.send_response(401)
            self.end_headers()
            self.wfile.write(b"Invalid signature")
            return

        # Parse payload
        payload = json.loads(payload_bytes)
        # Only deploy on main branch
        if payload.get("ref") != "refs/heads/main":
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b"Ignoring non-main branch push")
            return

        # Trigger deploy script
        try:
            subprocess.run(["/bin/bash", DEPLOY_SCRIPT], check=True)
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b"Deploy triggered successfully")
        except subprocess.CalledProcessError as e:
            self.send_response(500)
            self.end_headers()
            self.wfile.write(f"Deploy failed: {e}".encode())

def run():
    server_address = ("0.0.0.0", PORT)
    httpd = HTTPServer(server_address, WebhookHandler)
    print(f"Listening for GitHub webhook on port {PORT}...")
    httpd.serve_forever()

if __name__ == "__main__":
    run()
