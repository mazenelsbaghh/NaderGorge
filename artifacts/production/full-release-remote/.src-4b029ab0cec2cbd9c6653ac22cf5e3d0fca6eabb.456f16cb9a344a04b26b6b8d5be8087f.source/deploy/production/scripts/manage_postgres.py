#!/usr/bin/env python3
"""Bootstrap and inspect the Massar etcd/Patroni PostgreSQL data plane."""

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
ETCD_TEMPLATE = ROOT / "deploy/production/config/etcd/massar-etcd.env.tmpl"
PATRONI_TEMPLATE = ROOT / "deploy/production/config/patroni/patroni.yml.tmpl"
ROLE_CALLBACK = ROOT / "deploy/production/config/patroni/massar-patroni-role-change"
HAPROXY_CONFIG = ROOT / "deploy/production/config/haproxy/postgres.cfg"
ETCD_AUTH_BOOTSTRAP = ROOT / "deploy/production/scripts/bootstrap_etcd_auth.py"


class BootstrapError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--known-hosts", type=Path, required=True)
    parser.add_argument("--identity", type=Path, required=True)
    parser.add_argument("--secret-dir", type=Path, required=True)
    parser.add_argument("action", choices=("bootstrap", "status"))
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args()


def load_inventory(path: Path) -> dict[str, Any]:
    inventory = json.loads(path.read_text(encoding="utf-8"))
    if inventory.get("cluster", {}).get("name") != "massar-production":
        raise BootstrapError("inventory is not the Massar production cluster")
    nodes = inventory.get("nodes", [])
    if [node.get("id") for node in nodes] != ["node-1", "node-2", "node-3"]:
        raise BootstrapError("inventory must contain node-1, node-2, node-3 in order")
    return inventory


class RootTransport:
    def __init__(self, known_hosts: Path, identity: Path) -> None:
        self.known_hosts = known_hosts.expanduser().resolve()
        self.identity = identity.expanduser().resolve()
        if not self.known_hosts.is_file() or not self.identity.is_file():
            raise BootstrapError("known-hosts and identity files are required")
        if self.identity.stat().st_mode & 0o077:
            raise BootstrapError("SSH identity must be mode 0600")

    def _base(self, program: str) -> list[str]:
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
            "-o",
            "ConnectTimeout=10",
        ]

    def run(self, address: str, script: str, timeout: int = 120) -> str:
        result = subprocess.run(
            [*self._base("ssh"), f"root@{address}", "bash", "-s"],
            input=script,
            text=True,
            capture_output=True,
            timeout=timeout,
            check=False,
            env={**os.environ, "LC_ALL": "C"},
        )
        if result.returncode:
            raise BootstrapError(result.stderr.strip() or "remote command failed")
        return result.stdout

    def copy(self, address: str, source: Path, destination: str) -> None:
        result = subprocess.run(
            [*self._base("scp"), str(source), f"root@{address}:{destination}"],
            text=True,
            capture_output=True,
            timeout=60,
            check=False,
            env={**os.environ, "LC_ALL": "C"},
        )
        if result.returncode:
            raise BootstrapError(result.stderr.strip() or "secure copy failed")


def required_secret(secret_dir: Path, name: str) -> Path:
    path = (secret_dir / name).resolve()
    if not path.is_file():
        raise BootstrapError(f"missing external secret file: {name}")
    if path.stat().st_mode & 0o077:
        raise BootstrapError(f"external secret file is not mode 0600: {name}")
    value = path.read_text(encoding="utf-8").strip()
    if len(value) < 32 or "\n" in value:
        raise BootstrapError(f"external secret file is invalid: {name}")
    return path


def render(template: Path, replacements: dict[str, str]) -> str:
    result = template.read_text(encoding="utf-8")
    for placeholder, value in replacements.items():
        result = result.replace(placeholder, value)
    unresolved = [word for word in result.split() if "__" in word]
    if unresolved:
        raise BootstrapError(f"unresolved template placeholder in {template.name}")
    return result


def copy_bytes(transport: RootTransport, address: str, content: str, remote: str) -> None:
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", delete=False) as handle:
        handle.write(content)
        temporary = Path(handle.name)
    try:
        temporary.chmod(0o600)
        transport.copy(address, temporary, remote)
    finally:
        temporary.unlink(missing_ok=True)


