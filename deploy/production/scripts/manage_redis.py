#!/usr/bin/env python3
"""Bootstrap and inspect Redis replication with three Sentinels."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[3]
REDIS_TEMPLATE = ROOT / "deploy/production/config/redis/redis.conf.tmpl"
SENTINEL_TEMPLATE = ROOT / "deploy/production/config/redis/sentinel.conf.tmpl"


class RedisBootstrapError(RuntimeError):
    pass


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--known-hosts", type=Path, required=True)
    parser.add_argument("--identity", type=Path, required=True)
    parser.add_argument("--secret-dir", type=Path)
    parser.add_argument("action", choices=("bootstrap", "status", "tune"))
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args()


def inventory(path: Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if value.get("cluster", {}).get("name") != "massar-production":
        raise RedisBootstrapError("wrong cluster inventory")
    if [node["id"] for node in value.get("nodes", [])] != [
        "node-1",
        "node-2",
        "node-3",
    ]:
        raise RedisBootstrapError("expected exactly node-1, node-2, node-3")
    return value


class Transport:
    def __init__(self, known_hosts: Path, identity: Path) -> None:
        self.known_hosts = known_hosts.expanduser().resolve()
        self.identity = identity.expanduser().resolve()
        if self.identity.stat().st_mode & 0o077:
            raise RedisBootstrapError("SSH identity must be mode 0600")

    def base(self, program: str) -> list[str]:
        return [
            program,
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

    def copy(self, address: str, source: Path, destination: str) -> None:
        result = subprocess.run(
            [*self.base("scp"), str(source), f"root@{address}:{destination}"],
            capture_output=True,
            text=True,
            timeout=60,
            check=False,
        )
        if result.returncode:
            raise RedisBootstrapError(result.stderr.strip() or "copy failed")

    def run(self, address: str, script: str) -> str:
        result = subprocess.run(
            [*self.base("ssh"), f"root@{address}", "bash", "-s"],
            input=script,
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
        )
        if result.returncode:
            raise RedisBootstrapError(result.stderr.strip() or "remote command failed")
        return result.stdout


def secret_file(directory: Path) -> Path:
    path = (directory / "redis").resolve()
    if not path.is_file() or path.stat().st_mode & 0o077:
        raise RedisBootstrapError("redis secret is missing or not mode 0600")
    if len(path.read_text(encoding="utf-8").strip()) < 32:
        raise RedisBootstrapError("redis secret is too short")
    return path


def temporary_config(content: str) -> Path:
    handle = tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", delete=False)
    try:
        handle.write(content)
    finally:
        handle.close()
    path = Path(handle.name)
    path.chmod(0o600)
    return path


def install_node(
    transport: Transport,
    node: dict[str, Any],
    password_path: Path,
) -> None:
    credential = password_path.read_text(encoding="utf-8").strip()
    replica = (
        ""
        if node["id"] == "node-1"
        else "replicaof 10.77.0.11 6379"
    )
    redis_config = (
        REDIS_TEMPLATE.read_text(encoding="utf-8")
        .replace("__OVERLAY_ADDRESS__", node["overlay_address"])
        .replace("__REDIS_PASSWORD__", credential)
        .replace("__REPLICAOF_DIRECTIVE__", replica)
    )
    sentinel_config = (
        SENTINEL_TEMPLATE.read_text(encoding="utf-8")
        .replace("__OVERLAY_ADDRESS__", node["overlay_address"])
        .replace("__REDIS_PASSWORD__", credential)
    )
    redis_temp = temporary_config(redis_config)
    sentinel_temp = temporary_config(sentinel_config)
    try:
        transport.copy(node["public_address"], redis_temp, "/tmp/massar-redis.conf")
        transport.copy(
            node["public_address"], sentinel_temp, "/tmp/massar-sentinel.conf"
        )
        transport.copy(
            node["public_address"],
            password_path,
            "/tmp/massar-redis-password",
        )
    finally:
        redis_temp.unlink(missing_ok=True)
        sentinel_temp.unlink(missing_ok=True)
    transport.run(
        node["public_address"],
        r"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test ! -f /etc/massar/redis-bootstrap-complete
install -m 0640 -o redis -g redis /tmp/massar-redis.conf /etc/redis/redis.conf
install -m 0640 -o redis -g redis /tmp/massar-sentinel.conf /etc/redis/sentinel.conf
install -m 0600 -o root -g root /tmp/massar-redis-password /etc/massar/secrets/redis-password
rm -f /tmp/massar-redis.conf /tmp/massar-sentinel.conf /tmp/massar-redis-password
systemctl daemon-reload
""",
    )


