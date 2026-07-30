#!/usr/bin/env python3
"""Run the root-owned isolated PostgreSQL PITR test on one reviewed node."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from clusterctl import load_inventory
from ssh_transport import SshTarget, StrictSshTransport


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--node", choices=("node-1", "node-2", "node-3"), default="node-3")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    inventory = load_inventory(args.inventory)
    node = next(item for item in inventory.nodes if item.id == args.node)
    if args.dry_run:
        print(json.dumps({"node": node.id, "isolated": True, "recoveryTargetMinutesAgo": 5, "status": "dry-run"}))
        return 0
    if not args.yes:
        print("restore test blocked: --yes or --dry-run is required", file=sys.stderr)
        return 5
    transport = StrictSshTransport(args.known_hosts, args.identity)
    completed = transport.run(
        SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"]),
        ("sudo", "systemctl", "start", "massar-db-restore-test.service"),
        timeout_seconds=900,
        check=False,
    )
    if completed.returncode:
        journal = transport.run(
            SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"]),
            (
                "bash",
                "-lc",
                "sudo journalctl -u massar-db-restore-test.service "
                "--no-pager --output=cat -n 120 | "
                "sed -E 's/((key|secret|password)[^ =]*=)[^ ]+/\\1[REDACTED]/Ig'",
            ),
            timeout_seconds=30,
            check=False,
        )
        important = [
            line
            for line in journal.stdout.splitlines()
            if any(
                marker in line
                for marker in (
                    "ERROR:",
                    "FATAL:",
                    "PANIC:",
                    "Failed",
                    "failed",
                    "recovery",
                    "requested",
                    "server",
                    "redo",
                    "replay",
                    "restored log file",
                    "consistent recovery",
                    "recovery stopping",
                    "last completed transaction",
                )
            )
        ]
        detail = "\n".join(important[-30:])[-6000:] or "no journal error line"
        print(f"restore test failed: {detail}", file=sys.stderr)
        return 6
    print(json.dumps({"node": node.id, "status": "success"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
