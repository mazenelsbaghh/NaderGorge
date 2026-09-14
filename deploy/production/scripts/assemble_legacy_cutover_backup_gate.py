#!/usr/bin/env python3
"""Assemble a legacy-cutover backup gate from already-produced restore evidence.

This command deliberately does not run pgBackRest, Restic, SSH, or any backup
operation.  It only validates and binds four immutable evidence files.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Any


class BackupGateError(RuntimeError):
    """Raised when source evidence cannot prove the cutover backup gate."""


PG_BACKREST_LABEL = re.compile(
    r"^[0-9]{8}-[0-9]{6}F(?:_[0-9]{8}-[0-9]{6}[DI])?$"
)
RESTIC_SNAPSHOT_ID = re.compile(r"^[0-9a-f]{64}$")
RELEASE_ID = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$")
CLUSTER_ID = re.compile(r"^[a-z0-9][a-z0-9-]{2,63}$")
CANDIDATE_DATABASE = re.compile(
    r"^massar_platform_candidate_[0-9]{8}T[0-9]{6}Z$"
)
SHA256 = re.compile(r"^[0-9a-f]{64}$")
EVIDENCE_MAX_AGE = dt.timedelta(minutes=15)
EVIDENCE_FUTURE_SKEW = dt.timedelta(minutes=2)

COMMON_FIELDS = {
    "schemaVersion",
    "status",
    "producer",
    "clusterId",
    "releaseId",
    "startedAt",
    "completedAt",
    "capturedAt",
}
DATABASE_BACKUP_FIELDS = COMMON_FIELDS | {
    "backupLabel",
    "backupType",
    "stanza",
    "repository",
    "encrypted",
    "replicationFactor",
    "repositoryInfoSha256",
    "walArchiveAgeSeconds",
}
DATABASE_RESTORE_FIELDS = COMMON_FIELDS | {
    "backupLabel",
    "isolated",
    "productionTarget",
    "integrityOk",
    "migrationStateOk",
    "loginSmokeOk",
    "checksumVerified",
    "repositoryInfoSha256",
    "backupEvidenceSha256",
    "recoveryTarget",
    "latestMigration",
    "destroyedAt",
}
FILE_BACKUP_FIELDS = COMMON_FIELDS | {
    "snapshotId",
    "hostname",
    "paths",
    "encrypted",
    "replicationFactor",
    "backupSummarySha256",
    "snapshotAgeSeconds",
}
FILE_RESTORE_FIELDS = COMMON_FIELDS | {
    "snapshotId",
    "isolated",
    "productionTarget",
    "repositoryCheckOk",
    "checksumVerified",
    "fileSampleOk",
    "snapshotMetadataSha256",
    "backupEvidenceSha256",
    "checksum",
    "destroyedAt",
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def regular_file(path: Path, label: str) -> Path:
    candidate = path.expanduser().resolve()
    if path.is_symlink() or not candidate.is_file():
        raise BackupGateError(f"{label} must be a regular non-symlink file")
    return candidate


def load_exact_json(path: Path, label: str, fields: set[str]) -> tuple[dict[str, Any], str]:
    source = regular_file(path, label)
    try:
        raw = source.read_bytes()
        value = json.loads(raw)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise BackupGateError(f"{label} is not valid JSON") from exc
    if not isinstance(value, dict) or set(value) != fields:
        missing = sorted(fields - set(value) if isinstance(value, dict) else fields)
        extra = sorted(set(value) - fields if isinstance(value, dict) else set())
        raise BackupGateError(
            f"{label} fields do not match the evidence contract "
            f"missing={missing} extra={extra}"
        )
    return value, hashlib.sha256(raw).hexdigest()


def timestamp(value: object, label: str) -> dt.datetime:
    try:
        parsed = dt.datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    except ValueError as exc:
        raise BackupGateError(f"{label} is not a valid ISO-8601 timestamp") from exc
    if parsed.tzinfo is None:
        raise BackupGateError(f"{label} must include a timezone")
    return parsed.astimezone(dt.timezone.utc)


def load_inventory_binding(path: Path, cluster_id: str) -> str:
    source = regular_file(path, "inventory")
    try:
        value = json.loads(source.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise BackupGateError("inventory is not valid JSON") from exc
    if (
        not isinstance(value, dict)
        or not isinstance(value.get("cluster"), dict)
        or value["cluster"].get("name") != cluster_id
    ):
        raise BackupGateError("inventory cluster binding mismatch")
    return sha256_file(source)


def load_candidate_binding(path: Path) -> tuple[str, str, str, str]:
    source = regular_file(path, "candidate manifest")
    try:
        value = json.loads(source.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise BackupGateError("candidate manifest is not valid JSON") from exc
    if (
        not isinstance(value, dict)
        or value.get("schemaVersion") != 1
        or value.get("status") != "success"
        or not isinstance(value.get("backupId"), str)
    ):
        raise BackupGateError("candidate manifest is not successful bundle evidence")
    backup_id = value["backupId"]
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._:-]{7,127}", backup_id):
        raise BackupGateError("candidate manifest backupId is invalid")

    digests: list[str] = []
    for field in ("candidateDump", "fileArchive"):
        artifact = value.get(field)
        if not isinstance(artifact, dict) or set(artifact) != {"path", "sha256"}:
            raise BackupGateError(f"candidate manifest {field} binding is invalid")
        expected = artifact["sha256"]
        if not isinstance(expected, str) or not SHA256.fullmatch(expected):
            raise BackupGateError(f"candidate manifest {field} SHA-256 is invalid")
        artifact_path = regular_file(Path(str(artifact["path"])), field)
        if sha256_file(artifact_path) != expected:
            raise BackupGateError(f"candidate manifest {field} digest mismatch")
        digests.append(expected)
    return backup_id, digests[0], digests[1], sha256_file(source)


def validate_common(
    value: dict[str, Any],
    label: str,
    *,
    producer: str,
    cluster_id: str,
    release_id: str,
) -> tuple[dt.datetime, dt.datetime]:
    if value["schemaVersion"] != 1 or value["status"] != "success":
        raise BackupGateError(f"{label} must be successful schemaVersion 1 evidence")
    if value["producer"] != producer:
        raise BackupGateError(f"{label} producer must be {producer}")
    if value["clusterId"] != cluster_id or value["releaseId"] != release_id:
        raise BackupGateError(f"{label} cluster/release binding mismatch")
    started = timestamp(value["startedAt"], f"{label}.startedAt")
    completed = timestamp(value["completedAt"], f"{label}.completedAt")
    if completed < started:
        raise BackupGateError(f"{label} completedAt precedes startedAt")
    captured = timestamp(value["capturedAt"], f"{label}.capturedAt")
    if captured < completed:
        raise BackupGateError(f"{label} capturedAt precedes completedAt")
    return started, completed


def destroyed_after(
    value: dict[str, Any],
    label: str,
    completed: dt.datetime,
) -> dt.datetime:
    destroyed = timestamp(value["destroyedAt"], f"{label}.destroyedAt")
    if destroyed < completed:
        raise BackupGateError(f"{label} destroyedAt precedes completedAt")
    return destroyed


def assemble(
    *,
    database_backup_evidence: Path,
    database_restore_evidence: Path,
    file_backup_evidence: Path,
    file_restore_evidence: Path,
    inventory: Path,
    cluster_id: str,
    release_id: str,
    candidate_database: str,
    candidate_prepared_at: str,
    candidate_manifest: Path,
    operation_id: str,
    now: dt.datetime | None = None,
) -> dict[str, Any]:
    if not CLUSTER_ID.fullmatch(cluster_id):
        raise BackupGateError("cluster-id is invalid")
    if not RELEASE_ID.fullmatch(release_id):
        raise BackupGateError("release-id is invalid")
    if not CANDIDATE_DATABASE.fullmatch(candidate_database):
        raise BackupGateError("candidate-database is invalid")
    try:
        uuid.UUID(operation_id)
    except ValueError as exc:
        raise BackupGateError("operation-id must be a UUID") from exc
    prepared = timestamp(candidate_prepared_at, "candidate-prepared-at")
    observed_now = now or dt.datetime.now(dt.timezone.utc)
    if observed_now.tzinfo is None:
        raise BackupGateError("assembler clock must include a timezone")
    observed_now = observed_now.astimezone(dt.timezone.utc)

    inventory_sha256 = load_inventory_binding(inventory, cluster_id)
    (
        candidate_backup_id,
        candidate_dump_sha256,
        file_archive_sha256,
        manifest_sha256,
    ) = load_candidate_binding(candidate_manifest)
    if not SHA256.fullmatch(inventory_sha256) or not SHA256.fullmatch(manifest_sha256):
        raise BackupGateError("binding digest calculation failed")

    db_backup, db_backup_sha = load_exact_json(
        database_backup_evidence,
        "database backup evidence",
        DATABASE_BACKUP_FIELDS,
    )
    db_restore, db_restore_sha = load_exact_json(
        database_restore_evidence,
        "database restore evidence",
        DATABASE_RESTORE_FIELDS,
    )
    files_backup, files_backup_sha = load_exact_json(
        file_backup_evidence,
        "file backup evidence",
        FILE_BACKUP_FIELDS,
    )
    files_restore, files_restore_sha = load_exact_json(
        file_restore_evidence,
        "file restore evidence",
        FILE_RESTORE_FIELDS,
    )

    db_backup_started, db_backup_completed = validate_common(
        db_backup,
        "database backup evidence",
        producer="pgbackrest",
        cluster_id=cluster_id,
        release_id=release_id,
    )
    db_restore_started, db_restore_completed = validate_common(
        db_restore,
        "database restore evidence",
        producer="pgbackrest",
        cluster_id=cluster_id,
        release_id=release_id,
    )
    files_backup_started, files_backup_completed = validate_common(
        files_backup,
        "file backup evidence",
        producer="restic",
        cluster_id=cluster_id,
        release_id=release_id,
    )
    files_restore_started, files_restore_completed = validate_common(
        files_restore,
        "file restore evidence",
        producer="restic",
        cluster_id=cluster_id,
        release_id=release_id,
    )

    backup_label = str(db_backup["backupLabel"])
    if (
        not PG_BACKREST_LABEL.fullmatch(backup_label)
        or db_restore["backupLabel"] != backup_label
    ):
        raise BackupGateError(
            "database restore evidence is not bound to the pgBackRest backup label"
        )
    if (
        db_backup["backupType"] not in {"full", "diff", "incr"}
        or db_backup["stanza"] != "massar"
        or db_backup["repository"] != 1
        or db_backup["encrypted"] is not True
        or db_backup["replicationFactor"] != 3
        or not SHA256.fullmatch(str(db_backup["repositoryInfoSha256"]))
        or not isinstance(db_backup["walArchiveAgeSeconds"], (int, float))
        or isinstance(db_backup["walArchiveAgeSeconds"], bool)
        or db_backup["walArchiveAgeSeconds"] < 0
    ):
        raise BackupGateError("database backup durability/encryption metadata is invalid")
    if (
        db_restore["isolated"] is not True
        or db_restore["productionTarget"] is not False
        or db_restore["integrityOk"] is not True
        or db_restore["migrationStateOk"] is not True
        or db_restore["loginSmokeOk"] is not True
        or db_restore["checksumVerified"] is not True
        or not SHA256.fullmatch(str(db_restore["repositoryInfoSha256"]))
        or db_restore["backupEvidenceSha256"] != db_backup_sha
        or not isinstance(db_restore["latestMigration"], str)
        or not db_restore["latestMigration"]
    ):
        raise BackupGateError("database restore evidence does not prove an isolated restore")
    db_destroyed = destroyed_after(
        db_restore,
        "database restore evidence",
        db_restore_completed,
    )

    snapshot_id = str(files_backup["snapshotId"])
    if (
        not RESTIC_SNAPSHOT_ID.fullmatch(snapshot_id)
        or files_restore["snapshotId"] != snapshot_id
    ):
        raise BackupGateError(
            "file restore evidence is not bound to the full Restic snapshot ID"
        )
    if (
        files_backup["hostname"] != "massar-cluster"
        or files_backup["paths"] != ["/srv/massar-shared"]
        or files_backup["encrypted"] is not True
        or files_backup["replicationFactor"] != 3
        or not SHA256.fullmatch(str(files_backup["backupSummarySha256"]))
        or not isinstance(files_backup["snapshotAgeSeconds"], (int, float))
        or isinstance(files_backup["snapshotAgeSeconds"], bool)
        or files_backup["snapshotAgeSeconds"] < 0
    ):
        raise BackupGateError("file backup durability/encryption metadata is invalid")
    if (
        files_restore["isolated"] is not True
        or files_restore["productionTarget"] is not False
        or files_restore["repositoryCheckOk"] is not True
        or files_restore["checksumVerified"] is not True
        or files_restore["fileSampleOk"] is not True
        or not SHA256.fullmatch(str(files_restore["snapshotMetadataSha256"]))
        or files_restore["backupEvidenceSha256"] != files_backup_sha
        or not SHA256.fullmatch(str(files_restore["checksum"]))
    ):
        raise BackupGateError("file restore evidence does not prove an isolated restore")
    files_destroyed = destroyed_after(
        files_restore,
        "file restore evidence",
        files_restore_completed,
    )

    for label, started, completed in (
        ("database backup", db_backup_started, db_backup_completed),
        ("database restore", db_restore_started, db_restore_completed),
        ("file backup", files_backup_started, files_backup_completed),
        ("file restore", files_restore_started, files_restore_completed),
    ):
        if started < prepared:
            raise BackupGateError(f"{label} evidence predates candidate preparation")
        if completed > observed_now + EVIDENCE_FUTURE_SKEW:
            raise BackupGateError(f"{label} evidence is future-dated")
        if observed_now - completed > EVIDENCE_MAX_AGE:
            raise BackupGateError(f"{label} evidence is stale")
    if db_restore_started < db_backup_completed:
        raise BackupGateError("database restore started before its backup completed")
    if files_restore_started < files_backup_completed:
        raise BackupGateError("file restore started before its snapshot completed")

    captured_at = max(db_destroyed, files_destroyed)
    if captured_at > observed_now + EVIDENCE_FUTURE_SKEW:
        raise BackupGateError("restore destruction evidence is future-dated")
    if observed_now - captured_at > EVIDENCE_MAX_AGE:
        raise BackupGateError("restore destruction evidence is stale")
    return {
        "schemaVersion": 2,
        "status": "success",
        "clusterId": cluster_id,
        "inventorySha256": inventory_sha256,
        "releaseId": release_id,
        "candidateDatabase": candidate_database,
        "candidatePreparedAt": candidate_prepared_at,
        "candidateManifestSha256": manifest_sha256,
        "candidateBackupId": candidate_backup_id,
        "candidateDumpSha256": candidate_dump_sha256,
        "fileArchiveSha256": file_archive_sha256,
        "operationId": operation_id,
        "databaseBackupId": backup_label,
        "fileSnapshotId": snapshot_id,
        "databaseRestoreVerified": True,
        "fileRestoreVerified": True,
        "databaseBackupEvidenceSha256": db_backup_sha,
        "databaseRestoreEvidenceSha256": db_restore_sha,
        "fileBackupEvidenceSha256": files_backup_sha,
        "fileRestoreEvidenceSha256": files_restore_sha,
        "capturedAt": captured_at.isoformat().replace("+00:00", "Z"),
    }


def write_new_evidence(path: Path, payload: dict[str, Any]) -> None:
    destination = path.expanduser().resolve()
    if path.is_symlink() or destination.exists():
        raise BackupGateError("output must not already exist or be a symlink")
    destination.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{destination.name}.",
        suffix=".tmp",
        dir=destination.parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as output:
            json.dump(payload, output, ensure_ascii=False, indent=2, sort_keys=True)
            output.write("\n")
            output.flush()
            os.fsync(output.fileno())
        os.chmod(temporary, 0o640)
        os.replace(temporary, destination)
    finally:
        temporary.unlink(missing_ok=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Assemble (without running backups) a bound legacy cutover backup gate."
    )
    parser.add_argument("--database-backup-evidence", required=True, type=Path)
    parser.add_argument("--database-restore-evidence", required=True, type=Path)
    parser.add_argument("--file-backup-evidence", required=True, type=Path)
    parser.add_argument("--file-restore-evidence", required=True, type=Path)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--cluster-id", required=True)
    parser.add_argument("--release-id", required=True)
    parser.add_argument("--candidate-database", required=True)
    parser.add_argument("--candidate-prepared-at", required=True)
    parser.add_argument("--candidate-manifest", required=True, type=Path)
    parser.add_argument("--operation-id", required=True)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        payload = assemble(
            database_backup_evidence=args.database_backup_evidence,
            database_restore_evidence=args.database_restore_evidence,
            file_backup_evidence=args.file_backup_evidence,
            file_restore_evidence=args.file_restore_evidence,
            inventory=args.inventory,
            cluster_id=args.cluster_id,
            release_id=args.release_id,
            candidate_database=args.candidate_database,
            candidate_prepared_at=args.candidate_prepared_at,
            candidate_manifest=args.candidate_manifest,
            operation_id=args.operation_id,
        )
        write_new_evidence(args.output, payload)
    except (BackupGateError, OSError, ValueError) as exc:
        print(f"backup gate assembly blocked: {exc}", file=sys.stderr)
        return 6
    print(json.dumps({
        "status": "success",
        "output": str(args.output),
        "databaseBackupId": payload["databaseBackupId"],
        "fileSnapshotId": payload["fileSnapshotId"],
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