def node_status(transport: Transport, node: dict[str, Any]) -> str:
    return transport.run(
        node["public_address"],
        r"""
set -euo pipefail
systemctl is-active redis-server redis-sentinel
credential="$(cat /etc/massar/secrets/redis-password)"
REDISCLI_AUTH="$credential" redis-cli -h 127.0.0.1 -p 6379 --no-auth-warning ROLE
REDISCLI_AUTH="$credential" redis-cli -h 127.0.0.1 -p 26379 --no-auth-warning \
  SENTINEL get-master-addr-by-name massar-redis
REDISCLI_AUTH="$credential" redis-cli -h 127.0.0.1 -p 6379 --no-auth-warning \
  CONFIG GET maxmemory maxmemory-policy
""",
    )


def tune_node(transport: Transport, node: dict[str, Any]) -> None:
    transport.run(
        node["public_address"],
        r"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
credential="$(cat /etc/massar/secrets/redis-password)"
REDISCLI_AUTH="$credential" redis-cli -h 127.0.0.1 -p 6379 --no-auth-warning \
  CONFIG SET maxmemory 4gb >/dev/null
REDISCLI_AUTH="$credential" redis-cli -h 127.0.0.1 -p 6379 --no-auth-warning \
  CONFIG SET maxmemory-policy noeviction >/dev/null
REDISCLI_AUTH="$credential" redis-cli -h 127.0.0.1 -p 6379 --no-auth-warning \
  CONFIG REWRITE >/dev/null
REDISCLI_AUTH="$credential" redis-cli -h 127.0.0.1 -p 6379 --no-auth-warning \
  CONFIG GET maxmemory maxmemory-policy
""",
    )


def bootstrap(
    transport: Transport,
    nodes: list[dict[str, Any]],
    password_path: Path,
) -> None:
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(lambda node: install_node(transport, node, password_path), nodes))
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "set -euo pipefail\nsystemctl enable redis-server\nsystemctl restart redis-server\n",
                ),
                nodes,
            )
        )
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "set -euo pipefail\nsystemctl enable redis-sentinel\nsystemctl restart redis-sentinel\n",
                ),
                nodes,
            )
        )
    for node in nodes:
        node_status(transport, node)
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "install -m 0644 /dev/null /etc/massar/redis-bootstrap-complete\n",
                ),
                nodes,
            )
        )


def main() -> int:
    args = arguments()
    data = inventory(args.inventory)
    transport = Transport(args.known_hosts, args.identity)
    if args.action == "status":
        for node in data["nodes"]:
            print(f"--- {node['id']} ---")
            print(node_status(transport, node))
        return 0
    if args.action == "tune":
        if args.dry_run:
            print("Would set a 4 GiB Redis ceiling with noeviction, one node at a time.")
            return 0
        if not args.yes:
            raise RedisBootstrapError("tune requires --dry-run or --yes")
        for node in data["nodes"]:
            tune_node(transport, node)
            node_status(transport, node)
        return 0
    if args.dry_run:
        print("Would configure node-1 as initial Redis primary.")
        print("Would configure node-2/node-3 as replicas and start three Sentinels.")
        print("Would require AOF, authentication, quorum two, and one healthy replica.")
        return 0
    if not args.yes:
        raise RedisBootstrapError("bootstrap requires --dry-run or --yes")
    if args.secret_dir is None:
        raise RedisBootstrapError("bootstrap requires --secret-dir")
    bootstrap(
        transport,
        data["nodes"],
        secret_file(args.secret_dir.expanduser().resolve()),
    )
    for node in data["nodes"]:
        print(f"--- {node['id']} ---")
        print(node_status(transport, node))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RedisBootstrapError, subprocess.TimeoutExpired) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
