#!/usr/bin/env python3
"""Strict, shared contracts for immutable Production releases."""

from __future__ import annotations

import datetime as dt
import hashlib
import json
import os
import re
import uuid
from dataclasses import dataclass
from pathlib import Path


IMAGES = ("backend", "frontend", "worker", "migrator")
NODE_IDS = ("node-1", "node-2", "node-3")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
HEX_SHA256 = re.compile(r"^[0-9a-f]{64}$")
GIT_COMMIT = re.compile(r"^[0-9a-f]{40}$")
RELEASE = re.compile(
    r"^(?:git-[0-9a-f]{40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$"
)
CURRENT_RELEASE = re.compile(
    r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$"
)
EVIDENCE_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{7,159}$")


class ReleaseContractError(RuntimeError):
    """Raised when release provenance or safety evidence is incomplete."""


@dataclass(frozen=True)
class ReleaseManifest:
    path: Path
    sha256: str
    release_id: str
    git_commit: str | None
    source_state_sha256: str | None
    provenance_type: str
    images: dict[str, str]
    release_files_sha256: str


@dataclass(frozen=True)
class MigrationSafetyGate:
    release_id: str
    manifest_sha256: str
    current_release_id: str
    current_manifest_sha256: str
    database_system_identifier: str
    pre_migration_ids_sha256: str
    post_migration_ids_sha256: str
    post_migration_schema_sha256: str


@dataclass(frozen=True)
class RollbackCompatibilityGate:
    current_release_id: str
    current_manifest_sha256: str
    target_release_id: str
    target_manifest_sha256: str
    database_system_identifier: str
    migration_ids_sha256: str
    schema_sha256: str


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_json_atomic(path: Path, value: dict[str, object]) -> None:
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def parse_utc(value: object, field: str) -> dt.datetime:
    if not isinstance(value, str):
        raise ReleaseContractError(f"{field} must be an ISO-8601 UTC timestamp")
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ReleaseContractError(f"{field} must be an ISO-8601 UTC timestamp") from exc
    if parsed.tzinfo is None or parsed.utcoffset() != dt.timedelta(0):
        raise ReleaseContractError(f"{field} must be UTC")
    return parsed


