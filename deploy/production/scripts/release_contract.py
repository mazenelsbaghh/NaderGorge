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
from pathlib import Path, PurePosixPath


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
SOURCE_DIGEST_ALGORITHM = "massar-release-snapshot-sha256-v2"
PATH_CLASSIFICATIONS = frozenset(
    {
        "artifact", "configuration", "documentation", "infrastructure",
        "other", "source", "specification", "test", "tooling",
    }
)


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
    schema_version: int = 1
    source_paths: tuple[dict[str, object], ...] = ()
    artifacts: dict[str, dict[str, str]] | None = None
    migration_compatibility: dict[str, object] | None = None
    verification_evidence: dict[str, str] | None = None
    eligible: bool = False
    invalidation_reason: str | None = None


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


def canonical_source_digest(paths: list[dict[str, object]]) -> str:
    digest = hashlib.sha256()
    for entry in paths:
        digest.update(str(entry["path"]).encode("utf-8", errors="surrogateescape"))
        digest.update(b"\0")
        digest.update(str(entry["sha256"]).encode("ascii"))
        digest.update(b"\0")
    return digest.hexdigest()


def _safe_source_path(value: object) -> str:
    if not isinstance(value, str):
        raise ReleaseContractError("source path must be a relative string")
    path = PurePosixPath(value)
    lowered = tuple(part.lower() for part in path.parts)
    if path.is_absolute() or not path.parts or ".." in path.parts:
        raise ReleaseContractError("source path is unsafe")
    if (
        path.name.lower().startswith(".env")
        and path.name.lower() not in {".env.example", ".env.sample"}
    ) or any(
        part in {"secret", "secrets", ".secrets", "credential", "credentials"}
        for part in lowered
    ) or path.suffix.lower() in {
        ".key", ".kdbx", ".keystore", ".p12", ".pem", ".pfx"
    }:
        raise ReleaseContractError("source path contains blocked sensitive material")
    return path.as_posix()


def _validate_source_paths(value: object, source_digest: str) -> tuple[dict[str, object], ...]:
    if not isinstance(value, list) or not value:
        raise ReleaseContractError("release manifest requires complete source paths")
    validated: list[dict[str, object]] = []
    seen: set[str] = set()
    for entry in value:
        if not isinstance(entry, dict) or set(entry) != {
            "path", "status", "classification", "sizeBytes", "sha256",
        }:
            raise ReleaseContractError("source path fields do not match the exact contract")
        path = _safe_source_path(entry["path"])
        if path in seen:
            raise ReleaseContractError("source path inventory contains duplicates")
        seen.add(path)
        if (
            entry["status"] not in {"tracked", "added", "modified", "untracked"}
            or entry["classification"] not in PATH_CLASSIFICATIONS
            or not isinstance(entry["sizeBytes"], int)
            or entry["sizeBytes"] < 0
            or not isinstance(entry["sha256"], str)
            or not HEX_SHA256.fullmatch(entry["sha256"])
        ):
            raise ReleaseContractError("source path classification or digest is invalid")
        validated.append(dict(entry))
    if [entry["path"] for entry in validated] != sorted(seen):
        raise ReleaseContractError("source path inventory must be sorted")
    if canonical_source_digest(validated) != source_digest:
        raise ReleaseContractError("source path inventory does not match source digest")
    return tuple(validated)


def _validate_artifacts(
    value: object,
    images: dict[str, str],
    release_files_sha256: str,
) -> dict[str, dict[str, str]]:
    expected = {*IMAGES, "release-files"}
    if not isinstance(value, dict) or set(value) != expected:
        raise ReleaseContractError("artifact manifest must include every release artifact")
    artifacts: dict[str, dict[str, str]] = {}
    for name in IMAGES:
        item = value[name]
        if (
            not isinstance(item, dict)
            or set(item) != {"imageDigest", "archiveSha256"}
            or item["imageDigest"] != images[name]
            or not isinstance(item["archiveSha256"], str)
            or not HEX_SHA256.fullmatch(item["archiveSha256"])
        ):
            raise ReleaseContractError(f"artifact parity is invalid for {name}")
        artifacts[name] = {
            "imageDigest": str(item["imageDigest"]),
            "archiveSha256": str(item["archiveSha256"]),
        }
    release_files = value["release-files"]
    if (
        not isinstance(release_files, dict)
        or set(release_files) != {"sha256"}
        or release_files.get("sha256") != release_files_sha256
    ):
        raise ReleaseContractError("release bundle artifact parity is invalid")
    artifacts["release-files"] = {"sha256": release_files_sha256}
    return artifacts


