from __future__ import annotations

import datetime as dt
import hashlib
import importlib.util
import json
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "release_contract",
    SCRIPTS / "release_contract.py",
)
assert SPEC and SPEC.loader
contract = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = contract
SPEC.loader.exec_module(contract)


NOW = dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc)
RELEASE = "git-" + "a" * 40


def release_manifest(path: Path, **overrides: object) -> Path:
    value = {
        "schemaVersion": 1,
        "releaseId": RELEASE,
        "gitCommit": "a" * 40,
        "sourceStateSha256": "b" * 64,
        "dirtySourceSnapshot": False,
        "createdAt": "2026-07-27T11:30:00Z",
        "platform": "linux/amd64",
        "images": {
            name: f"sha256:{index:064x}"
            for index, name in enumerate(contract.IMAGES, 1)
        },
        "status": "success",
        "nodeCount": 3,
        "digestParity": True,
        "distribution": {
            node: {
                "status": "verified",
                "releaseFilesSha256": "c" * 64,
            }
            for node in contract.NODE_IDS
        },
    }
    value.update(overrides)
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def migration_gate(path: Path, manifest, **overrides: object) -> Path:
    value = {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": "massar-production",
        "releaseId": manifest.release_id,
        "manifestSha256": manifest.sha256,
        "currentReleaseId": "prod-20260726-166-r1",
        "currentManifestSha256": "2" * 64,
        "databaseSystemIdentifier": "7586552109940137719",
        "databaseBackupId": "pgbackrest-20260727-001",
        "databaseRestoreId": "restore-20260727-001",
        "backupCapturedAt": "2026-07-27T11:00:00Z",
        "restoreCapturedAt": "2026-07-27T11:20:00Z",
        "validatedAt": "2026-07-27T11:30:00Z",
        "backupEncrypted": True,
        "restoreIsolated": True,
        "restoreChecksumVerified": True,
        "restoredCopyMigrationVerified": True,
        "realDataValidationVerified": True,
        "nMinusOneCompatibilityVerified": True,
        "sourceDatabaseTableCountsSha256": "e" * 64,
        "restoredDatabaseTableCountsSha256": "e" * 64,
        "preMigrationIdsSha256": "f" * 64,
        "postMigrationIdsSha256": "1" * 64,
        "postMigrationSchemaSha256": "3" * 64,
    }
    value.update(overrides)
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def test_strict_manifest_accepts_only_complete_three_node_provenance(
    tmp_path: Path,
) -> None:
    path = release_manifest(tmp_path / "manifest.json")
    manifest = contract.load_release_manifest(path, RELEASE)
    assert manifest.sha256 == hashlib.sha256(path.read_bytes()).hexdigest()
    assert manifest.release_files_sha256 == "c" * 64


@pytest.mark.parametrize(
    "override, message",
    [
        ({"digestParity": False}, "identity, status, or provenance"),
        ({"nodeCount": 2}, "identity, status, or provenance"),
        ({"distribution": {}}, "exact three nodes"),
        ({"dirtySourceSnapshot": True}, "Git release provenance"),
        ({"unexpected": True}, "exact contract"),
    ],
)
def test_strict_manifest_rejects_partial_or_inconsistent_evidence(
    tmp_path: Path,
    override: dict[str, object],
    message: str,
) -> None:
    path = release_manifest(tmp_path / "manifest.json", **override)
    with pytest.raises(contract.ReleaseContractError, match=message):
        contract.load_release_manifest(path, RELEASE)


def test_safety_gate_is_fresh_and_bound_to_manifest_and_database(
    tmp_path: Path,
) -> None:
    manifest = contract.load_release_manifest(
        release_manifest(tmp_path / "manifest.json"),
        RELEASE,
    )
    gate = contract.load_migration_safety_gate(
        migration_gate(tmp_path / "gate.json", manifest),
        manifest=manifest,
        now=NOW,
    )
    assert gate.database_system_identifier == "7586552109940137719"
    assert gate.current_release_id == "prod-20260726-166-r1"


@pytest.mark.parametrize(
    "override, message",
    [
        ({"manifestSha256": "0" * 64}, "bound backup"),
        ({"nMinusOneCompatibilityVerified": False}, "bound backup"),
        ({"realDataValidationVerified": False}, "bound backup"),
        ({"restoreIsolated": False}, "bound backup"),
        ({"restoredDatabaseTableCountsSha256": "0" * 64}, "bound backup"),
        ({"validatedAt": "2026-07-27T09:00:00Z"}, "stale"),
        ({"backupCapturedAt": "2026-07-27T09:00:00Z"}, "stale"),
        ({"backupCapturedAt": "2026-07-27T12:03:00Z"}, "stale"),
        ({"restoreCapturedAt": "2026-07-27T12:03:00Z"}, "stale"),
        ({"unexpected": True}, "exact contract"),
    ],
)
def test_safety_gate_fails_closed_on_stale_unbound_or_incomplete_evidence(
    tmp_path: Path,
    override: dict[str, object],
    message: str,
) -> None:
    manifest = contract.load_release_manifest(
        release_manifest(tmp_path / "manifest.json"),
        RELEASE,
    )
    path = migration_gate(tmp_path / "gate.json", manifest, **override)
    with pytest.raises(contract.ReleaseContractError, match=message):
        contract.load_migration_safety_gate(path, manifest=manifest, now=NOW)


