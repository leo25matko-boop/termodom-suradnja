#!/usr/bin/env python3
"""Serve the TermoDom house view on loopback with a fixed, read-only HTTPS relay."""
import argparse
import json
import ssl
import sys
import threading
import webbrowser
from http import HTTPStatus
from http.client import HTTPException
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.error import URLError
from urllib.request import (
    HTTPRedirectHandler, HTTPSHandler, ProxyHandler, Request, build_opener,
)

ROOT = Path(__file__).resolve().parent
LOOPBACK = '127.0.0.1'
MAX_RESPONSE = 4 * 1024 * 1024
UPSTREAM_TIMEOUT = 8
SOURCE = 'https://automatiziraj.se/centralno/php/m.php'
STATIC = {
    '/': ('local.html', 'text/html; charset=UTF-8'),
    '/local.html': ('local.html', 'text/html; charset=UTF-8'),
    '/m-house.js': ('m-house.js', 'text/javascript; charset=UTF-8'),
    '/m-house-data.js': ('m-house-data.js', 'text/javascript; charset=UTF-8'),
    '/m-house.css': ('m-house.css', 'text/css; charset=UTF-8'),
    '/m-house-cutaway.webp': ('m-house-cutaway.webp', 'image/webp'),
}
ENDPOINTS = {f'/m.php?action={action}': f'{SOURCE}?action={action}' for action in ('data', 'pumps')}
JSON_TYPE = 'application/json; charset=UTF-8'
CSP = "; ".join((
    "default-src 'none'", "script-src 'self'", "style-src 'self' 'unsafe-inline'", "img-src 'self'",
    "connect-src 'self' https://api.open-meteo.com", "font-src 'self'", "base-uri 'none'",
    "form-action 'none'", "frame-ancestors 'none'",
))


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def public_get(fixed_url):
    if fixed_url not in ENDPOINTS.values():
        raise ValueError('Unknown source URL.')
    opener = build_opener(ProxyHandler({}), HTTPSHandler(context=ssl.create_default_context()), NoRedirect())
    request = Request(fixed_url, method='GET', headers={
        'Accept': 'application/json', 'Accept-Encoding': 'identity',
        'Cache-Control': 'no-cache', 'User-Agent': 'TermoDom-Local/1.0',
    })
    with opener.open(request, timeout=UPSTREAM_TIMEOUT) as upstream:
        if upstream.status != 200 or upstream.headers.get_content_type() != 'application/json':
            raise ValueError('Expected a JSON telemetry response.')
        size = upstream.headers.get('Content-Length')
        if size is not None and not 0 <= int(size) <= MAX_RESPONSE:
            raise ValueError('Response size is outside the limit.')
        body = upstream.read(MAX_RESPONSE + 1)
        if not body or len(body) > MAX_RESPONSE:
            raise ValueError('Response size is outside the limit.')
        return body


class LocalWebServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, port, fetch=public_get):
        self.fetch = fetch
        super().__init__((LOOPBACK, port), Handler)

    @property
    def host(self):
        return f'{LOOPBACK}:{self.server_port}'


class Handler(BaseHTTPRequestHandler):
    server_version = 'TermoDom-Local'
    sys_version = ''
    timeout = 12

    def log_message(self, format, *args):
        # Never print request URLs or headers. The console is just a launcher.
        return

    def reply(self, status, content_type, payload, *, head=False, allowed=None):
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(payload)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('X-Frame-Options', 'DENY')
        self.send_header('Referrer-Policy', 'no-referrer')
        self.send_header('Content-Security-Policy', CSP)
        if allowed:
            self.send_header('Allow', allowed)
        self.end_headers()
        if not head:
            try:
                self.wfile.write(payload)
            except (BrokenPipeError, ConnectionResetError):
                pass

    def fail(self, status, message, *, head=False, allowed=None):
        payload = json.dumps({'success': False, 'error': message}, ensure_ascii=False).encode('utf-8')
        self.reply(status, JSON_TYPE, payload, head=head, allowed=allowed)

    def host_allowed(self):
        return self.headers.get_all('Host') == [self.server.host]

    def local_origin_allowed(self):
        origins = self.headers.get_all('Origin')
        if origins is not None and origins != [f'http://{self.server.host}']:
            return False
        return self.headers.get('Sec-Fetch-Site') in (None, 'none', 'same-origin')

    def handle_request(self, *, head=False):
        if not self.host_allowed():
            return self.fail(HTTPStatus.FORBIDDEN, 'Aplikacija je dostupna samo na lokalnoj adresi.', head=head)
        # Match the raw path, not a user-supplied URL or arbitrary query. Route
        # only the exact two known requests; never pass client headers upstream.
        if self.path in ENDPOINTS:
            if head:
                return self.fail(HTTPStatus.METHOD_NOT_ALLOWED, 'Dopušten je samo GET.', head=True, allowed='GET')
            if not self.local_origin_allowed():
                return self.fail(HTTPStatus.FORBIDDEN, 'Dohvat nije pokrenut iz lokalne aplikacije.')
            try:
                payload = self.server.fetch(ENDPOINTS[self.path])
            except (URLError, ValueError, OSError, HTTPException):
                return self.fail(HTTPStatus.BAD_GATEWAY, 'Nije moguće pročitati podatke s izvornog sustava grijanja.')
            return self.reply(HTTPStatus.OK, JSON_TYPE, payload)
        entry = STATIC.get(self.path)
        if not entry:
            return self.fail(HTTPStatus.NOT_FOUND, 'Ova lokalna stranica nije dostupna.', head=head)
        filename, content_type = entry
        try:
            asset = (ROOT / filename).resolve(strict=True)
            if asset.parent != ROOT:
                raise OSError('Asset is outside the application directory.')
            payload = asset.read_bytes()
        except OSError:
            return self.fail(HTTPStatus.NOT_FOUND, 'Datoteka aplikacije nedostaje.', head=head)
        self.reply(HTTPStatus.OK, content_type, payload, head=head)

    def do_GET(self):
        self.handle_request()

    def do_HEAD(self):
        self.handle_request(head=True)

    def reject_method(self):
        if not self.host_allowed():
            return self.fail(HTTPStatus.FORBIDDEN, 'Aplikacija je dostupna samo na lokalnoj adresi.')
        self.fail(HTTPStatus.METHOD_NOT_ALLOWED, 'Promjene grijanja nisu dostupne preko lokalnog poslužitelja.', allowed='GET, HEAD')

    do_POST = do_PUT = do_PATCH = do_DELETE = do_OPTIONS = do_TRACE = do_CONNECT = reject_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=8740, help='Lokalni port (zadano: 8740).')
    parser.add_argument('--no-browser', action='store_true', help='Nemoj automatski otvoriti preglednik.')
    options = parser.parse_args()
    if not 1 <= options.port <= 65535:
        parser.error('Port mora biti između 1 i 65535.')
    try:
        server = LocalWebServer(options.port)
    except OSError:
        print(f'Ne mogu pokrenuti lokalni poslužitelj na portu {options.port}. Ako je zauzet, dodajte --port 8741.', file=sys.stderr)
        return 1
    with server:
        url = f'http://{server.host}/'
        print(f'TermoDom je dostupan na {url}\nZa zaustavljanje pritisnite Ctrl+C.', flush=True)
        if not options.no_browser:
            threading.Thread(target=webbrowser.open, args=(url,), daemon=True).start()
        try:
            server.serve_forever(poll_interval=0.25)
        except KeyboardInterrupt:
            pass
    return 0


if __name__ == '__main__':
    sys.exit(main())
