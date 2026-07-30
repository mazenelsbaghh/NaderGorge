#!/usr/bin/env python3
"""Bootstrap and inspect the Massar Gluster replica-3 arbiter-1 volume."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any


class FileClusterError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--known-hosts", type=Path, required=True)
    parser.add_argument("--identity", type=Path, required=True)
    parser.add_argument("action", choices=("bootstrap", "status"))
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args()


def load_inventory(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if value.get("cluster", {}).get("name") != "massar-production":
        raise FileClusterError("wrong cluster inventory")
    if [node["id"] for node in value.get("nodes", [])] != [
        "node-1",
        "node-2",
        "node-3",
    ]:
        raise FileClusterError("expected exactly node-1, node-2, node-3")
    return value


class Transport:
    def __init__(self, known_hosts: Path, identity: Path) -> None:
        self.known_hosts = known_hosts.expanduser().resolve()
        self.identity = identity.expanduser().resolve()
        if self.identity.stat().st_mode & 0o077:
            raise FileClusterError("SSH identity must be mode 0600")

    def base(self) -> list[str]:
        return [
            "ssh",
            "-F",
            "/dev/null",
            "-i",
            str(self.identity),
            "-o",
            f"UserKnownHostsFile={self.known_hosts}",
            "-o",
            "StrictHostKeyChecking=yes",
            "-o",
            "BatchMode=yes",
            "-o",
            "IdentitiesOnly=yes",
        ]

    def run(self, address: str, script: str, timeout: int = 180) -> str:
        result = subprocess.run(
            [*self.base(), f"root@{address}", "bash", "-s"],
            input=script,
            capture_output=True,
            text=True,
            timeout=timeout,
            check=False,
            env={**os.environ, "LC_ALL": "C"},
        )
        if result.returncode:
            raise FileClusterError(result.stderr.strip() or "remote command failed")
        return result.stdout


def prepare_node(transport: Transport, node: dict[str, Any]) -> None:
    transport.run(
        node["public_address"],
        r"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test ! -f /etc/massar/files-bootstrap-complete
install -d -m 0755 -o root -g root /srv/gluster/massar/brick /srv/massar-shared
systemctl enable glusterd
systemctl restart glusterd
""",
    )


def create_volume(transport: Transport, node: dict[str, Any]) -> None:
    transport.run(
        node["public_address"],
        r"""
set -euo pipefail
gluster peer probe node-2.cluster.internal
gluster peer probe node-3.cluster.internal
for attempt in $(seq 1 30); do
  peers="$(gluster peer status | grep -c 'Peer in Cluster (Connected)' || true)"
  test "$peers" -eq 2 && break
  test "$attempt" -lt 30 || { echo 'Gluster peer quorum did not form' >&2; exit 1; }
  sleep 1
done
if ! gluster volume info massar-shared >/dev/null 2>&1; then
  gluster volume create massar-shared replica 3 arbiter 1 \
    node-1.cluster.internal:/srv/gluster/massar/brick \
    node-2.cluster.internal:/srv/gluster/massar/brick \
    node-3.cluster.internal:/srv/gluster/massar/brick force
fi
gluster volume set massar-shared cluster.quorum-type auto
gluster volume set massar-shared cluster.server-quorum-type server
gluster volume set all cluster.server-quorum-ratio 51%
gluster volume set massar-shared cluster.self-heal-daemon on
gluster volume set massar-shared cluster.data-self-heal on
gluster volume set massar-shared cluster.metadata-self-heal on
gluster volume set massar-shared cluster.entry-self-heal on
if ! gluster volume status massar-shared >/dev/null 2>&1; then
  gluster volume start massar-shared
fi
""",
    )


def mount_node(transport: Transport, node: dict[str, Any]) -> None:
    transport.run(
        node["public_address"],
        r"""
set -euo pipefail
entry='node-1.cluster.internal:/massar-shared /srv/massar-shared glusterfs defaults,_netdev,backup-volfile-servers=node-2.cluster.internal:node-3.cluster.internal,log-level=WARNING 0 0'
grep -Fqx "$entry" /etc/fstab || printf '%s\n' "$entry" >> /etc/fstab
mountpoint -q /srv/massar-shared || mount /srv/massar-shared
mountpoint -q /srv/massar-shared
""",
    )


def status(transport: Transport, node: dict[str, Any]) -> str:
    base = transport.run(
        node["public_address"],
        r"""
set -euo pipefail
systemctl is-active glusterd
mountpoint -q /srv/massar-shared
findmnt -n -o FSTYPE,SOURCE /srv/massar-shared
""",
    )
    if node["id"] == "node-1":
        base += transport.run(
            node["public_address"],
            "gluster peer status\ngluster volume status massar-shared\ngluster volume heal massar-shared info summary\n",
        )
    return base


def bootstrap(transport: Transport, nodes: list[dict[str, Any]]) -> None:
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(lambda node: prepare_node(transport, node), nodes))
    create_volume(transport, nodes[0])
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(lambda node: mount_node(transport, node), nodes))
    transport.run(
        nodes[0]["public_address"],
        r"""
set -euo pipefail
install -d -m 0775 -o root -g massar \
  /srv/massar-shared/public \
  /srv/massar-shared/protected \
  /srv/massar-shared/private \
  /srv/massar-shared/live-support \
  /srv/massar-shared/subtitles \
  /srv/massar-shared/mindmaps \
  /srv/massar-shared/.cluster-health
nonce="$(date -u +%Y%m%dT%H%M%SZ)-$RANDOM"
printf '%s\n' "$nonce" > /srv/massar-shared/.cluster-health/bootstrap
sync /srv/massar-shared/.cluster-health/bootstrap
sha256sum /srv/massar-shared/.cluster-health/bootstrap > /srv/massar-shared/.cluster-health/bootstrap.sha256
""",
    )
    expected = transport.run(
        nodes[0]["public_address"],
        "sha256sum /srv/massar-shared/.cluster-health/bootstrap | awk '{print $1}'\n",
    ).strip()
    checksums = [
        transport.run(
            node["public_address"],
            "sha256sum /srv/massar-shared/.cluster-health/bootstrap | awk '{print $1}'\n",
        ).strip()
        for node in nodes
    ]
    if checksums != [expected, expected, expected]:
        raise FileClusterError("shared file checksum differs across nodes")
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "install -m 0644 /dev/null /etc/massar/files-bootstrap-complete\n",
                ),
                nodes,
            )
        )


def main() -> int:
    args = parse_args()
    data = load_inventory(args.inventory)
    transport = Transport(args.known_hosts, args.identity)
    if args.action == "status":
        for node in data["nodes"]:
            print(f"--- {node['id']} ---")
            print(status(transport, node))
        return 0
    if args.dry_run:
        print("Would create one Gluster volume with two full data bricks and one arbiter.")
        print("Would mount the same massar-shared path on all three nodes.")
        print("Would verify one byte-identical checksum through every mount.")
        return 0
    if not args.yes:
        raise FileClusterError("bootstrap requires --dry-run or --yes")
    bootstrap(transport, data["nodes"])
    for node in data["nodes"]:
        print(f"--- {node['id']} ---")
        print(status(transport, node))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (FileClusterError, subprocess.TimeoutExpired) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
