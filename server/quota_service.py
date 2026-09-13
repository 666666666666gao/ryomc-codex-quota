"""Private upstream collector. Exposes only a sanitized weekly snapshot."""
import hmac
import json
import os
from pathlib import Path
import subprocess
import threading
import time
import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import yaml


def weekly(payload, now):
    limits = payload.get('rate_limit') or {}
    windows = [limits.get(k) for k in ('primary_window', 'secondary_window')]
    windows = [w for w in windows if isinstance(w, dict) and w.get('limit_window_seconds') == 604800]
    if len(windows) != 1:
        raise ValueError('weekly_window_unavailable')
    w = windows[0]
    used = w.get('used_percent')
    if type(used) not in (int, float) or not 0 <= used <= 100:
        raise ValueError('weekly_percentage_unavailable')
    reset = w.get('reset_at')
    if type(reset) not in (int, float) or reset <= 0:
        delta = w.get('reset_after_seconds')
        reset = now + delta if type(delta) in (int, float) and delta >= 0 else None
    iso = lambda t: datetime.datetime.fromtimestamp(t, datetime.timezone.utc).isoformat().replace('+00:00', 'Z')
    return {'remainingPercent': round(100-used, 2), 'resetAt': iso(reset) if reset is not None else None,
            'queriedAt': iso(now), 'windowSeconds': 604800}


def curl_quote(value):
    return '"' + str(value).replace('\\', '\\\\').replace('"', '\\"').replace('\n', '\\n').replace('\r', '\\r') + '"'


def collect(cfg):
    auth = json.loads(Path(cfg['authFile']).read_text())
    if auth.get('disabled') or auth.get('type') != 'codex':
        raise ValueError('account_unavailable')
    upstream = yaml.safe_load(Path(cfg['cpaConfig']).read_text())
    proxy = auth.get('proxy_url') or upstream.get('proxy-url')
    token = auth.get('access_token')
    if not token:
        raise ValueError('account_unavailable')
    # Send secrets on stdin, not command-line arguments or HTTP response bodies.
    options = ['url = "https://chatgpt.com/backend-api/wham/usage"', 'silent', 'show-error',
               'max-time = 25', 'proto = "=https"',
               'header = ' + curl_quote('Authorization: Bearer ' + token),
               'header = "User-Agent: codex-tui/0.149.1"',
               'write-out = "\\n%{http_code}"']
    if auth.get('account_id'):
        options.append('header = ' + curl_quote('Chatgpt-Account-Id: ' + auth['account_id']))
    if proxy:
        options.append('proxy = ' + curl_quote(proxy))
    result = subprocess.run(['curl', '--config', '-'], input='\n'.join(options), text=True,
                            capture_output=True, timeout=30)
    if result.returncode:
        raise ValueError('upstream_connection_failed')
    body, status = result.stdout.rsplit('\n', 1)
    if status != '200':
        raise ValueError('upstream_http_' + status if status.isdigit() else 'upstream_error')
    return weekly(json.loads(body), time.time())


class Snapshot:
    def __init__(self, collector, interval=120):
        self.collector, self.interval = collector, interval
        self.lock = threading.Lock()
        self.updated = 0
        self.result = None
        self.ok = False

    def get(self):
        with self.lock:
            if time.monotonic() - self.updated >= self.interval or self.result is None:
                try:
                    self.result, self.ok = self.collector(), True
                except Exception:
                    # Raw upstream errors may include credentials and private account data.
                    self.result, self.ok = {'error': 'upstream_quota_unavailable'}, False
                self.updated = time.monotonic()
            return self.ok, self.result


def handler_for(cfg, snapshot):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def do_GET(self):
            if self.path != '/quota/weekly':
                return self.reply(404, {'error': 'not_found'})
            given = self.headers.get('Authorization', '')
            if not hmac.compare_digest(given.encode(), ('Bearer ' + cfg['readToken']).encode()):
                return self.reply(401, {'error': 'invalid_read_token'})
            ok, body = snapshot.get()
            self.reply(200 if ok else 503, body)

        def reply(self, status, body):
            data = json.dumps(body).encode()
            self.send_response(status)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Cache-Control', 'no-store')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            self.wfile.write(data)
    return Handler


if __name__ == '__main__':
    cfg = json.loads(Path(os.environ.get('RYOMC_SERVICE_CONFIG', '/etc/ryomc-quota/config.json')).read_text())
    if len(cfg['readToken']) < 32:
        raise ValueError('Read token must contain at least 32 characters')
    snapshot = Snapshot(lambda: collect(cfg))
    ThreadingHTTPServer((cfg['bind'], cfg['port']), handler_for(cfg, snapshot)).serve_forever()
