#!/usr/bin/env python3
"""Assemble a migration gate from real backup, restored-copy, and N-1 evidence."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import sys
from pathlib import Path

from release_contract import (
    CURRENT_RELEASE,
    EVIDENCE_ID,
    HEX_SHA256,
    ReleaseContractError,
    load_migration_safety_gate,
    load_release_manifest,
    parse_utc,
    read_exact_json,
    write_json_atomic,
)


class GateAssemblyError(RuntimeError):
    """Raised when source evidence cannot prove a safe live migration."""


def exact_evidence(
    path: Path,
    *,
    label: str,
    fields: set[str],
) -> dict[str, object]:
    _, value = read_exact_json(path, label)
    if set(value) != fields:
        raise GateAssemblyError(f"{label} fields do not match the exact contract")
    if value.get("schemaVersion") != 1 or value.get("status") != "success":
        raise GateAssemblyError(f"{label} must be successful schemaVersion 1 evidence")
    return value


def assemble(
    *,
    manifest_path: Path,
    release_id: str,
    backup_path: Path,
    restore_path: Path,
    compatibility_path: Path,
    output: Path,
    now: dt.datetime,
) -> dict[str, object]:
    if output.exists() or output.is_symlink():
        raise GateAssemblyError("migration gate output must not already exist")
    manifest = load_release_manifest(manifest_path, release_id)
    common = {
        "schemaVersion", "status", "clusterId", "currentReleaseId",
        "databaseSystemIdentifier", "capturedAt",
    }
    backup = exact_evidence(
        backup_path,
        label="database backup evidence",
        fields=common | {
            "backupId", "encrypted", "tableCountsSha256", "migrationIdsSha256",
        },
    )
    restore = exact_evidence(
        restore_path,
        label="restored-copy migration evidence",
        fields=common | {
            "backupId", "restoreId", "isolated", "checksumVerified",
            "sourceTableCountsSha256", "restoredTableCountsSha256",
            "preMigrationIdsSha256", "postMigrationIdsSha256",
            "restoredCopyMigrationVerified", "realDataValidationVerified",
        },
    )
    compatibility = exact_evidence(
        compatibility_path,
        label="N-1 compatibility evidence",
        fields={
            "schemaVersion", "status", "clusterId", "currentReleaseId",
            "currentManifestSha256", "targetReleaseId", "manifestSha256",
            "databaseSystemIdentifier", "postMigrationIdsSha256",
            "postMigrationSchemaSha256", "capturedAt",
            "nMinusOneCompatibilityVerified",
        },
    )
    current_release = backup["currentReleaseId"]
    system_identifier = backup["databaseSystemIdentifier"]
    backup_id = backup["backupId"]
    if (
        backup["clusterId"] != "massar-production"
        or not isinstance(current_release, str)
        or not CURRENT_RELEASE.fullmatch(current_release)
        or not isinstance(system_identifier, str)
        or not system_identifier.isdigit()
        or len(system_identifier) < 10
        or not isinstance(backup_id, str)
        or not EVIDENCE_ID.fullmatch(backup_id)
        or backup["encrypted"] is not True
        or not isinstance(backup["tableCountsSha256"], str)
        or not HEX_SHA256.fullmatch(backup["tableCountsSha256"])
        or not isinstance(backup["migrationIdsSha256"], str)
        or not HEX_SHA256.fullmatch(backup["migrationIdsSha256"])
    ):
        raise GateAssemblyError("database backup evidence identity or integrity is invalid")
    if (
        any(
            value["clusterId"] != "massar-production"
            or value["currentReleaseId"] != current_release
            or value["databaseSystemIdentifier"] != system_identifier
            for value in (restore, compatibility)
        )
        or restore["backupId"] != backup_id
        or restore["isolated"] is not True
        or restore["checksumVerified"] is not True
        or restore["restoredCopyMigrationVerified"] is not True
        or restore["realDataValidationVerified"] is not True
        or restore["sourceTableCountsSha256"] != backup["tableCountsSha256"]
        or restore["restoredTableCountsSha256"] != backup["tableCountsSha256"]
        or restore["preMigrationIdsSha256"] != backup["migrationIdsSha256"]
        or compatibility["targetReleaseId"] != release_id
        or compatibility["manifestSha256"] != manifest.sha256
        or not isinstance(compatibility["currentManifestSha256"], str)
        or not HEX_SHA256.fullmatch(compatibility["currentManifestSha256"])
        or compatibility["postMigrationIdsSha256"]
        != restore["postMigrationIdsSha256"]
        or not isinstance(compatibility["postMigrationSchemaSha256"], str)
        or not HEX_SHA256.fullmatch(compatibility["postMigrationSchemaSha256"])
        or compatibility["nMinusOneCompatibilityVerified"] is not True
    ):
        raise GateAssemblyError(
            "backup, restored-copy migration, and N-1 evidence are not exactly bound"
        )
    restore_id = restore["restoreId"]
    hashes = (
        restore["sourceTableCountsSha256"],
        restore["restoredTableCountsSha256"],
        restore["preMigrationIdsSha256"],
        restore["postMigrationIdsSha256"],
    )
    if (
        not isinstance(restore_id, str)
        or not EVIDENCE_ID.fullmatch(restore_id)
        or any(not isinstance(value, str) or not HEX_SHA256.fullmatch(value)
               for value in hashes)
    ):
        raise GateAssemblyError("restored-copy evidence identifiers or hashes are invalid")
    backup_at = parse_utc(backup["capturedAt"], "backup capturedAt")
    restore_at = parse_utc(restore["capturedAt"], "restore capturedAt")
    compatibility_at = parse_utc(
        compatibility["capturedAt"], "compatibility capturedAt"
    )
    if not backup_at <= restore_at <= compatibility_at:
        raise GateAssemblyError("release safety evidence timestamps are out of order")
    payload = {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": "massar-production",
        "releaseId": release_id,
        "manifestSha256": manifest.sha256,
        "currentReleaseId": current_release,
        "currentManifestSha256": compatibility["currentManifestSha256"],
        "databaseSystemIdentifier": system_identifier,
        "databaseBackupId": backup_id,
        "databaseRestoreId": restore_id,
        "backupCapturedAt": backup["capturedAt"],
        "restoreCapturedAt": restore["capturedAt"],
        "validatedAt": compatibility["capturedAt"],
        "backupEncrypted": True,
        "restoreIsolated": True,
        "restoreChecksumVerified": True,
        "restoredCopyMigrationVerified": True,
        "realDataValidationVerified": True,
        "nMinusOneCompatibilityVerified": True,
        "sourceDatabaseTableCountsSha256": backup["tableCountsSha256"],
        "restoredDatabaseTableCountsSha256": restore["restoredTableCountsSha256"],
        "preMigrationIdsSha256": backup["migrationIdsSha256"],
        "postMigrationIdsSha256": restore["postMigrationIdsSha256"],
        "postMigrationSchemaSha256": compatibility[
            "postMigrationSchemaSha256"
        ],
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    write_json_atomic(output, payload)
    output.chmod(0o640)
    load_migration_safety_gate(output, manifest=manifest, now=now)
    return payload


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--release", required=True)
    parser.add_argument("--database-backup-evidence", required=True, type=Path)
    parser.add_argument("--restored-copy-evidence", required=True, type=Path)
    parser.add_argument("--compatibility-evidence", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        payload = assemble(
            manifest_path=args.manifest,
            release_id=args.release,
            backup_path=args.database_backup_evidence,
            restore_path=args.restored_copy_evidence,
            compatibility_path=args.compatibility_evidence,
            output=args.output,
            now=dt.datetime.now(dt.timezone.utc),
        )
    except (GateAssemblyError, ReleaseContractError, OSError, ValueError) as exc:
        print(f"migration gate blocked: {exc}", file=sys.stderr)
        return 6
    print(json.dumps({
        "status": "success",
        "releaseId": payload["releaseId"],
        "output": str(args.output),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
