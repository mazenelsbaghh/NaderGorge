#!/usr/bin/env python3
"""Start or inspect the dedicated node-3 ChatGPT login without copying credentials."""
import argparse
import shlex
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'production/scripts'))
from clusterctl import load_inventory, target
from ssh_transport import StrictSshTransport

parser = argparse.ArgumentParser()
parser.add_argument('--status', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
inventory = load_inventory(root / 'deploy/production/inventory/production.yml', require_operator_files=True)
transport = StrictSshTransport(Path(inventory.cluster['known_hosts_file']), Path(inventory.cluster['identity_file']))
node = target(inventory, next(n for n in inventory.nodes if n.id == 'node-3'))
remote = ['env', 'CODEX_HOME=/home/massar-ops/.local/share/massar-auto-repair/codex-home',
    '/home/massar-ops/.local/lib/massar-codex/0.147.0/bin/codex', 'login', 'status' if args.status else '--device-auth']
raise SystemExit(subprocess.run([*transport.base_args(), f'{node.user}@{node.address}', '--', shlex.join(remote)], check=False).returncode)
