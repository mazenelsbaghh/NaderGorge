#!/usr/bin/env python3
"""Bootstrap and inspect the three-node internal Garage backup bucket."""

from __future__ import annotations

import argparse
import json
import re
import secrets
import shutil
import stat
import subprocess
import sys
import tempfile
import time
from pathlib import Path

from clusterctl import Inventory, Node, load_inventory
from ssh_transport import SshTarget, SshTransportError, StrictSshTransport


ROOT = Path(__file__).resolve().parents[3]
GARAGE_TEMPLATE = ROOT / "deploy/production/config/garage/garage.toml.tmpl"
GARAGE_SERVICE = ROOT / "deploy/production/systemd/massar-backup-bucket.service"
TLS_PROXY_CONFIG = ROOT / "deploy/production/config/garage/haproxy-tls.cfg"
TLS_PROXY_SERVICE = ROOT / "deploy/production/systemd/massar-backup-tls-proxy.service"
PGBACKREST_TEMPLATE = ROOT / "deploy/production/config/pgbackrest/pgbackrest.conf.tmpl"
RELEASE_GATE_SUDOERS = (
    ROOT / "deploy/production/config/sudoers/massar-release-migration-gate"
)
NORMALIZATION_SUDOERS = (
    ROOT
    / "deploy/production/config/sudoers/massar-current-release-normalization"
)
LEGACY_SEAL_SUDOERS = (
    ROOT / "deploy/production/config/sudoers/massar-legacy-release-seal"
)
IMMUTABLE_RELEASE_SUDOERS = (
    ROOT / "deploy/production/config/sudoers/massar-immutable-release-install"
)
REMOTE_BUILDER_SUDOERS = ROOT / "deploy/production/config/sudoers/massar-remote-builder"
LEGACY_CUTOVER_GLUSTER_SUDOERS = (
    ROOT / "deploy/production/config/sudoers/massar-legacy-cutover-gluster"
)
BACKUP_SCRIPTS = {
    "backup_database.sh": "/usr/local/sbin/massar-backup-database",
    "backup_files.sh": "/usr/local/sbin/massar-backup-files",
    "prune_file_backups.sh": "/usr/local/sbin/massar-prune-file-backups",
    "restore_files_sample.sh": "/usr/local/sbin/massar-restore-files-sample",
    "restore_database_sample.sh": "/usr/local/lib/massar/restore_database_sample.sh",
    "run_database_restore_drill.sh": "/usr/local/lib/massar/run_database_restore_drill.sh",
    "initialize_backup_repository.sh": "/usr/local/sbin/massar-initialize-backup-repository",
    "initialize_database_backup.sh": "/usr/local/sbin/massar-initialize-database-backup",
    "prepare_pitr_probe.sh": "/usr/local/sbin/massar-prepare-pitr-probe",
    "prepare_release_migration_gate.py":
        "/usr/local/sbin/massar-produce-release-migration-gate",
    "normalize_current_release_root.py":
        "/usr/local/sbin/massar-normalize-current-release",
    "seal_legacy_release_root.py":
        "/usr/local/sbin/massar-seal-legacy-release",
    "install_immutable_release.py":
        "/usr/local/sbin/massar-install-immutable-release",
    "remote_builder_executor.py": "/usr/local/sbin/massar-remote-builder",
}
BACKUP_UNITS = (
    "massar-backup-repository-init.service",
    "massar-pgbackrest-init.service",
    "massar-pitr-probe.service",
    "massar-pgbackrest-diff.service",
    "massar-pgbackrest-diff.timer",
    "massar-pgbackrest-full.service",
    "massar-pgbackrest-full.timer",
    "massar-files-backup.service",
    "massar-files-backup.timer",
    "massar-files-prune.service",
    "massar-files-prune.timer",
    "massar-db-restore-scheduled.service",
    "massar-db-restore-test.service",
    "massar-db-restore-test.timer",
    "massar-files-restore-test.service",
    "massar-files-restore-test.timer",
)
BACKUP_TIMERS = (
    "massar-pgbackrest-diff.timer",
    "massar-pgbackrest-full.timer",
    "massar-files-backup.timer",
    "massar-files-prune.timer",
    "massar-db-restore-test.timer",
    "massar-files-restore-test.timer",
)
GARAGE_IMAGE = (
    "dxflrs/garage@"
    "sha256:dac0c92add4f1a0b41035e94b41036a270ffbe88a37c7ac9c3f19e6dc5bdccf2"
)
TLS_PROXY_IMAGE = (
    "haproxy@"
    "sha256:82b76748bcf0f2f8e9e48a4bcb83667df06493ea8fd2a699056a45e64ec6d08f"
)
BUCKET = "massar-backups"
KEY_NAME = "massar-backup-client"
CAPACITY_RE = re.compile(r"^[1-9][0-9]*(?:GB|TB)$")
KEY_ID_RE = re.compile(r"^Key ID:\s*(\S+)\s*$", re.MULTILINE)
KEY_SECRET_RE = re.compile(r"^Secret key:\s*(\S+)\s*$", re.MULTILINE)