def read_exact_json(path: Path, label: str) -> tuple[Path, dict[str, object]]:
    expanded = path.expanduser()
    if expanded.is_symlink() or not expanded.is_file():
        raise ReleaseContractError(f"{label} must be a regular non-symlink file")
    resolved = expanded.resolve()
    try:
        value = json.loads(resolved.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ReleaseContractError(f"{label} is not valid UTF-8 JSON") from exc
    if not isinstance(value, dict):
        raise ReleaseContractError(f"{label} root must be an object")
    return resolved, value


def load_release_manifest(path: Path, expected_release: str) -> ReleaseManifest:
    resolved, value = read_exact_json(path, "release manifest")
    common = {
        "schemaVersion", "releaseId", "createdAt", "platform", "images",
        "status", "nodeCount", "digestParity", "distribution",
    }
    source_required = common | {
        "schemaVersion", "releaseId", "gitCommit", "sourceStateSha256",
        "dirtySourceSnapshot", "createdAt", "platform", "images", "status",
        "nodeCount", "digestParity", "distribution",
    }
    legacy_required = common | {"sealedLegacyProvenance"}
    if frozenset(value) not in {
        frozenset(source_required), frozenset(legacy_required)
    }:
        raise ReleaseContractError("release manifest fields do not match the exact contract")
    release_id = value["releaseId"]
    sealed_legacy = "sealedLegacyProvenance" in value
    sealed_tree_sha256: str | None = None
    git_commit = value.get("gitCommit")
    source_digest = value.get("sourceStateSha256")
    dirty = value.get("dirtySourceSnapshot")
    if (
        value["schemaVersion"] != 1
        or value["status"] != "success"
        or value["platform"] != "linux/amd64"
        or value["nodeCount"] != 3
        or value["digestParity"] is not True
        or not isinstance(release_id, str)
        or not RELEASE.fullmatch(release_id)
        or release_id != expected_release
    ):
        raise ReleaseContractError("release manifest identity, status, or provenance is invalid")
    if sealed_legacy:
        provenance = value["sealedLegacyProvenance"]
        if (
            not release_id.startswith("prod-")
            or not isinstance(provenance, dict)
            or set(provenance) != {
                "schemaVersion", "type", "sealedAt", "runtimeBundleSha256",
                "runtimeBundleDigestAlgorithm",
                "sourceReleaseLabel",
            }
            or provenance.get("schemaVersion") != 2
            or provenance.get("type") != "sealed-legacy-bootstrap"
            or provenance.get("sourceReleaseLabel") != release_id
            or provenance.get("runtimeBundleDigestAlgorithm")
            != "massar-runtime-bundle-sha256-v1"
            or not isinstance(provenance.get("runtimeBundleSha256"), str)
            or not HEX_SHA256.fullmatch(str(provenance.get("runtimeBundleSha256")))
        ):
            raise ReleaseContractError("sealed Legacy provenance is invalid")
        sealed_tree_sha256 = str(provenance["runtimeBundleSha256"])
        parse_utc(provenance["sealedAt"], "sealedLegacyProvenance.sealedAt")
    elif (
        not isinstance(git_commit, str)
        or not GIT_COMMIT.fullmatch(git_commit)
        or not isinstance(source_digest, str)
        or not HEX_SHA256.fullmatch(source_digest)
        or not isinstance(dirty, bool)
    ):
        raise ReleaseContractError("source release provenance is invalid")
    if not sealed_legacy and release_id.startswith("git-") and (
        dirty is not False or release_id != f"git-{git_commit}"
    ):
        raise ReleaseContractError("Git release provenance is inconsistent")
    if not sealed_legacy and release_id.startswith("src-") and (
        dirty is not True or release_id != f"src-{source_digest[:40]}"
    ):
        raise ReleaseContractError("source snapshot release provenance is inconsistent")
    if not sealed_legacy and release_id.startswith("prod-") and dirty is not False:
        raise ReleaseContractError("labeled Production release provenance is inconsistent")
    parse_utc(value["createdAt"], "createdAt")

    images = value["images"]
    expected_images = (
        {"backend", "frontend", "worker"} if sealed_legacy else set(IMAGES)
    )
    if (
        not isinstance(images, dict)
        or set(images) != expected_images
        or any(not isinstance(digest, str) or not DIGEST.fullmatch(digest)
               for digest in images.values())
    ):
        raise ReleaseContractError("release manifest image set or digest is invalid")
    distribution = value["distribution"]
    if not isinstance(distribution, dict) or set(distribution) != set(NODE_IDS):
        raise ReleaseContractError("release distribution must prove the exact three nodes")
    release_file_digests: set[str] = set()
    for node_id in NODE_IDS:
        node = distribution[node_id]
        if (
            not isinstance(node, dict)
            or set(node) != {"status", "releaseFilesSha256"}
            or node["status"] != "verified"
            or not isinstance(node["releaseFilesSha256"], str)
            or not HEX_SHA256.fullmatch(node["releaseFilesSha256"])
        ):
            raise ReleaseContractError(f"release distribution proof is invalid for {node_id}")
        release_file_digests.add(node["releaseFilesSha256"])
    if len(release_file_digests) != 1:
        raise ReleaseContractError("release file digest parity is inconsistent")
    if sealed_legacy and release_file_digests != {sealed_tree_sha256}:
        raise ReleaseContractError(
            "sealed Legacy tree digest does not match distribution proof"
        )
    return ReleaseManifest(
        path=resolved,
        sha256=file_sha256(resolved),
        release_id=release_id,
        git_commit=git_commit,
        source_state_sha256=source_digest,
        provenance_type=(
            "sealed-legacy-bootstrap" if sealed_legacy else "source-build"
        ),
        images={name: str(images[name]) for name in sorted(expected_images)},
        release_files_sha256=release_file_digests.pop(),
    )


def load_migration_safety_gate(
    path: Path,
    *,
    manifest: ReleaseManifest,
    now: dt.datetime | None = None,
    maximum_age: dt.timedelta = dt.timedelta(hours=1),
) -> MigrationSafetyGate:
    _, value = read_exact_json(path, "migration safety gate")
    required = {
        "schemaVersion", "status", "clusterId", "releaseId", "manifestSha256",
        "currentReleaseId", "currentManifestSha256",
        "databaseSystemIdentifier", "databaseBackupId",
        "databaseRestoreId", "backupCapturedAt", "restoreCapturedAt",
        "validatedAt", "backupEncrypted", "restoreIsolated",
        "restoreChecksumVerified", "restoredCopyMigrationVerified",
        "realDataValidationVerified", "nMinusOneCompatibilityVerified",
        "sourceDatabaseTableCountsSha256", "restoredDatabaseTableCountsSha256",
        "preMigrationIdsSha256", "postMigrationIdsSha256",
        "postMigrationSchemaSha256",
    }
    if set(value) != required:
        raise ReleaseContractError(
            "migration safety gate fields do not match the exact contract"
        )
    hashes = (
        value["manifestSha256"],
        value["currentManifestSha256"],
        value["sourceDatabaseTableCountsSha256"],
        value["restoredDatabaseTableCountsSha256"],
        value["preMigrationIdsSha256"],
        value["postMigrationIdsSha256"],
        value["postMigrationSchemaSha256"],
    )
    if (
        value["schemaVersion"] != 1
        or value["status"] != "success"
        or value["clusterId"] != "massar-production"
        or value["releaseId"] != manifest.release_id
        or value["manifestSha256"] != manifest.sha256
        or not isinstance(value["currentReleaseId"], str)
        or not CURRENT_RELEASE.fullmatch(value["currentReleaseId"])
        or not isinstance(value["databaseSystemIdentifier"], str)
        or not value["databaseSystemIdentifier"].isdigit()
        or len(value["databaseSystemIdentifier"]) < 10
        or not isinstance(value["databaseBackupId"], str)
        or not EVIDENCE_ID.fullmatch(value["databaseBackupId"])
        or not isinstance(value["databaseRestoreId"], str)
        or not EVIDENCE_ID.fullmatch(value["databaseRestoreId"])
        or any(not isinstance(item, str) or not HEX_SHA256.fullmatch(item) for item in hashes)
        or value["sourceDatabaseTableCountsSha256"]
        != value["restoredDatabaseTableCountsSha256"]
        or any(
            value[field] is not True
            for field in (
                "backupEncrypted", "restoreIsolated", "restoreChecksumVerified",
                "restoredCopyMigrationVerified", "realDataValidationVerified",
                "nMinusOneCompatibilityVerified",
            )
        )
    ):
        raise ReleaseContractError(
            "migration safety gate does not prove a bound backup, restore, and compatibility"
        )
    backup_at = parse_utc(value["backupCapturedAt"], "backupCapturedAt")
    restore_at = parse_utc(value["restoreCapturedAt"], "restoreCapturedAt")
    validated_at = parse_utc(value["validatedAt"], "validatedAt")
    observed_now = now or dt.datetime.now(dt.timezone.utc)
    future_limit = observed_now + dt.timedelta(minutes=2)
    if (
        backup_at > restore_at
        or restore_at > validated_at
        or any(item > future_limit for item in (backup_at, restore_at, validated_at))
        or any(
            observed_now - item > maximum_age
            for item in (backup_at, restore_at, validated_at)
        )
    ):
        raise ReleaseContractError("migration safety gate is stale or temporally inconsistent")
    return MigrationSafetyGate(
        release_id=manifest.release_id,
        manifest_sha256=manifest.sha256,
        current_release_id=str(value["currentReleaseId"]),
        current_manifest_sha256=str(value["currentManifestSha256"]),
        database_system_identifier=str(value["databaseSystemIdentifier"]),
        pre_migration_ids_sha256=str(value["preMigrationIdsSha256"]),
        post_migration_ids_sha256=str(value["postMigrationIdsSha256"]),
        post_migration_schema_sha256=str(value["postMigrationSchemaSha256"]),
    )


def load_rollback_compatibility_gate(
    path: Path,
    *,
    current_manifest: ReleaseManifest,
    target_manifest: ReleaseManifest,
    now: dt.datetime | None = None,
    maximum_age: dt.timedelta = dt.timedelta(hours=24),
) -> RollbackCompatibilityGate:
    """Validate the original migration gate as an exact N-1 rollback proof.

    Rollback evidence remains usable for the deployment day because the live
    prestate independently rechecks the database identity, migration IDs and
    schema digest before any node is drained. The ordinary forward migration
    gate keeps its stricter one-hour freshness bound.
    """
    if current_manifest.release_id == target_manifest.release_id:
        raise ReleaseContractError("rollback current and target releases must differ")
    gate = load_migration_safety_gate(
        path,
        manifest=current_manifest,
        now=now,
        maximum_age=maximum_age,
    )
    if (
        gate.current_release_id != target_manifest.release_id
        or gate.current_manifest_sha256 != target_manifest.sha256
    ):
        raise ReleaseContractError(
            "rollback evidence is not bound to the exact target manifest"
        )
    return RollbackCompatibilityGate(
        current_release_id=current_manifest.release_id,
        current_manifest_sha256=current_manifest.sha256,
        target_release_id=target_manifest.release_id,
        target_manifest_sha256=target_manifest.sha256,
        database_system_identifier=gate.database_system_identifier,
        migration_ids_sha256=gate.post_migration_ids_sha256,
        schema_sha256=gate.post_migration_schema_sha256,
    )
