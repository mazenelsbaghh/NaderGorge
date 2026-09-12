#!/usr/bin/env python3
"""Provision stable Baileys secrets and private routing without printing values."""

from __future__ import annotations

import argparse
import base64
import json
import os
from pathlib import Path
import secrets
import sys
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "deploy/production/scripts"))
from clusterctl import load_inventory, operator_transport, target


def configure_local_bridge_access(transport, remote, *, apply: bool) -> None:
    rule = ['iifname', '"massar-app0"', 'ip', 'saddr', '172.29.0.0/24', 'tcp', 'dport', '3002', 'accept', 'comment', '"massar-baileys-local"']
    transport.run(remote, ['sudo', '/usr/sbin/nft', '--check', 'insert', 'rule', 'inet', 'massar', 'input', *rule])
    source = '/etc/massar/massar-production.nft'
    anchor = 'ip saddr 172.29.0.0/24 tcp dport { 6379, 6432, 26379 } accept'
    transport.run(remote, ['python3', '-c', f"from pathlib import Path; text=Path('{source}').read_text(); assert {anchor!r} in text, 'Unrecognized firewall configuration'"])
    if not apply:
        print('node-3: preview local app-network access to the private bridge; no public port rule')
        return
    staged = f'/home/massar-ops/.massar-baileys-firewall-{uuid.uuid4().hex}'
    line = '    ' + ' '.join(rule)
    code = (
        f"from pathlib import Path; source=Path('{source}'); text=source.read_text(); "
        f"updated=text if 'massar-baileys-local' in text else text.replace({anchor!r}, {anchor!r}+'\\n'+{line!r},1); "
        f"Path('{staged}').write_text(updated)"
    )
    try:
        transport.run(remote, ['python3', '-c', code])
        transport.run(remote, ['sudo', '/usr/sbin/nft', '--check', '-f', staged])
        transport.run(remote, ['sudo', '/usr/bin/install', '-m', '0644', '-o', 'root', '-g', 'root', staged, source])
        live = transport.run(remote, ['sudo', '/usr/sbin/nft', '-j', 'list', 'chain', 'inet', 'massar', 'input']).stdout
        if 'massar-baileys-local' not in live:
            transport.run(remote, ['sudo', '/usr/sbin/nft', 'insert', 'rule', 'inet', 'massar', 'input', *rule])
        print('node-3: persistent and live local bridge access configured')
    finally:
        transport.run(remote, ['rm', '-f', staged])


def credentials(path: Path) -> dict[str, str]:
    path.parent.mkdir(mode=0o700, parents=True, exist_ok=True)
    if not path.exists():
        values = {"BAILEYS_API_KEY": secrets.token_hex(32), "BAILEYS_AUTH_KEY": base64.b64encode(secrets.token_bytes(32)).decode()}
        descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(descriptor, "w") as stream:
            json.dump(values, stream)
    if path.stat().st_mode & 0o077:
        raise ValueError("Baileys secret file must have mode 0600")
    values = json.loads(path.read_text())
    token = values.get("BAILEYS_API_KEY", "")
    if len(token) < 32 or not token.isalnum() or len(base64.b64decode(values.get("BAILEYS_AUTH_KEY", ""), validate=True)) != 32:
        raise ValueError("Invalid Baileys secret file")
    return {key: values[key] for key in ("BAILEYS_API_KEY", "BAILEYS_AUTH_KEY")}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--secret-file", type=Path, required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    if args.secret_file.resolve().is_relative_to(ROOT):
        raise ValueError("Keep the persistent secret file outside the repository")
    inventory = load_inventory(ROOT / "deploy/production/inventory/production.yml", require_operator_files=True)
    transport = operator_transport(inventory)
    bridge = next(node for node in inventory.nodes if node.id == "node-3")
    for node in inventory.nodes:
        transport.run(target(inventory, node), ["test", "-f", "/etc/massar/app.env"])
        print(f"{node.id}: environment available; bridge target node-3")
    configure_local_bridge_access(transport, target(inventory, bridge), apply=False)
    if args.dry_run:
        print("Preview: preserve/create local protected keys, merge matching credentials and private routing on all nodes; no restart.")
        return 0
    values = credentials(args.secret_file)
    values["BAILEYS_BASE_URL"] = f"http://{bridge.overlay_address}:3002"
    with tempfile.NamedTemporaryFile(mode="w", delete=False) as stream:
        os.chmod(stream.name, 0o600)
        stream.write("\n".join(f"{key}={value}" for key, value in values.items()) + "\n")
        local = Path(stream.name)
    try:
        for node in inventory.nodes:
            remote = target(inventory, node)
            incoming = f"/home/massar-ops/.massar-baileys-{uuid.uuid4().hex}"
            staged = f"/home/massar-ops/.massar-app-env-{uuid.uuid4().hex}"
            try:
                transport.copy(remote, local, incoming)
                code = (
                    "from pathlib import Path; import os; "
                    f"updates=dict(line.split('=',1) for line in Path('{incoming}').read_text().splitlines()); "
                    "rows=Path('/etc/massar/app.env').read_text().splitlines(); "
                    "existing=dict(line.split('=',1) for line in rows if line and not line.startswith('#') and '=' in line); "
                    "assert all(not existing.get(key) or existing[key]==updates[key] for key in ('BAILEYS_API_KEY','BAILEYS_AUTH_KEY')), 'Existing Baileys credentials differ; refusing rotation'; "
                    "prefixes=tuple(key+'=' for key in updates); rows=[line for line in rows if not line.startswith(prefixes)]; "
                    "rows.extend(key+'='+value for key,value in updates.items()); "
                    f"dst=Path('{staged}'); dst.write_text('\\n'.join(rows)+'\\n'); os.chmod(dst,0o600)"
                )
                transport.run(remote, ["python3", "-c", code], timeout_seconds=30)
                transport.run(remote, ["sudo", "/usr/bin/install", "-m", "0640", "-o", "root", "-g", "massar", staged, "/etc/massar/app.env"], timeout_seconds=30)
                print(f"{node.id}: configured")
            finally:
                transport.run(remote, ["rm", "-f", incoming, staged], timeout_seconds=10)
        configure_local_bridge_access(transport, target(inventory, bridge), apply=True)
    finally:
        local.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
