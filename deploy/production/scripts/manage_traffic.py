#!/usr/bin/env python3
"""Drain, undrain, and verify one application node across every ingress."""

from __future__ import annotations

import argparse
import json
import subprocess
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

from ssh_transport import SshTarget, StrictSshTransport


RUNTIME_CLIENT = r"""
import socket,sys
for command in sys.argv[1:]:
    s=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM)
    s.settimeout(5)
    s.connect('/run/haproxy/admin.sock')
    s.sendall((command+'\n').encode())
    s.shutdown(socket.SHUT_WR)
    data=b''
    while True:
        chunk=s.recv(65536)
        if not chunk: break
        data+=chunk
    print(data.decode(),end='')
"""

TRAFFIC_BACKENDS = ("massar_nodes", "massar_signalr_nodes")


class TrafficError(RuntimeError):
    pass


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--node", required=True, choices=("node-1", "node-2", "node-3"))
    parser.add_argument("--timeout", type=int, default=30)
    parser.add_argument("action", choices=("drain", "undrain", "status"))
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args()


def load(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    nodes = value.get("nodes", [])
    if value.get("cluster", {}).get("name") != "massar-production":
        raise TrafficError("wrong cluster inventory")
    if [node.get("id") for node in nodes] != ["node-1", "node-2", "node-3"]:
        raise TrafficError("expected the exact three production nodes")
    return value


def runtime(
    transport: StrictSshTransport,
    target: SshTarget,
    *commands: str,
) -> str:
    if not commands:
        raise TrafficError("at least one HAProxy runtime command is required")
    return transport.run(
        target,
        ("python3", "-c", RUNTIME_CLIENT, *commands),
        # Production SSH handshakes can legitimately take more than ten
        # seconds while a node is busy building or draining.  Keep the
        # runtime socket timeout short, but do not mistake SSH latency for a
        # failed HAProxy operation.
        timeout_seconds=60,
    ).stdout


def server_status(csv_text: str, node_id: str) -> str:
    observed: dict[str, str] = {}
    for backend in TRAFFIC_BACKENDS:
        for line in csv_text.splitlines():
            fields = line.split(",")
            if len(fields) > 17 and fields[0].lstrip("# ") == backend and fields[1] == node_id:
                observed[backend] = fields[17]
                break
        else:
            raise TrafficError(f"HAProxy did not report {backend}/{node_id}")
    if len(set(observed.values())) != 1:
        raise TrafficError(f"HTTP and SignalR pools disagree for {node_id}: {observed}")
    return next(iter(observed.values()))


def main() -> int:
    args = arguments()
    inventory = load(args.inventory)
    if args.action != "status" and not (args.yes or args.dry_run):
        raise TrafficError("state changes require --yes or --dry-run")
    commands = (
        ("show stat",)
        if args.action == "status"
        else tuple(
            f"set server {backend}/{args.node} state {'drain' if args.action == 'drain' else 'ready'}"
            for backend in TRAFFIC_BACKENDS
        )
    )
    if args.dry_run:
        print(json.dumps({"action": args.action, "node": args.node, "ingressCount": 3, "status": "dry-run"}))
        return 0

    transport = StrictSshTransport(args.known_hosts, args.identity)
    ssh_user = inventory["cluster"]["ssh_user"]
    targets = [
        SshTarget(node["id"], node["public_address"], ssh_user)
        for node in inventory["nodes"]
    ]
    with ThreadPoolExecutor(max_workers=3) as pool:
        outputs = list(pool.map(lambda target: runtime(transport, target, *commands), targets))
    if args.action == "status":
        print(json.dumps({
            target.node_id: server_status(output, args.node)
            for target, output in zip(targets, outputs, strict=True)
        }, sort_keys=True))
        return 0

    expected = "DRAIN" if args.action == "drain" else "UP"
    deadline = time.monotonic() + args.timeout
    observed: dict[str, str] = {}
    while time.monotonic() < deadline:
        with ThreadPoolExecutor(max_workers=3) as pool:
            stats = list(pool.map(lambda target: runtime(transport, target, "show stat"), targets))
        observed = {
            target.node_id: server_status(output, args.node)
            for target, output in zip(targets, stats, strict=True)
        }
        if all(value.startswith(expected) for value in observed.values()):
            print(json.dumps({"action": args.action, "node": args.node, "status": "converged", "ingress": observed}, sort_keys=True))
            return 0
        time.sleep(1)
    raise TrafficError(f"HAProxy convergence timed out: {observed}")


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (TrafficError, OSError, TimeoutError, subprocess.TimeoutExpired) as exc:
        print(f"traffic operation failed: {exc}", file=__import__("sys").stderr)
        raise SystemExit(6)