def test_rollback_gate_binds_current_and_target_manifests_and_schema(
    tmp_path: Path,
) -> None:
    current = contract.load_release_manifest(
        release_manifest(tmp_path / "current.json"),
        RELEASE,
    )
    target_release = "prod-20260726-166-r1"
    target = contract.load_release_manifest(
        release_manifest(
            tmp_path / "target.json",
            releaseId=target_release,
            gitCommit="9" * 40,
        ),
        target_release,
    )
    evidence = migration_gate(
        tmp_path / "rollback-evidence.json",
        current,
        currentReleaseId=target_release,
        currentManifestSha256=target.sha256,
    )
    gate = contract.load_rollback_compatibility_gate(
        evidence,
        current_manifest=current,
        target_manifest=target,
        now=NOW,
    )
    assert gate.current_manifest_sha256 == current.sha256
    assert gate.target_manifest_sha256 == target.sha256
    assert gate.migration_ids_sha256 == "1" * 64
    assert gate.schema_sha256 == "3" * 64


def test_rollback_gate_remains_valid_during_deployment_day(
    tmp_path: Path,
) -> None:
    current = contract.load_release_manifest(
        release_manifest(tmp_path / "current.json"),
        RELEASE,
    )
    target_release = "prod-20260726-166-r1"
    target = contract.load_release_manifest(
        release_manifest(
            tmp_path / "target.json",
            releaseId=target_release,
            gitCommit="9" * 40,
        ),
        target_release,
    )
    evidence = migration_gate(
        tmp_path / "rollback-evidence.json",
        current,
        currentReleaseId=target_release,
        currentManifestSha256=target.sha256,
        backupCapturedAt="2026-07-27T03:00:00Z",
        restoreCapturedAt="2026-07-27T03:10:00Z",
        validatedAt="2026-07-27T03:20:00Z",
    )

    gate = contract.load_rollback_compatibility_gate(
        evidence,
        current_manifest=current,
        target_manifest=target,
        now=NOW,
    )

    assert gate.target_release_id == target_release


@pytest.mark.parametrize(
    "override,message",
    [
        ({"currentManifestSha256": "0" * 64}, "exact target manifest"),
        ({"currentReleaseId": "git-" + "8" * 40}, "exact target manifest"),
        ({"postMigrationIdsSha256": "not-a-hash"}, "bound backup"),
        ({"postMigrationSchemaSha256": "not-a-hash"}, "bound backup"),
        ({"nMinusOneCompatibilityVerified": False}, "bound backup"),
        ({"validatedAt": "2026-07-27T09:00:00Z"}, "stale"),
        ({
            "backupCapturedAt": "2026-07-25T10:00:00Z",
            "restoreCapturedAt": "2026-07-25T10:10:00Z",
            "validatedAt": "2026-07-25T10:20:00Z",
        }, "stale"),
        ({"validatedAt": "2026-07-27T12:03:00Z"}, "stale"),
        ({"unexpected": True}, "exact contract"),
    ],
)
def test_rollback_gate_rejects_unbound_stale_or_incomplete_evidence(
    tmp_path: Path,
    override: dict[str, object],
    message: str,
) -> None:
    current = contract.load_release_manifest(
        release_manifest(tmp_path / "current.json"),
        RELEASE,
    )
    target_release = "prod-20260726-166-r1"
    target = contract.load_release_manifest(
        release_manifest(
            tmp_path / "target.json",
            releaseId=target_release,
            gitCommit="9" * 40,
        ),
        target_release,
    )
    values: dict[str, object] = {
        "currentReleaseId": target_release,
        "currentManifestSha256": target.sha256,
    }
    values.update(override)
    evidence = migration_gate(
        tmp_path / "rollback-evidence.json",
        current,
        **values,
    )
    with pytest.raises(contract.ReleaseContractError, match=message):
        contract.load_rollback_compatibility_gate(
            evidence,
            current_manifest=current,
            target_manifest=target,
            now=NOW,
        )


def test_rollback_gate_refuses_symlink_evidence(tmp_path: Path) -> None:
    current = contract.load_release_manifest(
        release_manifest(tmp_path / "current.json"),
        RELEASE,
    )
    target_release = "prod-20260726-166-r1"
    target = contract.load_release_manifest(
        release_manifest(
            tmp_path / "target.json",
            releaseId=target_release,
            gitCommit="9" * 40,
        ),
        target_release,
    )
    real = migration_gate(
        tmp_path / "real.json",
        current,
        currentReleaseId=target_release,
        currentManifestSha256=target.sha256,
    )
    link = tmp_path / "link.json"
    link.symlink_to(real)
    with pytest.raises(contract.ReleaseContractError, match="non-symlink"):
        contract.load_rollback_compatibility_gate(
            link,
            current_manifest=current,
            target_manifest=target,
            now=NOW,
        )
