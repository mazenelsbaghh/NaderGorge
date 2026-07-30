#!/usr/bin/env python3
"""Enable pgBackRest WAL archiving through Patroni, one healthy node at a time."""

from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path
from typing import Any

from clusterctl import Inventory, Node, load_inventory
from ssh_transport import SshTarget, StrictSshTransport


ARCHIVE_COMMAND = "pgbackrest --stanza=massar archive-push %p"


class ArchiveConfigurationError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("action", choices=("apply", "status"))
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args()


def target(inventory: Inventory, node: Node) -> SshTarget:
    return SshTarget(node.id, node.public_address, str(inventory.cluster["ssh_user"]))


def request_json(
    transport: StrictSshTransport,
    inventory: Inventory,
    node: Node,
    path: str,
) -> dict[str, Any]:
    completed = transport.run(
        target(inventory, node),
        ("curl", "--fail", "--silent", f"http://127.0.0.1:8008{path}"),
        timeout_seconds=30,
    )
    value = json.loads(completed.stdout)
    if not isinstance(value, dict):
        raise ArchiveConfigurationError(f"{node.id} returned invalid Patroni JSON")
    return value


def cluster_state(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> dict[str, dict[str, Any]]:
    state = {
        node.id: request_json(transport, inventory, node, "/patroni")
        for node in inventory.nodes
    }
    primaries = [
        node_id
        for node_id, value in state.items()
        if value.get("role") in {"primary", "master"}
    ]
    if len(primaries) != 1:
        raise ArchiveConfigurationError("Patroni must have exactly one primary")
    if any(value.get("state") != "running" for value in state.values()):
        raise ArchiveConfigurationError("all Patroni members must be running")
    return state


def wait_healthy(
    transport: StrictSshTransport,
    inventory: Inventory,
    *,
    attempts: int = 60,
) -> dict[str, dict[str, Any]]:
    last_error = "cluster did not become healthy"
    for attempt in range(attempts):
        try:
            return cluster_state(transport, inventory)
        except (ArchiveConfigurationError, json.JSONDecodeError, OSError) as exc:
            last_error = str(exc)
        if attempt + 1 < attempts:
            time.sleep(2)
    raise ArchiveConfigurationError(last_error)


def archive_config(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> dict[str, Any]:
    return request_json(transport, inventory, inventory.nodes[0], "/config")


def verify_archive_config(value: dict[str, Any]) -> None:
    parameters = value.get("postgresql", {}).get("parameters", {})
    if str(parameters.get("archive_mode", "")).lower() != "on":
        raise ArchiveConfigurationError("Patroni archive_mode is not on")
    if parameters.get("archive_command") != ARCHIVE_COMMAND:
        raise ArchiveConfigurationError("Patroni archive_command is not pgBackRest")
    if int(parameters.get("archive_timeout", 0)) != 300:
        raise ArchiveConfigurationError("Patroni archive_timeout is not 300 seconds")


def patch_config(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> None:
    payload = json.dumps(
        {
            "postgresql": {
                "parameters": {
                    "archive_mode": "on",
                    "archive_command": ARCHIVE_COMMAND,
                    "archive_timeout": 300,
                }
            }
        },
        separators=(",", ":"),
    )
    transport.run(
        target(inventory, inventory.nodes[0]),
        (
            "curl",
            "--fail",
            "--silent",
            "--show-error",
            "-X",
            "PATCH",
            "-H",
            "Content-Type: application/json",
            "--data",
            payload,
            "http://127.0.0.1:8008/config",
        ),
        timeout_seconds=30,
    )


def apply(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> None:
    state = wait_healthy(transport, inventory)
    primary = next(
        node_id
        for node_id, value in state.items()
        if value.get("role") in {"primary", "master"}
    )
    patch_config(transport, inventory)
    order = [
        *(node for node in inventory.nodes if node.id != primary),
        next(node for node in inventory.nodes if node.id == primary),
    ]
    for node in order:
        current = request_json(transport, inventory, node, "/patroni")
        if current.get("pending_restart"):
            transport.run(
                target(inventory, node),
                ("sudo", "/usr/bin/systemctl", "restart", "patroni.service"),
                timeout_seconds=180,
            )
            wait_healthy(transport, inventory)
    verify_archive_config(archive_config(transport, inventory))
    final_state = wait_healthy(transport, inventory)
    if any(value.get("pending_restart") for value in final_state.values()):
        raise ArchiveConfigurationError("a Patroni member still requires restart")


def main() -> int:
    args = parse_args()
    inventory = load_inventory(args.inventory, require_operator_files=True)
    transport = StrictSshTransport(args.known_hosts, args.identity)
    if args.action == "status":
        verify_archive_config(archive_config(transport, inventory))
        wait_healthy(transport, inventory)
        print(json.dumps({"status": "success", "archiveTimeoutSeconds": 300}))
        return 0
    if args.dry_run:
        print(
            json.dumps(
                {
                    "status": "dry-run",
                    "restartOrder": "replicas-first-current-primary-last",
                    "archiveCommand": ARCHIVE_COMMAND,
                }
            )
        )
        return 0
    if not args.yes:
        raise ArchiveConfigurationError("apply requires --dry-run or --yes")
    apply(transport, inventory)
    print(json.dumps({"status": "success", "archiveTimeoutSeconds": 300}))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ArchiveConfigurationError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