def _v2_source_provenance(
    manifest: dict[str, object],
    expected_release: str,
) -> tuple[str, str, str, tuple[dict[str, object], ...]]:
    release_id = manifest["releaseId"]
    git_commit = manifest["gitCommit"]
    source_digest = manifest["sourceStateSha256"]
    dirty = manifest["dirtySourceSnapshot"]
    valid_identity = (
        isinstance(release_id, str)
        and RELEASE.fullmatch(release_id)
        and release_id == expected_release
        and isinstance(git_commit, str)
        and GIT_COMMIT.fullmatch(git_commit)
        and isinstance(source_digest, str)
        and HEX_SHA256.fullmatch(source_digest)
        and isinstance(dirty, bool)
    )
    if not valid_identity or manifest["sourceDigestAlgorithm"] != SOURCE_DIGEST_ALGORITHM:
        raise ReleaseContractError("release manifest v2 identity or source provenance is invalid")
    if release_id.startswith("git-") and (dirty or release_id != f"git-{git_commit}"):
        raise ReleaseContractError("Git release provenance is inconsistent")
    if release_id.startswith("src-") and (
        not dirty or release_id != f"src-{source_digest[:40]}"
    ):
        raise ReleaseContractError("source snapshot release provenance is inconsistent")
    return (
        release_id,
        git_commit,
        source_digest,
        _validate_source_paths(manifest["sourcePaths"], source_digest),
    )


def _validate_deleted_source_paths(manifest: dict[str, object]) -> None:
    deleted_paths = manifest["deletedSourcePaths"]
    if (
        not isinstance(deleted_paths, list)
        or any(not isinstance(path, str) for path in deleted_paths)
        or deleted_paths != sorted(set(deleted_paths))
        or any(_safe_source_path(path) != path for path in deleted_paths)
    ):
        raise ReleaseContractError("deleted source path inventory is invalid")


def _v2_artifact_provenance(
    manifest: dict[str, object],
) -> tuple[dict[str, str], str, dict[str, dict[str, str]]]:
    image_values = manifest["images"]
    if (
        not isinstance(image_values, dict)
        or set(image_values) != set(IMAGES)
        or any(
            not isinstance(digest, str) or not DIGEST.fullmatch(digest)
            for digest in image_values.values()
        )
    ):
        raise ReleaseContractError("release manifest image set or digest is invalid")
    distribution = manifest["distribution"]
    if not isinstance(distribution, dict) or set(distribution) != set(NODE_IDS):
        raise ReleaseContractError("release distribution must prove the exact three nodes")
    release_file_digests: set[str] = set()
    for node_id in NODE_IDS:
        node_evidence = distribution[node_id]
        if (
            not isinstance(node_evidence, dict)
            or set(node_evidence) != {"status", "releaseFilesSha256"}
            or node_evidence["status"] != "verified"
            or not isinstance(node_evidence["releaseFilesSha256"], str)
            or not HEX_SHA256.fullmatch(node_evidence["releaseFilesSha256"])
        ):
            raise ReleaseContractError(f"release distribution proof is invalid for {node_id}")
        release_file_digests.add(node_evidence["releaseFilesSha256"])
    if len(release_file_digests) != 1:
        raise ReleaseContractError("release file digest parity is inconsistent")
    release_files_sha256 = release_file_digests.pop()
    images = {name: str(image_values[name]) for name in IMAGES}
    return (
        images,
        release_files_sha256,
        _validate_artifacts(manifest["artifacts"], images, release_files_sha256),
    )