def install_node(
    transport: RootTransport,
    node: dict[str, Any],
    secret_dir: Path,
    pki_dir: Path,
) -> None:
    node_id = node["id"]
    address = node["public_address"]
    overlay = node["overlay_address"]
    etcd_config = render(
        ETCD_TEMPLATE,
        {"__NODE_ID__": node_id, "__OVERLAY_ADDRESS__": overlay},
    )
    passwords = {
        "__ETCD_PATRONI_PASSWORD__": required_secret(
            secret_dir, "etcd-patroni"
        ).read_text(encoding="utf-8").strip(),
        "__POSTGRES_SUPERUSER_PASSWORD__": required_secret(
            secret_dir, "postgres-superuser"
        ).read_text(encoding="utf-8").strip(),
        "__POSTGRES_REPLICATION_PASSWORD__": required_secret(
            secret_dir, "postgres-replication"
        ).read_text(encoding="utf-8").strip(),
        "__POSTGRES_REWIND_PASSWORD__": required_secret(
            secret_dir, "postgres-rewind"
        ).read_text(encoding="utf-8").strip(),
        "__NODE_ID__": node_id,
        "__OVERLAY_ADDRESS__": overlay,
    }
    patroni_config = render(PATRONI_TEMPLATE, passwords)

    files = {
        "ca.crt": pki_dir / "ca.crt",
        "server.crt": pki_dir / f"{node_id}.crt",
        "server.key": pki_dir / f"{node_id}.key",
    }
    for source in files.values():
        if not source.is_file():
            raise BootstrapError(f"missing PKI file: {source.name}")

    copy_bytes(transport, address, etcd_config, "/tmp/massar-etcd.env")
    copy_bytes(transport, address, patroni_config, "/tmp/massar-patroni.yml")
    transport.copy(address, ROLE_CALLBACK, "/tmp/massar-patroni-role-change")
    transport.copy(address, HAPROXY_CONFIG, "/tmp/massar-postgres-haproxy.cfg")
    for destination, source in files.items():
        transport.copy(address, source, f"/tmp/massar-etcd-{destination}")
    transport.copy(
        address,
        required_secret(secret_dir, "postgres-app"),
        "/tmp/massar-postgres-app-password",
    )
    transport.copy(
        address,
        required_secret(secret_dir, "postgres-superuser"),
        "/tmp/massar-postgres-superuser-password",
    )

    transport.run(
        address,
        r"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test ! -f /etc/massar/postgres-bootstrap-complete
usermod -aG massar etcd
usermod -aG massar postgres
install -d -m 0750 -o root -g massar /etc/massar
install -d -m 0755 -o root -g root /etc/massar/pki/etcd
install -d -m 0700 -o etcd -g etcd /var/lib/etcd/massar
install -d -m 0700 -o postgres -g postgres /var/lib/postgresql/16/massar
install -m 0640 -o root -g etcd /tmp/massar-etcd.env /etc/default/etcd
install -m 0644 -o root -g root /tmp/massar-etcd-ca.crt /etc/massar/pki/etcd/ca.crt
install -m 0644 -o root -g root /tmp/massar-etcd-server.crt /etc/massar/pki/etcd/server.crt
install -m 0640 -o root -g etcd /tmp/massar-etcd-server.key /etc/massar/pki/etcd/server.key
install -m 0600 -o postgres -g postgres /tmp/massar-patroni.yml /etc/patroni/config.yml
install -m 0755 -o root -g root /tmp/massar-patroni-role-change /usr/local/sbin/massar-patroni-role-change
install -m 0644 -o root -g root /tmp/massar-postgres-haproxy.cfg /etc/haproxy/haproxy.cfg
install -m 0600 -o root -g root /tmp/massar-postgres-app-password /etc/massar/secrets/postgres-app-password
install -m 0600 -o root -g root /tmp/massar-postgres-superuser-password /etc/massar/secrets/postgres-superuser-password
rm -f /tmp/massar-etcd-* /tmp/massar-patroni.yml /tmp/massar-patroni-role-change /tmp/massar-postgres-haproxy.cfg /tmp/massar-postgres-app-password /tmp/massar-postgres-superuser-password
haproxy -c -f /etc/haproxy/haproxy.cfg
systemctl daemon-reload
""",
    )


ETCD_ROOT_FLAGS = (
    'ETCDCTL_API=3 ETCDCTL_USER=root ETCDCTL_PASSWORD="$(cat /etc/massar/secrets/etcd-root-password)" etcdctl '
    "--endpoints=https://10.77.0.11:2379,https://10.77.0.12:2379,https://10.77.0.13:2379 "
    "--cacert=/etc/massar/pki/etcd/ca.crt"
)


def enable_etcd_auth(
    transport: RootTransport,
    node: dict[str, Any],
    pki_dir: Path,
    secret_dir: Path,
) -> None:
    address = node["public_address"]
    transport.copy(address, pki_dir / "root.crt", "/tmp/massar-etcd-root.crt")
    transport.copy(address, pki_dir / "root.key", "/tmp/massar-etcd-root.key")
    transport.copy(
        address,
        required_secret(secret_dir, "etcd-root"),
        "/tmp/massar-etcd-root-password",
    )
    transport.copy(
        address,
        required_secret(secret_dir, "etcd-patroni"),
        "/tmp/massar-etcd-patroni-password",
    )
    transport.copy(address, ETCD_AUTH_BOOTSTRAP, "/tmp/bootstrap-etcd-auth.py")
    transport.run(
        address,
        """
set -euo pipefail
install -m 0600 -o root -g root /tmp/massar-etcd-root.crt /etc/massar/pki/etcd/root.crt
install -m 0600 -o root -g root /tmp/massar-etcd-root.key /etc/massar/pki/etcd/root.key
install -m 0600 -o root -g root /tmp/massar-etcd-root-password /etc/massar/secrets/etcd-root-password
python3 /tmp/bootstrap-etcd-auth.py /tmp/massar-etcd-root-password /tmp/massar-etcd-patroni-password
rm -f /tmp/massar-etcd-root.crt /tmp/massar-etcd-root.key /tmp/massar-etcd-root-password /tmp/massar-etcd-patroni-password /tmp/bootstrap-etcd-auth.py
""",
    )


def initialize_application_database(
    transport: RootTransport, node: dict[str, Any]
) -> None:
    transport.run(
        node["public_address"],
        r"""
set -euo pipefail
super_password="$(tr -d '\n' </etc/massar/secrets/postgres-superuser-password)"
app_password="$(tr -d '\n' </etc/massar/secrets/postgres-app-password)"
export PGPASSWORD="$super_password"
if ! psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -tAc "select 1 from pg_roles where rolname='massar_app'" | grep -qx 1; then
  printf "CREATE ROLE massar_app LOGIN PASSWORD '%s';\n" "$app_password" |
    psql -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 6432 -U postgres -d postgres >/dev/null
fi
if ! psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -tAc "select 1 from pg_database where datname='massar_platform'" | grep -qx 1; then
  createdb -h 127.0.0.1 -p 6432 -U postgres -O massar_app massar_platform
fi
psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -tAc \
  "select datname || ':' || pg_get_userbyid(datdba) from pg_database where datname='massar_platform'"
""",
    )


def status(transport: RootTransport, node: dict[str, Any]) -> str:
    etcd_status = (
        f"{ETCD_ROOT_FLAGS} endpoint status --write-out=table"
        if node["id"] == "node-1"
        else "echo 'etcd quorum status is collected once from node-1'"
    )
    return transport.run(
        node["public_address"],
        f"""
set -euo pipefail
systemctl is-active etcd patroni haproxy
{etcd_status}
patronictl -c /etc/patroni/config.yml list
""",
    )


def bootstrap(
    transport: RootTransport,
    inventory: dict[str, Any],
    secret_dir: Path,
) -> None:
    nodes = inventory["nodes"]
    pki_dir = (secret_dir / "etcd-pki").resolve()
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(pool.map(lambda node: install_node(transport, node, secret_dir, pki_dir), nodes))
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "set -euo pipefail\nsystemctl enable etcd\nsystemctl restart etcd\n",
                ),
                nodes,
            )
        )
    enable_etcd_auth(transport, nodes[0], pki_dir, secret_dir)
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "set -euo pipefail\nsystemctl enable --now patroni\n",
                    timeout=180,
                ),
                nodes,
            )
        )
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "set -euo pipefail\nsystemctl enable --now haproxy\n",
                ),
                nodes,
            )
        )
    initialize_application_database(transport, nodes[0])
    with ThreadPoolExecutor(max_workers=3) as pool:
        list(
            pool.map(
                lambda node: transport.run(
                    node["public_address"],
                    "set -euo pipefail\ninstall -m 0644 /dev/null /etc/massar/postgres-bootstrap-complete\n",
                ),
                nodes,
            )
        )


def main() -> int:
    args = parse_args()
    inventory = load_inventory(args.inventory)
    transport = RootTransport(args.known_hosts, args.identity)
    if args.action == "status":
        for node in inventory["nodes"]:
            print(f"--- {node['id']} ---")
            print(status(transport, node))
        return 0
    if args.dry_run:
        print("Would install node-specific mTLS etcd and Patroni configs on node-1..3.")
        print("Would start three-member etcd, enable certificate auth, then start Patroni.")
        print("Would expose only Patroni /primary through local HAProxy port 6432.")
        print("Would create massar_app/massar_platform only after one writer is healthy.")
        return 0
    if not args.yes:
        raise BootstrapError("bootstrap requires --dry-run or --yes")
    bootstrap(transport, inventory, args.secret_dir.expanduser().resolve())
    for node in inventory["nodes"]:
        print(f"--- {node['id']} ---")
        print(status(transport, node))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (BootstrapError, subprocess.TimeoutExpired) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
