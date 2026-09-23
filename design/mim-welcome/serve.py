"""Serve only the reviewed MIM preview, with audio ranges for mobile Safari."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit
import mimetypes
import re
import sys

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT.parent.parent / 'frontend/public/mim-game/assets'
ROUTES = {f'/{name}': ROOT / name for name in [
    'mim-bottom.png', 'mim-left.png', 'mim-right.png', 'platform.html', 'student-preview.html', 'platform.css', 'platform.mjs', 'embedded.html', 'brand-embed.css', 'embed.mjs', 'index.html', 'preview.css', 'preview.mjs', 'voice-timings.mjs', 'welcome-scenes.mjs', 'gemini-return-charon-mobile.m4a', 'gemini-return-charon.mp3',
    'gemini-puck-mobile.m4a', 'gemini-charon-mobile.m4a',
    'gemini-puck.mp3', 'gemini-charon.mp3',
]}
ROUTES.update({'/logo.svg': ASSETS.parent.parent / 'images/logo.svg', '/logo-mark.svg': ASSETS.parent.parent / 'images/logo-mark.svg', '/platform': ROOT / 'platform.html', '/': ROOT / 'index.html', '/mim.png': ASSETS / 'mim.png', '/arabic.woff2': ASSETS / 'arabic.woff2'})

class PreviewHandler(BaseHTTPRequestHandler):
    def do_HEAD(self):
        self.do_GET()

    def do_GET(self):
        route = urlsplit(self.path).path.removeprefix('/design/mim-welcome')
        asset = ROUTES.get(route)
        if asset is None:
            self.send_error(404)
            return
        content = asset.read_bytes()
        size = len(content)
        start, end = 0, size - 1
        byte_range = self.headers.get('Range')
        if byte_range:
            match = re.fullmatch(r'bytes=(\d+)-(\d*)', byte_range)
            if not match:
                self.send_error(416)
                return
            start = int(match[1])
            end = min(int(match[2]) if match[2] else size - 1, size - 1)
            if start > end:
                self.send_error(416)
                return
        self.send_response(206 if byte_range else 200)
        self.send_header('Content-Type', mimetypes.guess_type(asset)[0] or 'application/octet-stream')
        self.send_header('Content-Length', str(end - start + 1))
        self.send_header('Accept-Ranges', 'bytes')
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        if byte_range:
            self.send_header('Content-Range', f'bytes {start}-{end}/{size}')
        self.end_headers()
        if self.command != 'HEAD':
            self.wfile.write(content[start:end + 1])

if __name__ == '__main__':
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8767
    ThreadingHTTPServer(('0.0.0.0', port), PreviewHandler).serve_forever()