def _v2_migration_compatibility(
    manifest: dict[str, object],
) -> dict[str, object]:
    migration_ids = manifest["migrationSet"]
    if (
        not isinstance(migration_ids, list)
        or any(not isinstance(identifier, str) for identifier in migration_ids)
        or len(migration_ids) != len(set(migration_ids))
        or any(not EVIDENCE_ID.fullmatch(identifier) for identifier in migration_ids)
    ):
        raise ReleaseContractError("ordered migration set is invalid")
    compatibility = manifest["migrationCompatibility"]
    if not isinstance(compatibility, dict) or set(compatibility) != {
        "status", "emptyDatabaseVerified", "productionLikeVerified",
        "nMinusOneVerified", "evidenceSha256",
    }:
        raise ReleaseContractError("migration compatibility fields are incomplete")
    flags = (
        compatibility["emptyDatabaseVerified"],
        compatibility["productionLikeVerified"],
        compatibility["nMinusOneVerified"],
    )
    evidence = compatibility["evidenceSha256"]
    if compatibility["status"] == "passed":
        if (
            any(flag is not True for flag in flags)
            or not isinstance(evidence, str)
            or not HEX_SHA256.fullmatch(evidence)
        ):
            raise ReleaseContractError("migration compatibility evidence is incomplete")
    elif compatibility["status"] == "pending":
        if any(flag is not False for flag in flags) or evidence is not None:
            raise ReleaseContractError("pending migration compatibility is inconsistent")
    else:
        raise ReleaseContractError("migration compatibility status is invalid")
    return dict(compatibility)


def _v2_verification_evidence(manifest: dict[str, object]) -> dict[str, str]:
    evidence = manifest["verificationEvidence"]
    if (
        not isinstance(evidence, dict)
        or any(not isinstance(name, str) for name in evidence)
        or any(not isinstance(digest, str) for digest in evidence.values())
        or any(not EVIDENCE_ID.fullmatch(name) for name in evidence)
        or any(not HEX_SHA256.fullmatch(digest) for digest in evidence.values())
    ):
        raise ReleaseContractError("verification evidence map is invalid")
    return {str(name): str(digest) for name, digest in evidence.items()}


def _v2_eligibility(manifest: dict[str, object]) -> tuple[bool, str | None]:
    eligible = manifest["eligible"]
    invalidation_reason = manifest["invalidationReason"]
    valid = isinstance(eligible, bool) and (
        (eligible and invalidation_reason is None)
        or (
            not eligible
            and isinstance(invalidation_reason, str)
            and 1 <= len(invalidation_reason) <= 160
        )
    )
    if not valid:
        raise ReleaseContractError("candidate eligibility state is invalid")
    return eligible, invalidation_reason


def _load_release_manifest_v2(
    resolved: Path,
    manifest: dict[str, object],
    expected_release: str,
) -> ReleaseManifest:
    required_fields = {
        "schemaVersion", "releaseId", "gitCommit", "sourceStateSha256",
        "dirtySourceSnapshot", "sourceDigestAlgorithm", "sourcePaths",
        "deletedSourcePaths", "sealedAt", "createdAt", "platform", "images",
        "artifacts", "migrationSet", "migrationCompatibility",
        "verificationEvidence", "eligible", "invalidationReason", "status",
        "nodeCount", "digestParity", "distribution",
    }
    if set(manifest) != required_fields:
        raise ReleaseContractError("release manifest v2 fields do not match the exact contract")
    if (
        manifest["schemaVersion"] != 2
        or manifest["status"] != "success"
        or manifest["platform"] != "linux/amd64"
        or manifest["nodeCount"] != 3
        or manifest["digestParity"] is not True
    ):
        raise ReleaseContractError("release manifest v2 status is invalid")
    release_id, git_commit, source_digest, source_paths = _v2_source_provenance(
        manifest,
        expected_release,
    )
    _validate_deleted_source_paths(manifest)
    parse_utc(manifest["sealedAt"], "sealedAt")
    parse_utc(manifest["createdAt"], "createdAt")
    images, release_files_sha256, artifacts = _v2_artifact_provenance(manifest)
    migration = _v2_migration_compatibility(manifest)
    evidence = _v2_verification_evidence(manifest)
    eligible, invalidation_reason = _v2_eligibility(manifest)

    return ReleaseManifest(
        path=resolved,
        sha256=file_sha256(resolved),
        release_id=release_id,
        git_commit=git_commit,
        source_state_sha256=source_digest,
        provenance_type="source-build",
        images=images,
        release_files_sha256=release_files_sha256,
        schema_version=2,
        source_paths=source_paths,
        artifacts=artifacts,
        migration_compatibility=dict(migration),
        verification_evidence=evidence,
        eligible=eligible,
        invalidation_reason=invalidation_reason,
    )


def load_release_manifest(path: Path, expected_release: str) -> ReleaseManifest:
    resolved, value = read_exact_json(path, "release manifest")
    if value.get("schemaVersion") == 2:
        return _load_release_manifest_v2(resolved, value, expected_release)
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