class BackupBucketError(RuntimeError):
    pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--secret-dir", type=Path)
    parser.add_argument("--capacity-per-node")
    parser.add_argument("--node", choices=("node-1", "node-2", "node-3", "all"), default="all")
    parser.add_argument(
        "action",
        choices=(
            "activate-schedules",
            "bootstrap",
            "initialize",
            "plan",
            "schedule-status",
            "status",
            "sync-clients",
        ),
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args()


def target(inventory: Inventory, node: Node) -> SshTarget:
    return SshTarget(node.id, node.public_address, str(inventory.cluster["ssh_user"]))


def ensure_secret(directory: Path, name: str, *, hexadecimal: bool = False) -> Path:
    directory.mkdir(mode=0o700, parents=True, exist_ok=True)
    directory.chmod(0o700)
    path = directory / name
    if not path.exists():
        value = secrets.token_hex(32) if hexadecimal else secrets.token_urlsafe(48)
        path.write_text(value + "\n", encoding="utf-8")
        path.chmod(0o600)
    mode = stat.S_IMODE(path.stat().st_mode)
    value = path.read_text(encoding="utf-8").strip()
    if mode != 0o600 or len(value) < 32 or "\n" in value:
        raise BackupBucketError(f"protected secret file is invalid: {name}")
    return path


def ensure_tls_assets(directory: Path) -> tuple[Path, Path]:
    ca_certificate = directory / "backup-ca.crt"
    tls_bundle = directory / "backup-tls.pem"
    if ca_certificate.is_file() and tls_bundle.is_file():
        if tls_bundle.stat().st_mode & 0o077:
            raise BackupBucketError("protected TLS bundle must be mode 0600")
        return ca_certificate, tls_bundle
    with tempfile.TemporaryDirectory() as temporary_name:
        temporary = Path(temporary_name)
        ca_key = temporary / "ca.key"
        ca_crt = temporary / "ca.crt"
        server_key = temporary / "server.key"
        server_csr = temporary / "server.csr"
        server_crt = temporary / "server.crt"
        extensions = temporary / "server.ext"
        extensions.write_text(
            "subjectAltName=IP:127.0.0.1,DNS:backup-s3.local\n"
            "extendedKeyUsage=serverAuth\n",
            encoding="utf-8",
        )
        commands = (
            (
                "openssl",
                "req",
                "-x509",
                "-newkey",
                "rsa:3072",
                "-nodes",
                "-keyout",
                str(ca_key),
                "-out",
                str(ca_crt),
                "-subj",
                "/CN=Massar Internal Backup CA",
                "-days",
                "3650",
                "-sha256",
            ),
            (
                "openssl",
                "req",
                "-newkey",
                "rsa:3072",
                "-nodes",
                "-keyout",
                str(server_key),
                "-out",
                str(server_csr),
                "-subj",
                "/CN=backup-s3.local",
            ),
            (
                "openssl",
                "x509",
                "-req",
                "-in",
                str(server_csr),
                "-CA",
                str(ca_crt),
                "-CAkey",
                str(ca_key),
                "-CAcreateserial",
                "-out",
                str(server_crt),
                "-days",
                "825",
                "-sha256",
                "-extfile",
                str(extensions),
            ),
        )
        for command in commands:
            subprocess.run(command, check=True, capture_output=True, text=True)
        directory.mkdir(mode=0o700, parents=True, exist_ok=True)
        shutil.copyfile(ca_key, directory / "backup-ca.key")
        shutil.copyfile(ca_crt, ca_certificate)
        tls_bundle.write_text(
            server_crt.read_text(encoding="utf-8")
            + server_key.read_text(encoding="utf-8"),
            encoding="utf-8",
        )
    (directory / "backup-ca.key").chmod(0o600)
    ca_certificate.chmod(0o644)
    tls_bundle.chmod(0o600)
    return ca_certificate, tls_bundle


def render_config(node: Node) -> str:
    rendered = GARAGE_TEMPLATE.read_text(encoding="utf-8").replace(
        "__OVERLAY_ADDRESS__", node.overlay_address
    )
    if "__" in rendered:
        raise BackupBucketError("unresolved Garage configuration placeholder")
    return rendered


def copy_content(
    transport: StrictSshTransport,
    remote: SshTarget,
    content: str,
    destination: str,
) -> None:
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", delete=False) as handle:
        handle.write(content)
        temporary = Path(handle.name)
    try:
        temporary.chmod(0o600)
        transport.copy(remote, temporary, destination)
    finally:
        temporary.unlink(missing_ok=True)


def install_node(
    transport: StrictSshTransport,
    inventory: Inventory,
    node: Node,
    secret_dir: Path,
) -> None:
    remote = target(inventory, node)
    copy_content(transport, remote, render_config(node), "/tmp/massar-garage.toml")
    transport.copy(remote, GARAGE_SERVICE, "/tmp/massar-backup-bucket.service")
    transport.copy(remote, TLS_PROXY_CONFIG, "/tmp/massar-backup-haproxy.cfg")
    transport.copy(remote, TLS_PROXY_SERVICE, "/tmp/massar-backup-tls-proxy.service")
    transport.copy(remote, secret_dir / "backup-ca.crt", "/tmp/massar-backup-ca.crt")
    transport.copy(remote, secret_dir / "backup-tls.pem", "/tmp/massar-backup-tls.pem")
    for name in ("backup-rpc", "backup-admin", "backup-metrics"):
        transport.copy(remote, secret_dir / name, f"/tmp/massar-{name}")
    script = f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
sudo /usr/bin/install -d -m 0750 -o root -g massar /etc/massar /etc/massar/secrets
sudo /usr/bin/install -d -m 0755 -o root -g root /etc/massar/pki
sudo /usr/bin/install -d -m 0700 -o root -g root /var/lib/massar-backup/meta /var/lib/massar-backup/data
sudo /usr/bin/install -m 0640 -o root -g massar /tmp/massar-garage.toml /etc/massar/garage.toml
sudo /usr/bin/install -m 0600 -o root -g root /tmp/massar-backup-rpc /etc/massar/secrets/backup-rpc
sudo /usr/bin/install -m 0600 -o root -g root /tmp/massar-backup-admin /etc/massar/secrets/backup-admin
sudo /usr/bin/install -m 0600 -o root -g root /tmp/massar-backup-metrics /etc/massar/secrets/backup-metrics
sudo /usr/bin/install -m 0644 -o root -g root /tmp/massar-backup-bucket.service /etc/systemd/system/massar-backup-bucket.service
sudo /usr/bin/install -m 0644 -o root -g root /tmp/massar-backup-haproxy.cfg /etc/massar/backup-haproxy.cfg
sudo /usr/bin/install -m 0644 -o root -g root /tmp/massar-backup-tls-proxy.service /etc/systemd/system/massar-backup-tls-proxy.service
sudo /usr/bin/install -m 0644 -o root -g root /tmp/massar-backup-ca.crt /etc/massar/pki/backup-ca.crt
sudo /usr/bin/install -m 0600 -o root -g root /tmp/massar-backup-tls.pem /etc/massar/secrets/backup-tls.pem
rm -f /tmp/massar-garage.toml /tmp/massar-backup-rpc /tmp/massar-backup-admin /tmp/massar-backup-metrics /tmp/massar-backup-bucket.service
rm -f /tmp/massar-backup-haproxy.cfg /tmp/massar-backup-tls-proxy.service /tmp/massar-backup-ca.crt /tmp/massar-backup-tls.pem
sudo /usr/bin/docker pull {GARAGE_IMAGE} >/dev/null
sudo /usr/bin/docker pull {TLS_PROXY_IMAGE} >/dev/null
sudo /usr/bin/systemctl daemon-reload
sudo /usr/bin/systemctl enable --now massar-backup-bucket.service
sudo /usr/bin/systemctl enable --now massar-backup-tls-proxy.service
"""
    transport.run(remote, ("bash", "-lc", script), timeout_seconds=300)


def garage(
    transport: StrictSshTransport,
    inventory: Inventory,
    node: Node,
    *arguments: str,
    check: bool = True,
) -> str:
    completed = transport.run(
        target(inventory, node),
        (
            "sudo",
            "/usr/bin/docker",
            "exec",
            "massar-backup-bucket",
            "/garage",
            "--rpc-secret-file",
            "/run/secrets/garage-rpc",
            *arguments,
        ),
        timeout_seconds=120,
        check=check,
    )
    return completed.stdout


def bucket_exists(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> bool:
    completed = transport.run(
        target(inventory, inventory.nodes[0]),
        (
            "sudo",
            "/usr/bin/docker",
            "exec",
            "massar-backup-bucket",
            "/garage",
            "--rpc-secret-file",
            "/run/secrets/garage-rpc",
            "bucket",
            "info",
            BUCKET,
        ),
        timeout_seconds=60,
        check=False,
    )
    return completed.returncode == 0


def wait_for_nodes(
    transport: StrictSshTransport,
    inventory: Inventory,
    coordinator: Node,
    node_ids: tuple[str, ...],
) -> None:
    for attempt in range(30):
        output = garage(transport, inventory, coordinator, "status")
        if all(node_id[:16] in output for node_id in node_ids):
            return
        if attempt == 29:
            raise BackupBucketError("three Garage nodes did not form one cluster")
        time.sleep(2)


def store_client_secrets(secret_dir: Path, output: str) -> tuple[Path, Path]:
    key_id = KEY_ID_RE.search(output)
    key_secret = KEY_SECRET_RE.search(output)
    if not key_id or not key_secret:
        raise BackupBucketError("Garage did not return a parseable client key")
    paths = (
        secret_dir / "backup-s3-access",
        secret_dir / "backup-s3-secret",
    )
    for path, value in zip(paths, (key_id.group(1), key_secret.group(1)), strict=True):
        path.write_text(value + "\n", encoding="utf-8")
        path.chmod(0o600)
    return paths


def initialize_layout(
    transport: StrictSshTransport,
    inventory: Inventory,
    capacity: str,
    secret_dir: Path,
) -> None:
    coordinator = inventory.nodes[0]
    node_addresses: list[tuple[Node, str]] = []
    for node in inventory.nodes:
        node_address = garage(transport, inventory, node, "node", "id").strip()
        if "@" not in node_address:
            raise BackupBucketError(f"{node.id} returned an invalid Garage node address")
        node_addresses.append((node, node_address))

    current_status = garage(transport, inventory, coordinator, "status")
    for node, node_address in node_addresses[1:]:
        node_id = node_address.split("@", 1)[0]
        if node_id[:16] not in current_status:
            garage(
                transport,
                inventory,
                coordinator,
                "node",
                "connect",
                node_address,
            )
    wait_for_nodes(
        transport,
        inventory,
        coordinator,
        tuple(address.split("@", 1)[0] for _, address in node_addresses),
    )

    for node, node_address in node_addresses:
        node_id = node_address.split("@", 1)[0]
        garage(
            transport,
            inventory,
            coordinator,
            "layout",
            "assign",
            "--zone",
            node.id,
            "--capacity",
            capacity,
            node_id,
        )
    garage(transport, inventory, coordinator, "layout", "apply", "--version", "1")
    garage(transport, inventory, coordinator, "bucket", "create", BUCKET)
    key_output = garage(
        transport,
        inventory,
        coordinator,
        "key",
        "create",
        KEY_NAME,
    )
    store_client_secrets(secret_dir, key_output)
    garage(
        transport,
        inventory,
        coordinator,
        "bucket",
        "allow",
        "--read",
        "--write",
        BUCKET,
        "--key",
        KEY_NAME,
    )


def install_backup_clients(
    transport: StrictSshTransport,
    inventory: Inventory,
    secret_dir: Path,
    nodes: tuple[Node, ...] | None = None,
) -> None:
    access = (secret_dir / "backup-s3-access").read_text(encoding="utf-8").strip()
    secret = (secret_dir / "backup-s3-secret").read_text(encoding="utf-8").strip()
    restic_password = ensure_secret(secret_dir, "restic-password").read_text(
        encoding="utf-8"
    ).strip()
    cipher = ensure_secret(secret_dir, "pgbackrest-cipher").read_text(
        encoding="utf-8"
    ).strip()
    files_environment = f"""RESTIC_REPOSITORY=s3:http://127.0.0.1:9000/{BUCKET}/massar/files
AWS_ACCESS_KEY_ID={access}
AWS_SECRET_ACCESS_KEY={secret}
AWS_DEFAULT_REGION=massar-internal
RESTIC_PASSWORD_FILE=/etc/massar/secrets/restic-password
"""
    pg_environment = f"""PGBACKREST_REPO1_S3_KEY={access}
PGBACKREST_REPO1_S3_KEY_SECRET={secret}
PGBACKREST_REPO1_CIPHER_PASS={cipher}
"""
    pgbackrest_config = PGBACKREST_TEMPLATE.read_text(encoding="utf-8")
    for placeholder, value in {
        "__S3_ACCESS_KEY__": access,
        "__S3_SECRET_KEY__": secret,
        "__PGBACKREST_CIPHER_PASS__": cipher,
    }.items():
        pgbackrest_config = pgbackrest_config.replace(placeholder, value)
    if "__" in pgbackrest_config:
        raise BackupBucketError("unresolved pgBackRest configuration placeholder")
    for node in nodes or inventory.nodes:
        remote = target(inventory, node)
        copy_content(transport, remote, files_environment, "/tmp/massar-files.env")
        copy_content(transport, remote, pg_environment, "/tmp/massar-pgbackrest.env")
        copy_content(
            transport,
            remote,
            pgbackrest_config,
            "/tmp/massar-pgbackrest.conf",
        )
        transport.copy(
            remote,
            RELEASE_GATE_SUDOERS,
            "/tmp/massar-release-migration-gate.sudoers",
        )
        transport.copy(
            remote,
            LEGACY_SEAL_SUDOERS,
            "/tmp/massar-legacy-release-seal.sudoers",
        )
        transport.copy(
            remote,
            NORMALIZATION_SUDOERS,
            "/tmp/massar-current-release-normalization.sudoers",
        )
        transport.copy(
            remote,
            IMMUTABLE_RELEASE_SUDOERS,
            "/tmp/massar-immutable-release-install.sudoers",
        )
        transport.copy(remote, REMOTE_BUILDER_SUDOERS, "/tmp/massar-remote-builder.sudoers")
        transport.copy(
            remote,
            LEGACY_CUTOVER_GLUSTER_SUDOERS,
            "/tmp/massar-legacy-cutover-gluster.sudoers",
        )
        transport.copy(
            remote, secret_dir / "restic-password", "/tmp/massar-restic-password"
        )
        for source_name in BACKUP_SCRIPTS:
            transport.copy(
                remote,
                ROOT / "deploy/production/scripts" / source_name,
                f"/tmp/{source_name}",
            )
        for unit_name in BACKUP_UNITS:
            transport.copy(
                remote,
                ROOT / "deploy/production/systemd" / unit_name,
                f"/tmp/{unit_name}",
            )
        script = """
set -euo pipefail
sudo /usr/bin/install -d -m 0750 -o root -g massar /etc/massar/backup
sudo /usr/bin/install -d -m 0755 -o root -g root /usr/local/lib/massar
sudo /usr/bin/install -m 0600 -o root -g root /tmp/massar-files.env /etc/massar/backup/files.env
sudo /usr/bin/install -m 0600 -o root -g root /tmp/massar-pgbackrest.env /etc/massar/backup/pgbackrest.env
sudo /usr/bin/install -m 0600 -o root -g root /tmp/massar-restic-password /etc/massar/secrets/restic-password
sudo /usr/bin/install -m 0600 -o postgres -g postgres /tmp/massar-pgbackrest.conf /etc/pgbackrest.conf
sudo /usr/bin/install -m 0755 -o root -g root /tmp/backup_database.sh /usr/local/sbin/massar-backup-database
sudo /usr/bin/install -m 0755 -o root -g root /tmp/backup_files.sh /usr/local/sbin/massar-backup-files
sudo /usr/bin/install -m 0755 -o root -g root /tmp/prune_file_backups.sh /usr/local/sbin/massar-prune-file-backups
sudo /usr/bin/install -m 0755 -o root -g root /tmp/restore_files_sample.sh /usr/local/sbin/massar-restore-files-sample
sudo /usr/bin/install -m 0755 -o root -g root /tmp/restore_database_sample.sh /usr/local/lib/massar/restore_database_sample.sh
sudo /usr/bin/install -m 0755 -o root -g root /tmp/run_database_restore_drill.sh /usr/local/lib/massar/run_database_restore_drill.sh
sudo /usr/bin/install -m 0755 -o root -g root /tmp/initialize_backup_repository.sh /usr/local/sbin/massar-initialize-backup-repository
sudo /usr/bin/install -m 0755 -o root -g root /tmp/initialize_database_backup.sh /usr/local/sbin/massar-initialize-database-backup
sudo /usr/bin/install -m 0755 -o root -g root /tmp/prepare_pitr_probe.sh /usr/local/sbin/massar-prepare-pitr-probe
sudo /usr/bin/install -m 0755 -o root -g root /tmp/prepare_release_migration_gate.py /usr/local/sbin/massar-produce-release-migration-gate
sudo /usr/bin/install -m 0755 -o root -g root /tmp/normalize_current_release_root.py /usr/local/sbin/massar-normalize-current-release
sudo /usr/bin/install -m 0755 -o root -g root /tmp/seal_legacy_release_root.py /usr/local/sbin/massar-seal-legacy-release
sudo /usr/bin/install -m 0755 -o root -g root /tmp/install_immutable_release.py /usr/local/sbin/massar-install-immutable-release
sudo /usr/bin/install -m 0755 -o root -g root /tmp/remote_builder_executor.py /usr/local/sbin/massar-remote-builder
sudo /usr/bin/install -m 0440 -o root -g root /tmp/massar-release-migration-gate.sudoers /etc/sudoers.d/massar-release-migration-gate
sudo /usr/bin/install -m 0440 -o root -g root /tmp/massar-current-release-normalization.sudoers /etc/sudoers.d/massar-current-release-normalization
sudo /usr/bin/install -m 0440 -o root -g root /tmp/massar-legacy-release-seal.sudoers /etc/sudoers.d/massar-legacy-release-seal
sudo /usr/bin/install -m 0440 -o root -g root /tmp/massar-immutable-release-install.sudoers /etc/sudoers.d/massar-immutable-release-install
sudo /usr/bin/install -m 0440 -o root -g root /tmp/massar-remote-builder.sudoers /etc/sudoers.d/massar-remote-builder
sudo /usr/bin/install -m 0440 -o root -g root /tmp/massar-legacy-cutover-gluster.sudoers /etc/sudoers.d/massar-legacy-cutover-gluster
for unit in \
  /tmp/massar-pgbackrest-{diff,full}.{service,timer} \
  /tmp/massar-pgbackrest-init.service \
  /tmp/massar-pitr-probe.service \
  /tmp/massar-files-{backup,prune,restore-test}.{service,timer} \
  /tmp/massar-backup-repository-init.service \
  /tmp/massar-db-restore-scheduled.service \
  /tmp/massar-db-restore-test.{service,timer}; do
  sudo /usr/bin/install -m 0644 -o root -g root "$unit" "/etc/systemd/system/${unit##*/}"
done
rm -f /tmp/massar-files.env /tmp/massar-pgbackrest.env /tmp/massar-restic-password /tmp/massar-pgbackrest.conf
rm -f /tmp/backup_database.sh /tmp/backup_files.sh /tmp/prune_file_backups.sh /tmp/restore_files_sample.sh /tmp/restore_database_sample.sh /tmp/run_database_restore_drill.sh /tmp/initialize_backup_repository.sh /tmp/initialize_database_backup.sh /tmp/prepare_pitr_probe.sh /tmp/prepare_release_migration_gate.py /tmp/normalize_current_release_root.py /tmp/seal_legacy_release_root.py /tmp/install_immutable_release.py /tmp/remote_builder_executor.py /tmp/massar-release-migration-gate.sudoers /tmp/massar-current-release-normalization.sudoers /tmp/massar-legacy-release-seal.sudoers /tmp/massar-immutable-release-install.sudoers /tmp/massar-remote-builder.sudoers /tmp/massar-legacy-cutover-gluster.sudoers
rm -f /tmp/massar-pgbackrest-{diff,full}.{service,timer}
rm -f /tmp/massar-pgbackrest-init.service
rm -f /tmp/massar-pitr-probe.service
rm -f /tmp/massar-files-{backup,prune,restore-test}.{service,timer}
rm -f /tmp/massar-backup-repository-init.service
rm -f /tmp/massar-db-restore-scheduled.service
rm -f /tmp/massar-db-restore-test.{service,timer}
sudo /usr/bin/systemctl daemon-reload
"""
        transport.run(remote, ("bash", "-lc", script), timeout_seconds=120)


def status(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> None:
    results: dict[str, dict[str, str]] = {}
    node_ids: list[str] = []
    for node in inventory.nodes:
        completed = transport.run(
            target(inventory, node),
            (
                "bash",
                "-lc",
                "set -e; "
                "sudo systemctl is-active massar-backup-bucket.service massar-backup-tls-proxy.service; "
                "curl --silent --show-error --cacert /etc/massar/pki/backup-ca.crt "
                "https://127.0.0.1:9443/ >/dev/null; "
                "sudo docker exec massar-backup-bucket /garage "
                "--rpc-secret-file /run/secrets/garage-rpc node id; "
                "df -B1 --output=avail /var/lib | tail -n 1",
            ),
            timeout_seconds=60,
            check=False,
        )
        results[node.id] = {
            "status": "healthy" if completed.returncode == 0 else "failed",
            "detail": (completed.stdout + completed.stderr).strip(),
        }
        if completed.returncode:
            journal = transport.run(
                target(inventory, node),
                (
                    "bash",
                    "-lc",
                    "sudo journalctl -u massar-backup-tls-proxy.service "
                    "--no-pager --output=cat -n 20 | "
                    "sed -E 's/((key|secret|password)[^ =]*=)[^ ]+/\\1[REDACTED]/Ig'",
                ),
                timeout_seconds=30,
                check=False,
            )
            results[node.id]["detail"] = (
                results[node.id]["detail"] + "\n" + journal.stdout
            )[-2000:]
        detail_lines = completed.stdout.splitlines()
        node_address = next((line for line in detail_lines if "@" in line), "")
        if node_address:
            node_ids.append(node_address.split("@", 1)[0])
    print(json.dumps(results, ensure_ascii=False))
    if any(value["status"] != "healthy" for value in results.values()):
        failed = {
            node_id: value["detail"]
            for node_id, value in results.items()
            if value["status"] != "healthy"
        }
        raise BackupBucketError(
            "one or more internal backup bucket nodes are unhealthy: "
            + json.dumps(failed, ensure_ascii=False)
        )
    if len(node_ids) != 3:
        raise BackupBucketError("could not identify all three internal bucket nodes")
    cluster_status = garage(transport, inventory, inventory.nodes[0], "status")
    if not all(node_id[:16] in cluster_status for node_id in node_ids):
        raise BackupBucketError("internal backup bucket does not have three healthy members")
    garage(
        transport,
        inventory,
        inventory.nodes[0],
        "bucket",
        "info",
        BUCKET,
    )


def capacity_plan(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> None:
    results: dict[str, dict[str, int]] = {}
    for node in inventory.nodes:
        completed = transport.run(
            target(inventory, node),
            (
                "bash",
                "-lc",
                "set -e; "
                "printf 'free='; df -B1 --output=avail /var/lib | tail -n 1 | tr -d ' '; "
                "printf '\\nshared='; du -sb /srv/massar-shared 2>/dev/null | awk '{print $1}'; "
                "printf '\\n'",
            ),
            timeout_seconds=120,
        )
        values: dict[str, int] = {}
        for line in completed.stdout.splitlines():
            key, separator, value = line.partition("=")
            if separator and value.isdigit():
                values[key] = int(value)
        if set(values) != {"free", "shared"}:
            raise BackupBucketError(f"{node.id} returned incomplete capacity data")
        results[node.id] = values
    print(json.dumps(results, ensure_ascii=False))


def initialize_repository(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> None:
    completed = transport.run(
        target(inventory, inventory.nodes[0]),
        (
            "sudo",
            "/usr/bin/systemctl",
            "start",
            "massar-backup-repository-init.service",
        ),
        timeout_seconds=300,
        check=False,
    )
    if completed.returncode:
        raise BackupBucketError("encrypted repository initialization failed")


def timer_state_ok(
    transport: StrictSshTransport,
    inventory: Inventory,
    node: Node,
    action: str,
) -> bool:
    completed = transport.run(
        target(inventory, node),
        ("sudo", "/usr/bin/systemctl", action, *BACKUP_TIMERS),
        timeout_seconds=60,
        check=False,
    )
    return completed.returncode == 0


def scheduled_restore_binding_ok(
    transport: StrictSshTransport,
    inventory: Inventory,
    node: Node,
) -> bool:
    completed = transport.run(
        target(inventory, node),
        (
            "sudo",
            "/usr/bin/systemctl",
            "show",
            "--property=Unit",
            "--value",
            "massar-db-restore-test.timer",
        ),
        timeout_seconds=30,
        check=False,
    )
    return (
        completed.returncode == 0
        and completed.stdout.strip() == "massar-db-restore-scheduled.service"
    )


def schedule_status(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> None:
    failed = [
        node.id
        for node in inventory.nodes
        if not timer_state_ok(transport, inventory, node, "is-enabled")
        or not timer_state_ok(transport, inventory, node, "is-active")
        or not scheduled_restore_binding_ok(transport, inventory, node)
    ]
    if failed:
        raise BackupBucketError(
            "backup schedules are not enabled and active on: " + ",".join(failed)
        )
    print(
        json.dumps(
            {
                "status": "success",
                "nodes": [node.id for node in inventory.nodes],
                "timers": list(BACKUP_TIMERS),
            }
        )
    )


def restore_evidence_status(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> tuple[bool, bool]:
    database_restore_verified = False
    files_restore_verified = False
    for node in inventory.nodes:
        completed = transport.run(
            target(inventory, node),
            (
                "bash",
                "-lc",
                "test -s /var/lib/massar/evidence/restore/database-latest.json && printf 'database\\n'; "
                "test -s /var/lib/massar/evidence/restore/files-latest.json && printf 'files\\n' || true",
            ),
            timeout_seconds=30,
            check=False,
        )
        rows = set(completed.stdout.splitlines())
        database_restore_verified = database_restore_verified or "database" in rows
        files_restore_verified = files_restore_verified or "files" in rows
    return database_restore_verified, files_restore_verified


def activate_node_schedules(
    transport: StrictSshTransport,
    inventory: Inventory,
    node: Node,
) -> None:
    remote = target(inventory, node)
    for action in (("enable", "--now"), ("restart",)):
        completed = transport.run(
            remote,
            ("sudo", "/usr/bin/systemctl", *action, *BACKUP_TIMERS),
            timeout_seconds=120,
            check=False,
        )
        if completed.returncode:
            raise BackupBucketError(
                f"failed to {' '.join(action)} backup schedules on {node.id}"
            )


def activate_schedules(
    transport: StrictSshTransport,
    inventory: Inventory,
) -> None:
    # Enabling PostgreSQL archive automation before restore proof can fill disk.
    status(transport, inventory)
    database_restore_verified, files_restore_verified = restore_evidence_status(
        transport, inventory
    )
    if not database_restore_verified or not files_restore_verified:
        raise BackupBucketError(
            "schedule activation requires successful database and file restore evidence"
        )
    for node in inventory.nodes:
        activate_node_schedules(transport, inventory, node)
    schedule_status(transport, inventory)


def bootstrap(
    transport: StrictSshTransport,
    inventory: Inventory,
    secret_dir: Path,
    capacity: str,
) -> None:
    for name, hexadecimal in (
        ("backup-rpc", True),
        ("backup-admin", False),
        ("backup-metrics", False),
    ):
        ensure_secret(secret_dir, name, hexadecimal=hexadecimal)
    ensure_tls_assets(secret_dir)
    for node in inventory.nodes:
        install_node(transport, inventory, node, secret_dir)
    credentials = [
        secret_dir / "backup-s3-access",
        secret_dir / "backup-s3-secret",
    ]
    if bucket_exists(transport, inventory):
        if not all(path.is_file() and not (path.stat().st_mode & 0o077) for path in credentials):
            raise BackupBucketError(
                "bucket exists but its protected client credentials are unavailable"
            )
    else:
        initialize_layout(transport, inventory, capacity, secret_dir)
    install_backup_clients(transport, inventory, secret_dir)
    status(transport, inventory)


def main() -> int:
    args = parse_args()
    inventory = load_inventory(args.inventory, require_operator_files=True)
    transport = StrictSshTransport(args.known_hosts, args.identity)
    if args.action == "initialize":
        initialize_repository(transport, inventory)
        return 0
    if args.action == "activate-schedules":
        if not args.yes:
            raise BackupBucketError("activate-schedules requires --yes")
        activate_schedules(transport, inventory)
        return 0
    if args.action == "schedule-status":
        schedule_status(transport, inventory)
        return 0
    if args.action == "sync-clients":
        if not args.yes or not args.secret_dir:
            raise BackupBucketError(
                "sync-clients requires --secret-dir and --yes"
            )
        install_backup_clients(
            transport,
            inventory,
            args.secret_dir.expanduser().resolve(),
            tuple(
                node
                for node in inventory.nodes
                if args.node == "all" or node.id == args.node
            ),
        )
        return 0
    if args.action == "plan":
        capacity_plan(transport, inventory)
        return 0
    if args.action == "status":
        status(transport, inventory)
        return 0
    if not args.capacity_per_node or not CAPACITY_RE.fullmatch(args.capacity_per_node):
        raise BackupBucketError(
            "bootstrap requires --capacity-per-node as an explicit GB/TB value"
        )
    if args.dry_run:
        print(
            json.dumps(
                {
                    "status": "dry-run",
                    "nodes": [node.id for node in inventory.nodes],
                    "replicationFactor": 3,
                    "capacityPerNode": args.capacity_per_node,
                    "bucket": BUCKET,
                    "publiclyExposed": False,
                    "timersEnabled": False,
                }
            )
        )
        return 0
    if not args.yes:
        raise BackupBucketError("bootstrap requires --dry-run or --yes")
    if not args.secret_dir:
        raise BackupBucketError("bootstrap requires --secret-dir")
    bootstrap(
        transport,
        inventory,
        args.secret_dir.expanduser().resolve(),
        args.capacity_per_node,
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (BackupBucketError, SshTransportError, subprocess.TimeoutExpired) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(2)
