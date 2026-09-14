#!/usr/bin/env python3
"""Collect the four identity-bound backup-gate inputs through strict SSH."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import shlex
import shutil
import sys
import tempfile
import uuid
from pathlib import Path

from clusterctl import load_inventory
from ssh_transport import SshTarget, SshTransportError, StrictSshTransport


class CollectionError(RuntimeError):
    """Raised when backup-gate input collection cannot be proven safe."""


PG_BACKREST_LABEL = re.compile(
    r"^[0-9]{8}-[0-9]{6}F(?:_[0-9]{8}-[0-9]{6}[DI])?$"
)
RESTIC_SNAPSHOT_ID = re.compile(r"^[0-9a-f]{64}$")
MAX_EVIDENCE_BYTES = 1024 * 1024
LOCAL_NAMES = (
    "database-backup.json",
    "database-restore.json",
    "file-backup.json",
    "file-restore.json",
)


def iso_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def remote_sources(database_backup_id: str, file_snapshot_id: str) -> dict[str, str]:
    if not PG_BACKREST_LABEL.fullmatch(database_backup_id):
        raise CollectionError("database-backup-id is not a full pgBackRest label")
    if not RESTIC_SNAPSHOT_ID.fullmatch(file_snapshot_id):
        raise CollectionError("file-snapshot-id is not a full Restic snapshot ID")
    return {
        "database-backup.json":
            f"/var/lib/massar/evidence/backup/database-{database_backup_id}.json",
        "database-restore.json":
            f"/var/lib/massar/evidence/restore/database-{database_backup_id}.json",
        "file-backup.json":
            f"/srv/massar-shared/.cluster-health/file-backup-{file_snapshot_id}.json",
        "file-restore.json":
            f"/var/lib/massar/evidence/restore/files-{file_snapshot_id}.json",
    }


def target_for(inventory, node_id: str) -> SshTarget:
    matches = [node for node in inventory.nodes if node.id == node_id]
    if len(matches) != 1:
        raise CollectionError("chosen node is not an exact inventory member")
    node = matches[0]
    return SshTarget(node.id, node.public_address, str(inventory.cluster["ssh_user"]))


def stage_script(
    sources: dict[str, str],
    remote_dir: str,
    ssh_user: str,
) -> str:
    if not re.fullmatch(r"/tmp/massar-backup-gate-[0-9a-f]{32}", remote_dir):
        raise CollectionError("remote staging directory is invalid")
    if ssh_user != "massar-ops":
        raise CollectionError("collection requires the massar-ops account")
    rows = [
        "set -euo pipefail",
        "umask 077",
        f"test ! -e {shlex.quote(remote_dir)}",
        (
            "sudo /usr/bin/install -d -m 0700 -o massar-ops -g massar-ops "
            f"{shlex.quote(remote_dir)}"
        ),
    ]
    for local_name, source in sources.items():
        destination = f"{remote_dir}/{local_name}"
        rows.extend([
            f"sudo test -f {shlex.quote(source)}",
            f"sudo test ! -L {shlex.quote(source)}",
            (
                f"size=$(sudo /usr/bin/stat -c %s -- {shlex.quote(source)}); "
                f"test \"$size\" -gt 0; test \"$size\" -le {MAX_EVIDENCE_BYTES}"
            ),
            (
                "sudo /usr/bin/install -m 0600 -o massar-ops -g massar-ops -- "
                f"{shlex.quote(source)} {shlex.quote(destination)}"
            ),
        ])
    return "\n".join(rows)


def cleanup_script(remote_dir: str) -> str:
    if not re.fullmatch(r"/tmp/massar-backup-gate-[0-9a-f]{32}", remote_dir):
        raise CollectionError("remote staging directory is invalid")
    rows = ["set -euo pipefail"]
    for name in LOCAL_NAMES:
        rows.append(f"sudo /bin/rm -f -- {shlex.quote(f'{remote_dir}/{name}')}")
    rows.extend([
        (
            f"if sudo test -e {shlex.quote(remote_dir)}; then "
            f"sudo /usr/bin/rmdir -- {shlex.quote(remote_dir)}; fi"
        ),
        f"sudo test ! -e {shlex.quote(remote_dir)}",
    ])
    return "\n".join(rows)


def collect(
    *,
    transport: StrictSshTransport,
    inventory,
    node_id: str,
    database_backup_id: str,
    file_snapshot_id: str,
    output_dir: Path,
) -> dict[str, object]:
    sources = remote_sources(database_backup_id, file_snapshot_id)
    target = target_for(inventory, node_id)
    if output_dir.is_symlink() or output_dir.exists():
        raise CollectionError("output directory must not exist or be a symlink")
    output_dir.parent.mkdir(parents=True, exist_ok=True)
    temporary = Path(tempfile.mkdtemp(
        prefix=f".{output_dir.name}.",
        dir=output_dir.parent.resolve(),
    ))
    os.chmod(temporary, 0o750)
    remote_dir = f"/tmp/massar-backup-gate-{uuid.uuid4().hex}"
    cleanup_error: Exception | None = None
    result: dict[str, object] | None = None
    operation_error: Exception | None = None
    try:
        transport.run(
            target,
            ("bash", "-lc", stage_script(sources, remote_dir, target.user)),
            timeout_seconds=60,
        )
        files: dict[str, dict[str, object]] = {}
        for name in LOCAL_NAMES:
            destination = temporary / name
            size = transport.fetch(
                target,
                f"{remote_dir}/{name}",
                destination,
                timeout_seconds=60,
                max_bytes=MAX_EVIDENCE_BYTES,
            )
            files[name] = {
                "remoteSource": sources[name],
                "bytes": size,
                "sha256": sha256_file(destination),
            }
        result = {
            "schemaVersion": 1,
            "status": "success",
            "clusterId": str(inventory.cluster["name"]),
            "nodeId": node_id,
            "databaseBackupId": database_backup_id,
            "fileSnapshotId": file_snapshot_id,
            "collectedAt": iso_now(),
            "files": files,
        }
        evidence_path = temporary / "collection-evidence.json"
        evidence_path.write_text(
            json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        os.chmod(evidence_path, 0o640)
    except Exception as exc:
        operation_error = exc
    finally:
        try:
            completed = transport.run(
                target,
                ("bash", "-lc", cleanup_script(remote_dir)),
                timeout_seconds=30,
                check=False,
            )
            if completed.returncode != 0:
                raise CollectionError("remote staging cleanup failed")
        except Exception as exc:
            cleanup_error = exc
    if operation_error is not None or cleanup_error is not None:
        shutil.rmtree(temporary, ignore_errors=True)
        details = "; ".join(
            str(error)
            for error in (operation_error, cleanup_error)
            if error is not None
        )
        raise CollectionError(details)
    if result is None:
        shutil.rmtree(temporary, ignore_errors=True)
        raise CollectionError("collection produced no evidence")
    try:
        os.replace(temporary, output_dir)
    except Exception:
        shutil.rmtree(temporary, ignore_errors=True)
        raise
    return result


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--node", required=True, choices=("node-1", "node-2", "node-3"))
    parser.add_argument("--database-backup-id", required=True)
    parser.add_argument("--file-snapshot-id", required=True)
    parser.add_argument("--output-dir", required=True, type=Path)
    approval = parser.add_mutually_exclusive_group(required=True)
    approval.add_argument("--dry-run", action="store_true")
    approval.add_argument("--yes", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        inventory = load_inventory(args.inventory)
        sources = remote_sources(args.database_backup_id, args.file_snapshot_id)
        target = target_for(inventory, args.node)
        if args.dry_run:
            print(json.dumps({
                "status": "dry-run",
                "sshAttempted": False,
                "nodeId": target.node_id,
                "remoteSources": sources,
                "output": str(args.output_dir),
            }, ensure_ascii=False, sort_keys=True))
            return 0
        result = collect(
            transport=StrictSshTransport(args.known_hosts, args.identity),
            inventory=inventory,
            node_id=args.node,
            database_backup_id=args.database_backup_id,
            file_snapshot_id=args.file_snapshot_id,
            output_dir=args.output_dir,
        )
    except (CollectionError, SshTransportError, OSError, ValueError) as exc:
        print(f"backup gate input collection blocked: {exc}", file=sys.stderr)
        return 6
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
