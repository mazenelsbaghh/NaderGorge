"""Bounded application-container logs through pinned inventory-only SSH."""
import datetime as dt
import re
import sys
import threading
import subprocess
import urllib.error
import uuid
from pathlib import Path

from policy import redact

SERVICES = ('gateway', 'student', 'admin', 'teacher', 'staff', 'landing')
ERROR = re.compile(r'\b(error|fatal|critical|exception|warn(?:ing)?)\b|"\s+[45]\d\d\s', re.I)


def log_entries(stdout: str, node: str, service: str) -> list[dict]:
    entries = []
    for line in stdout.splitlines():
        timestamp, separator, message = line.partition(' ')
        if not separator or not ERROR.search(message):
            continue
        try:
            parsed = dt.datetime.fromisoformat(timestamp.replace('Z', '+00:00'))
            if parsed.tzinfo is None:
                continue
        except ValueError:
            continue
        entries.append({'id': str(uuid.uuid5(uuid.NAMESPACE_URL, node + service + line)),
            'timestamp': parsed.isoformat(), 'source': service, 'category': service,
            'level': 'warning' if re.search(r'\bwarn(?:ing)?\b', message, re.I) else 'error',
            'message': redact('[' + node + '] ' + message), 'exception': None})
    return entries


class Collector:
    def __init__(self, config, api):
        self.config, self.api = config, api
        self.stop = threading.Event()
        self.thread = threading.Thread(target=self.serve, daemon=True)

    def collect(self):
        scripts = str(Path(self.config['source_repository']) / 'deploy/production/scripts')
        if scripts not in sys.path:
            sys.path.insert(0, scripts)
        from clusterctl import load_inventory, target
        from ssh_transport import StrictSshTransport
        inventory = load_inventory(Path(scripts).parent / 'inventory/production.yml', require_operator_files=True)
        ssh = StrictSshTransport(Path(inventory.cluster['known_hosts_file']), Path(inventory.cluster['identity_file']))
        for node in inventory.nodes:
            for service in SERVICES:
                if self.stop.is_set():
                    return
                response = ssh.run(target(inventory, node), ['sudo', '-n', '/usr/bin/docker', 'logs', '--timestamps',
                    '--since', '2m', '--tail', '250', 'massar_production-' + service + '-1'], timeout_seconds=30)
                logs = log_entries(response.stdout + response.stderr, node.id, service)
                for offset in range(0, len(logs), 100):
                    self.api.post('logs', logs[offset:offset + 100])

    def serve(self):
        while not self.stop.is_set():
            try:
                self.collect()
            except (OSError, ValueError, RuntimeError, subprocess.SubprocessError, urllib.error.URLError) as exc:
                # This independent collector must retry outages without interrupting a deployment.
                # The error type is surfaced; no transport payload or credentials are logged.
                print('Container log collection failed: ' + type(exc).__name__, flush=True)
            self.stop.wait(60)
