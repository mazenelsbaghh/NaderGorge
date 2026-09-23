#!/usr/bin/env python3
"""Install the reviewed Patroni needrestart exclusion without restarting services."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from clusterctl import load_inventory, operator_transport, target


ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "deploy/production/config/needrestart/massar-patroni.conf"
DESTINATION = "/etc/needrestart/conf.d/massar-patroni.conf"
STAGING = "/tmp/massar-patroni-needrestart.conf"
EXPECTED_SHA256 = "9c6f336e0fa40c9572b3c7a516a36d9b4c28a20e376b9475f2ffd90d61419590"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    args = parser.parse_args()

    sha = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    if sha != EXPECTED_SHA256:
        raise SystemExit("reviewed Patroni policy digest changed")
    inventory = load_inventory(ROOT / "deploy/production/inventory/production.yml", require_operator_files=True)
    transport = operator_transport(inventory)
    nodes = tuple(inventory.nodes)
    if tuple(node.id for node in nodes) != ("node-1", "node-2", "node-3"):
        raise SystemExit("unexpected production node inventory")
    for node in nodes:
        remote = target(inventory, node)
        inspection = transport.run(remote, ["bash", "-lc",
            f"set -e; command -v needrestart >/dev/null; test -d /etc/needrestart/conf.d; "
            f"if test -e {DESTINATION}; then sha256sum {DESTINATION}; else printf absent; fi"],
            timeout_seconds=20)
        existing = inspection.stdout.strip()
        if existing != "absent" and not existing.startswith(sha + "  "):
            raise SystemExit(f"{node.id}: existing policy differs; inspect before replacement")
        print(json.dumps({"node": node.id, "destination": DESTINATION,
                          "sha256": sha, "existing": "same" if existing != "absent" else "absent",
                          "action": "preview" if args.dry_run else "install"}))
        if args.dry_run or existing != "absent":
            continue
        transport.copy(remote, SOURCE, STAGING, timeout_seconds=30)
        transport.run(remote, ["bash", "-lc",
            f"set -euo pipefail; printf '%s  %s\\n' '{sha}' '{STAGING}' | sha256sum -c -; "
            f"sudo -n /usr/bin/install -m 0644 -o root -g root {STAGING} {DESTINATION}; "
            f"rm -f {STAGING}; printf '%s  %s\\n' '{sha}' '{DESTINATION}' | sha256sum -c -; "
            f"test \"$(stat -c '%U:%G:%a' {DESTINATION})\" = root:root:644"],
            timeout_seconds=30)


if __name__ == "__main__":
    main()
