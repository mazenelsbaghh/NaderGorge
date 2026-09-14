#!/usr/bin/env python3
"""Bootstrap the fixed Cloudflare token connector executor on all nodes."""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

from clusterctl import load_inventory
from ssh_transport import SshTarget, StrictSshTransport


ROOT = Path(__file__).resolve().parents[3]
EXECUTOR = ROOT / "deploy/production/scripts/cloudflare_token_connector_executor.py"
SUDOERS = ROOT / "deploy/production/config/sudoers/massar-cloudflared-token"
UNIT = ROOT / "deploy/production/systemd/massar-cloudflared-token.service"
CONFIG = ROOT / "deploy/production/config/cloudflared/token-config.yml.tmpl"
REMOTE_EXECUTOR = "/usr/local/sbin/massar-cloudflared-token-install"
REMOTE_ASSET_DIRECTORY = "/usr/local/lib/massar-cloudflared-token"


class HelperInstallError(RuntimeError):
    pass


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def targets(inventory) -> tuple[SshTarget, ...]:
    nodes = {node.id: node for node in inventory.nodes}
    if set(nodes) != {"node-1", "node-2", "node-3"}:
        raise HelperInstallError("installer requires exactly the approved three-node inventory")
    return tuple(SshTarget(node_id, nodes[node_id].public_address, inventory.cluster["ssh_user"]) for node_id in ("node-3", "node-2", "node-1"))


def install(inventory, transport: StrictSshTransport) -> None:
    assets = (EXECUTOR, SUDOERS, UNIT, CONFIG)
    if not all(asset.is_file() for asset in assets):
        raise HelperInstallError("reviewed helper assets are missing")
    checksums = {asset.name: digest(asset) for asset in assets}
    for target in targets(inventory):
        for asset in assets:
            transport.copy(target, asset, f"/tmp/massar-cloudflared-bootstrap-{asset.name}", timeout_seconds=60)
        script = " ".join((
            "set -euo pipefail;",
            "trap 'rm -f /tmp/massar-cloudflared-bootstrap-*' EXIT;",
            "test \"$(cat /etc/massar/cluster-id)\" = massar-production;",
            *(
                f"printf '%s  %s\\n' '{checksums[asset.name]}' '/tmp/massar-cloudflared-bootstrap-{asset.name}' | sha256sum -c;"
                for asset in assets
            ),
            "sudo /usr/sbin/visudo -cf /tmp/massar-cloudflared-bootstrap-massar-cloudflared-token;",
            "sudo /usr/bin/install -d -m 0755 -o root -g root /usr/local/lib/massar-cloudflared-token;",
            "sudo /usr/bin/install -m 0755 -o root -g root /tmp/massar-cloudflared-bootstrap-cloudflare_token_connector_executor.py /usr/local/sbin/massar-cloudflared-token-install;",
            "sudo /usr/bin/install -m 0644 -o root -g root /tmp/massar-cloudflared-bootstrap-token-config.yml.tmpl /usr/local/lib/massar-cloudflared-token/config.yml;",
            "sudo /usr/bin/install -m 0644 -o root -g root /tmp/massar-cloudflared-bootstrap-massar-cloudflared-token.service /usr/local/lib/massar-cloudflared-token/massar-cloudflared-token.service;",
            "sudo /usr/bin/install -m 0440 -o root -g root /tmp/massar-cloudflared-bootstrap-massar-cloudflared-token /etc/sudoers.d/massar-cloudflared-token;",
            "test \"$(stat -c '%U:%G:%a' /usr/local/sbin/massar-cloudflared-token-install)\" = root:root:755;",
            "sudo -n -l /usr/local/sbin/massar-cloudflared-token-install | grep -F /usr/local/sbin/massar-cloudflared-token-install >/dev/null;",
        ))
        transport.run(target, ("bash", "-lc", script), timeout_seconds=120)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = arguments()
    inventory = load_inventory(args.inventory)
    targets(inventory)
    if args.dry_run:
        print('{"status":"dry-run","nodes":["node-3","node-2","node-1"],"helper":"massar-cloudflared-token-install"}')
        return 0
    if not args.yes:
        raise HelperInstallError("installer requires --yes or --dry-run")
    install(inventory, StrictSshTransport(args.known_hosts, args.identity))
    print('{"status":"success","nodes":["node-3","node-2","node-1"],"helper":"massar-cloudflared-token-install"}')
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (HelperInstallError, OSError) as error:
        print(f"Cloudflare helper bootstrap blocked: {error}", file=sys.stderr)
        raise SystemExit(6)
