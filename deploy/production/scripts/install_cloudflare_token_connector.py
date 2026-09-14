#!/usr/bin/env python3
"""Install a second, token-managed Cloudflare connector sequentially.

The input token stays in an operator-owned mode-0600 file. It is copied over
strict SSH but is never placed in a command argument, unit file, evidence, or
application configuration. The distinct service and metrics port permit a
safe overlap with the legacy connector until Cloudflare confirms three replicas.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

from clusterctl import load_inventory
from ssh_transport import SshTarget, StrictSshTransport


ROOT = Path(__file__).resolve().parents[3]
UNIT = ROOT / "deploy/production/systemd/massar-cloudflared-token.service"
CONFIG = ROOT / "deploy/production/config/cloudflared/token-config.yml.tmpl"
SERVICE_NAME = "massar-cloudflared-token"
REMOTE_DIRECTORY = "/etc/massar-cloudflared-token"
REMOTE_TOKEN = f"{REMOTE_DIRECTORY}/token"


class TokenConnectorError(RuntimeError):
    pass


def validate_token_file(path: Path) -> None:
    if not path.is_file():
        raise TokenConnectorError("--token-file must name an existing regular file")
    if path.is_symlink():
        raise TokenConnectorError("--token-file must not be a symbolic link")
    if path.stat().st_mode & 0o077:
        raise TokenConnectorError("--token-file must be mode 0600")
    if not path.read_bytes().strip():
        raise TokenConnectorError("--token-file must not be empty")


def targets(inventory) -> tuple[SshTarget, ...]:
    by_id = {node.id: node for node in inventory.nodes}
    if set(by_id) != {"node-1", "node-2", "node-3"}:
        raise TokenConnectorError("installer requires exactly the approved three-node inventory")
    return tuple(
        SshTarget(node_id, by_id[node_id].public_address, inventory.cluster["ssh_user"])
        for node_id in ("node-3", "node-2", "node-1")
    )


def preflight_script() -> str:
    return " ".join(
        (
            "set -euo pipefail;",
            "test \"$(cat /etc/massar/cluster-id)\" = massar-production;",
            "test -x /usr/bin/docker;",
            "sudo -n /usr/bin/docker info >/dev/null;",
            "curl --fail --silent -H 'Host: massar-academy.net' http://127.0.0.1:8088/ >/dev/null;",
        )
    )


def install_script() -> str:
    return " ".join(
        (
            "set -euo pipefail;",
            "trap 'rm -f /tmp/massar-cloudflared-token /tmp/massar-cloudflared-token.service /tmp/massar-cloudflared-token.yml' EXIT;",
            "test \"$(cat /etc/massar/cluster-id)\" = massar-production;",
            "sudo -n /usr/local/sbin/massar-cloudflared-token-install;",
        )
    )


def install(inventory, transport: StrictSshTransport, token_file: Path) -> None:
    if not UNIT.is_file() or not CONFIG.is_file():
        raise TokenConnectorError("reviewed token connector assets are missing")
    validate_token_file(token_file)
    for target in targets(inventory):
        transport.run(target, ("bash", "-lc", preflight_script()), timeout_seconds=60)
        transport.copy(target, token_file, "/tmp/massar-cloudflared-token", timeout_seconds=60)
        transport.run(target, ("bash", "-lc", install_script()), timeout_seconds=120)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--token-file", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = arguments()
    inventory = load_inventory(args.inventory)
    validate_token_file(args.token_file)
    targets(inventory)
    if args.dry_run:
        print('{"status":"dry-run","nodes":["node-3","node-2","node-1"],"service":"massar-cloudflared-token"}')
        return 0
    if not args.yes:
        raise TokenConnectorError("installer requires --yes or --dry-run")
    install(inventory, StrictSshTransport(args.known_hosts, args.identity), args.token_file)
    print('{"status":"success","nodes":["node-3","node-2","node-1"],"service":"massar-cloudflared-token"}')
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (TokenConnectorError, OSError) as exc:
        print(f"Cloudflare token connector blocked: {exc}", file=sys.stderr)
        raise SystemExit(6)
