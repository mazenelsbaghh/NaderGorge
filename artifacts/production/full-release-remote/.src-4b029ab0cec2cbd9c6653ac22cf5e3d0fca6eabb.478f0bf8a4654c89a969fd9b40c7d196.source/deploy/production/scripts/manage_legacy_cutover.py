#!/usr/bin/env python3
"""Prepare, atomically switch, or roll back the reviewed legacy Production candidate."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import shlex
import sys
import uuid
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Callable, Iterable

from clusterctl import Inventory, Node, reject_secret_keys
from ssh_transport import SshTarget, StrictSshTransport


LIVE_DATABASE = "massar_platform"
DATABASE_NAME = re.compile(
    r"^massar_platform_(?:candidate|rollback)_[0-9]{8}T[0-9]{6}Z$"
)
SHA256 = re.compile(r"^[0-9a-f]{64}$")
BACKUP_ID = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._:-]{7,127}$")
PG_BACKREST_LABEL = re.compile(
    r"^[0-9]{8}-[0-9]{6}F(?:_[0-9]{8}-[0-9]{6}[DI])?$"
)
RESTIC_SNAPSHOT_ID = re.compile(r"^[0-9a-f]{64}$")
RELEASE_ID = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$")
RESTORE_ID = re.compile(r"^legacy-restore-[0-9a-f]{32}$")
ALLOWED_AREAS = frozenset({"public", "protected", "private", "live-support"})
SOURCE_ARTIFACTS = frozenset({"database", "assets", "protected", "appData"})
STATE_ROOT = "/var/lib/massar/legacy-cutover"
IMPORT_ROOT = "/var/lib/massar/legacy-import"
SHARED_ROOT = "/srv/massar-shared"
EXECUTION_LOCK = f"{SHARED_ROOT}/.cluster-health/legacy-execution.lock"
CUTOVER_LOCK = f"{SHARED_ROOT}/.cluster-health/legacy-cutover.lock"
RECOVERY_MARKER = f"{SHARED_ROOT}/.cluster-health/legacy-recovery-required.json"
BACKUP_GATE_MAX_AGE = dt.timedelta(minutes=15)
BACKUP_GATE_FUTURE_SKEW = dt.timedelta(minutes=2)


class LegacyCutoverError(RuntimeError):
    """Raised when the cutover cannot prove a safe state transition."""


@dataclass(frozen=True)
class FileEntry:
    archive_path: str
    area: str
    relative_path: str
    size: int
    sha256: str


@dataclass(frozen=True)
class CandidateBundle:
    path: Path
    backup_id: str
    candidate_mode: str
    eligible_for_cutover: bool
    source_backup_id: str
    restore_id: str
    restore_evidence_sha256: str
    validation_evidence_sha256: str
    dump_path: Path
    dump_sha256: str
    files_path: Path
    files_sha256: str
    migration_ids: tuple[str, ...]
    table_counts: dict[str, int]
    files: tuple[FileEntry, ...]


@dataclass(frozen=True)
class CutoverOutcome:
    evidence: dict[str, object]
    error: Exception | None


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def iso8601(value: dt.datetime) -> str:
    return value.isoformat().replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_relative_path(value: str) -> str:
    if not value or "\\" in value or "\x00" in value:
        raise LegacyCutoverError("file paths must be non-empty normalized POSIX paths")
    path = PurePosixPath(value)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        raise LegacyCutoverError(f"unsafe file path: {value}")
    normalized = path.as_posix()
    if normalized != value:
        raise LegacyCutoverError(f"file path is not normalized: {value}")
    return normalized


def validated_database_name(value: str, expected_kind: str) -> str:
    if not DATABASE_NAME.fullmatch(value):
        raise LegacyCutoverError("database name does not match the approved timestamped pattern")
    if f"_{expected_kind}_" not in value:
        raise LegacyCutoverError(f"expected a {expected_kind} database name")
    return value


def validated_operation_id(value: object) -> str:
    operation_id = str(value)
    try:
        parsed = uuid.UUID(operation_id)
    except ValueError as exc:
        raise LegacyCutoverError("operation ID is not a canonical UUID") from exc
    if str(parsed) != operation_id:
        raise LegacyCutoverError("operation ID is not a canonical UUID")
    return operation_id


def load_cutover_inventory(path: Path) -> Inventory:
    """Load only the reviewed, secret-free three-node inventory."""
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise LegacyCutoverError("inventory is missing or invalid") from exc
    if not isinstance(raw, dict):
        raise LegacyCutoverError("inventory root must be a mapping")
    reject_secret_keys(raw)
    if set(raw) != {"cluster", "nodes", "hostnames"}:
        raise LegacyCutoverError("inventory must contain only cluster, nodes, and hostnames")
    cluster = dict(raw["cluster"])
    if cluster.get("name") != "massar-production" or cluster.get("ssh_user") != "massar-ops":
        raise LegacyCutoverError("cutover requires the approved massar-production inventory")
    nodes_raw = raw.get("nodes")
    if not isinstance(nodes_raw, list) or len(nodes_raw) != 3:
        raise LegacyCutoverError("inventory must contain exactly three nodes")
    nodes = tuple(
        Node(
            id=str(item["id"]),
            ssh_alias=str(item["ssh_alias"]),
            public_address=str(item["public_address"]),
            overlay_address=str(item["overlay_address"]),
            roles=tuple(item["roles"]),
        )
        for item in nodes_raw
    )
    if tuple(node.id for node in nodes) != ("node-1", "node-2", "node-3"):
        raise LegacyCutoverError("inventory nodes must be ordered node-1, node-2, node-3")
    if len({node.public_address for node in nodes}) != 3:
        raise LegacyCutoverError("inventory public addresses must be unique")
    return Inventory(path.resolve(), cluster, nodes, dict(raw["hostnames"]))


def parse_file_entry(value: object) -> FileEntry:
    if not isinstance(value, dict) or set(value) != {
        "archivePath", "area", "relativePath", "size", "sha256"
    }:
        raise LegacyCutoverError("each file entry must use the exact reviewed fields")
    area = str(value["area"])
    if area not in ALLOWED_AREAS:
        raise LegacyCutoverError(f"unsupported shared-storage area: {area}")
    relative = safe_relative_path(str(value["relativePath"]))
    archive = safe_relative_path(str(value["archivePath"]))
    if archive != f"{area}/{relative}":
        raise LegacyCutoverError("archivePath must equal area/relativePath")
    digest = str(value["sha256"])
    size = value["size"]
    if not SHA256.fullmatch(digest) or not isinstance(size, int) or size < 0:
        raise LegacyCutoverError("file size or SHA-256 is invalid")
    return FileEntry(archive, area, relative, size, digest)


def load_bundle(
    path: Path,
    *,
    verify_artifacts: bool = True,
    require_cutover_eligible: bool = True,
) -> CandidateBundle:
    manifest_path = path.expanduser().resolve()
    try:
        raw = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise LegacyCutoverError("candidate bundle manifest is missing or invalid") from exc
    required = {
        "schemaVersion", "status", "backupId", "candidateDump", "fileArchive",
        "migrationIds", "tableCounts", "files", "candidateMode",
        "eligibleForCutover", "sourceCapture", "sourceBackupId", "restoreId",
        "restoreEvidenceSha256", "validationEvidenceSha256",
    }
    if not isinstance(raw, dict) or set(raw) != required:
        raise LegacyCutoverError("candidate bundle manifest fields do not match the contract")
    if raw["schemaVersion"] != 2 or raw["status"] != "success":
        raise LegacyCutoverError("candidate bundle must be a successful schemaVersion 2 artifact")
    backup_id = str(raw["backupId"])
    if not BACKUP_ID.fullmatch(backup_id):
        raise LegacyCutoverError("candidate backupId is invalid")
    candidate_mode = raw["candidateMode"]
    eligible_for_cutover = raw["eligibleForCutover"]
    source_capture = raw["sourceCapture"]
    source_backup_id = raw["sourceBackupId"]
    restore_id = raw["restoreId"]
    restore_evidence_sha256 = raw["restoreEvidenceSha256"]
    validation_evidence_sha256 = raw["validationEvidenceSha256"]
    if (
        candidate_mode not in {"rehearsal", "authoritative-final"}
        or not isinstance(eligible_for_cutover, bool)
        or eligible_for_cutover is not (candidate_mode == "authoritative-final")
        or not isinstance(source_backup_id, str)
        or not BACKUP_ID.fullmatch(source_backup_id)
        or not isinstance(restore_id, str)
        or not RESTORE_ID.fullmatch(restore_id)
        or not isinstance(restore_evidence_sha256, str)
        or not SHA256.fullmatch(restore_evidence_sha256)
        or not isinstance(validation_evidence_sha256, str)
        or not SHA256.fullmatch(validation_evidence_sha256)
        or not isinstance(source_capture, dict)
        or set(source_capture) != {
            "backupId", "sourceMode", "authoritativeSource",
            "writersFrozenAtCompletion", "manifestSha256",
            "captureEvidenceSha256", "artifactSha256", "sourceHost", "sourceUser",
        }
        or source_capture["backupId"] != source_backup_id
        or not isinstance(source_capture["sourceHost"], str)
        or not source_capture["sourceHost"]
        or not isinstance(source_capture["sourceUser"], str)
        or not source_capture["sourceUser"]
        or source_capture["sourceMode"]
        not in {"read-only", "frozen-writers", "frozen-writers-held"}
        or source_capture["authoritativeSource"] is not (
            source_capture["sourceMode"] == "frozen-writers-held"
        )
        or source_capture["writersFrozenAtCompletion"]
        is not source_capture["authoritativeSource"]
        or not isinstance(source_capture["manifestSha256"], str)
        or not SHA256.fullmatch(source_capture["manifestSha256"])
        or not isinstance(source_capture["captureEvidenceSha256"], str)
        or not SHA256.fullmatch(source_capture["captureEvidenceSha256"])
        or not isinstance(source_capture["artifactSha256"], dict)
        or set(source_capture["artifactSha256"]) != SOURCE_ARTIFACTS
        or any(
            not isinstance(digest, str) or not SHA256.fullmatch(digest)
            for digest in source_capture["artifactSha256"].values()
        )
        or (
            eligible_for_cutover
            and source_capture["authoritativeSource"] is not True
        )
    ):
        raise LegacyCutoverError(
            "candidate provenance does not prove a coherent source capture"
        )
    if require_cutover_eligible and not eligible_for_cutover:
        raise LegacyCutoverError(
            "rehearsal candidates are not eligible for Production cutover"
        )

    def artifact(name: str) -> tuple[Path, str]:
        value = raw[name]
        if not isinstance(value, dict) or set(value) != {"path", "sha256"}:
            raise LegacyCutoverError(f"{name} must contain only path and sha256")
        artifact_path = Path(str(value["path"])).expanduser().resolve()
        digest = str(value["sha256"])
        if not SHA256.fullmatch(digest):
            raise LegacyCutoverError(f"{name} SHA-256 is invalid")
        if verify_artifacts:
            if not artifact_path.is_file() or sha256_file(artifact_path) != digest:
                raise LegacyCutoverError(f"{name} file does not match its manifest SHA-256")
        return artifact_path, digest

    dump_path, dump_digest = artifact("candidateDump")
    files_path, files_digest = artifact("fileArchive")
    migrations_raw = raw["migrationIds"]
    if (
        not isinstance(migrations_raw, list)
        or not migrations_raw
        or any(not isinstance(value, str) or not value for value in migrations_raw)
        or migrations_raw != sorted(set(migrations_raw))
    ):
        raise LegacyCutoverError("migrationIds must be a non-empty ordered unique list")
    counts_raw = raw["tableCounts"]
    if (
        not isinstance(counts_raw, dict)
        or "__EFMigrationsHistory" not in counts_raw
        or any(not isinstance(key, str) or not isinstance(value, int) or value < 0
               for key, value in counts_raw.items())
    ):
        raise LegacyCutoverError("tableCounts must contain non-negative exact counts")
    entries_raw = raw["files"]
    if not isinstance(entries_raw, list):
        raise LegacyCutoverError("files must be a list")
    entries = tuple(parse_file_entry(value) for value in entries_raw)
    if any(entry.size > 2 * 1024**3 for entry in entries) or sum(
        entry.size for entry in entries
    ) > 20 * 1024**3:
        raise LegacyCutoverError("candidate file archive exceeds the reviewed size bounds")
    archive_paths = [entry.archive_path for entry in entries]
    destinations = [(entry.area, entry.relative_path) for entry in entries]
    if len(archive_paths) != len(set(archive_paths)) or len(destinations) != len(set(destinations)):
        raise LegacyCutoverError("file manifest contains duplicate archive or destination paths")
    return CandidateBundle(
        manifest_path,
        backup_id,
        candidate_mode,
        eligible_for_cutover,
        source_backup_id,
        restore_id,
        restore_evidence_sha256,
        validation_evidence_sha256,
        dump_path,
        dump_digest,
        files_path,
        files_digest,
        tuple(migrations_raw),
        dict(counts_raw),
        entries,
    )


def validate_database_snapshot(
    expected_migrations: Iterable[str],
    expected_counts: dict[str, int],
    actual_migrations: Iterable[str],
    actual_counts: dict[str, int],
) -> None:
    if tuple(actual_migrations) != tuple(expected_migrations):
        raise LegacyCutoverError("candidate migration history is not an exact ordered match")
    if actual_counts != expected_counts:
        raise LegacyCutoverError("candidate table names/counts are not an exact match")


def classify_collision(
    destination_exists: bool,
    expected_sha256: str,
    actual_sha256: str | None,
) -> str:
    if not destination_exists:
        return "CREATE"
    if actual_sha256 == expected_sha256:
        return "SKIP_IDENTICAL"
    return "BLOCK_COLLISION"


def validate_backup_gate(
    path: Path,
    *,
    prepared_at: dt.datetime,
    now: dt.datetime,
    cluster_name: str,
    inventory_sha256: str,
    release_id: str,
    candidate_database: str,
    candidate_prepared_at: str,
    candidate_manifest_sha256: str,
    operation_id: str,
    candidate_backup_id: str,
    candidate_dump_sha256: str,
    file_archive_sha256: str,
) -> dict[str, object]:
    if (
        not SHA256.fullmatch(inventory_sha256)
        or not RELEASE_ID.fullmatch(release_id)
        or not SHA256.fullmatch(candidate_manifest_sha256)
        or not SHA256.fullmatch(candidate_dump_sha256)
        or not SHA256.fullmatch(file_archive_sha256)
    ):
        raise LegacyCutoverError("backup gate expected bindings are invalid")
    validated_operation_id(operation_id)
    if path.is_symlink() or not path.is_file():
        raise LegacyCutoverError("backup gate must be a regular non-symlink file")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise LegacyCutoverError("backup gate is missing or invalid") from exc
    required = {
        "schemaVersion", "status", "databaseBackupId", "fileSnapshotId",
        "databaseRestoreVerified", "fileRestoreVerified", "capturedAt",
        "clusterId", "inventorySha256", "releaseId", "candidateDatabase",
        "candidatePreparedAt", "candidateManifestSha256", "operationId",
        "candidateBackupId",
        "candidateDumpSha256", "fileArchiveSha256",
        "databaseBackupEvidenceSha256", "databaseRestoreEvidenceSha256",
        "fileBackupEvidenceSha256", "fileRestoreEvidenceSha256",
    }
    if not isinstance(value, dict) or set(value) != required:
        raise LegacyCutoverError("backup gate fields do not match the cutover contract")
    if (
        value["schemaVersion"] != 2
        or value["status"] != "success"
        or value["databaseRestoreVerified"] is not True
        or value["fileRestoreVerified"] is not True
        or not PG_BACKREST_LABEL.fullmatch(str(value["databaseBackupId"]))
        or not RESTIC_SNAPSHOT_ID.fullmatch(str(value["fileSnapshotId"]))
        or value["clusterId"] != cluster_name
        or value["inventorySha256"] != inventory_sha256
        or value["releaseId"] != release_id
        or value["candidateDatabase"] != candidate_database
        or value["candidatePreparedAt"] != candidate_prepared_at
        or value["candidateManifestSha256"] != candidate_manifest_sha256
        or value["operationId"] != operation_id
        or value["candidateBackupId"] != candidate_backup_id
        or value["candidateDumpSha256"] != candidate_dump_sha256
        or value["fileArchiveSha256"] != file_archive_sha256
        or any(
            not SHA256.fullmatch(str(value[field]))
            for field in (
                "databaseBackupEvidenceSha256",
                "databaseRestoreEvidenceSha256",
                "fileBackupEvidenceSha256",
                "fileRestoreEvidenceSha256",
            )
        )
    ):
        raise LegacyCutoverError(
            "backup gate does not prove bound, digest-matched isolated restores"
        )
    try:
        captured = dt.datetime.fromisoformat(str(value["capturedAt"]).replace("Z", "+00:00"))
    except ValueError as exc:
        raise LegacyCutoverError("backup gate capturedAt is invalid") from exc
    if now.tzinfo is None:
        raise LegacyCutoverError("backup gate validator requires a timezone-aware clock")
    if (
        captured.tzinfo is None
        or captured < prepared_at
        or captured > now + BACKUP_GATE_FUTURE_SKEW
        or now - captured > BACKUP_GATE_MAX_AGE
    ):
        raise LegacyCutoverError(
            "backup gate must be recent, not future-dated, and captured after preparation"
        )
    return value


def write_evidence(path: Path, payload: dict[str, object]) -> None:
    destination = path.expanduser().resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = destination.with_name(f".{destination.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    os.chmod(temporary, 0o640)
    os.replace(temporary, destination)


class LegacyCutover:
    """Orchestrate a fail-closed candidate import through strict SSH."""

    def __init__(
        self,
        inventory: Inventory,
        transport: StrictSshTransport,
        *,
        now: Callable[[], dt.datetime] = utc_now,
    ) -> None:
        self.inventory = inventory
        self.transport = transport
        self.now = now
        self.control = inventory.nodes[0]

    def target(self, node: Node) -> SshTarget:
        return SshTarget(
            node.id,
            node.public_address,
            str(self.inventory.cluster["ssh_user"]),
        )

    def remote(
        self,
        node: Node,
        script: str,
        *,
        timeout_seconds: int = 60,
        check: bool = True,
    ) -> str:
        result = self.transport.run(
            self.target(node),
            ("bash", "-lc", script),
            timeout_seconds=timeout_seconds,
            check=check,
        )
        return result.stdout.strip()

    def inspect_cluster(self, *, allow_recovery: bool = False) -> None:
        recovery_guard = ":" if allow_recovery else f"test ! -e {RECOVERY_MARKER}"
        for node in self.inventory.nodes:
            self.remote(
                node,
                f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
systemctl is-active --quiet docker etcd patroni haproxy glusterd massar-backup-bucket
mountpoint -q {SHARED_ROOT}
{recovery_guard}
""",
            )
        overlay = " ".join(shlex.quote(node.overlay_address) for node in self.inventory.nodes)
        self.remote(
            self.control,
            f"""
set -euo pipefail
primary_count=0
replica_count=0
for address in {overlay}; do
  if curl --fail --silent "http://$address:8008/primary" >/dev/null; then
    primary_count=$((primary_count + 1))
  fi
  if curl --fail --silent "http://$address:8008/replica" >/dev/null; then
    replica_count=$((replica_count + 1))
  fi
done
test "$primary_count" -eq 1
test "$replica_count" -eq 2
curl --fail --silent http://127.0.0.1:8008/cluster |
  python3 -c 'import json,sys
value=json.load(sys.stdin); members=value.get("members",[])
assert len(members)==3
assert sum(item.get("role")=="leader" and item.get("state")=="running" for item in members)==1
assert sum(item.get("role") in ("replica","sync_standby") and item.get("state")=="running" for item in members)==2
assert all(int(item.get("lag",0) or 0)<=16777216 for item in members)'
summary="$(sudo /usr/sbin/gluster volume heal massar-shared info summary)"
split="$(sudo /usr/sbin/gluster volume heal massar-shared info split-brain)"
test "$(printf '%s\n' "$summary" | grep -Ec 'Number of entries:[[:space:]]+0')" -ge 3
test "$(printf '%s\n' "$split" | grep -Ec 'Number of entries:[[:space:]]+0')" -ge 3
""",
        )

    def ensure_operator_roots(self) -> None:
        """Create fixed, operator-owned roots before any resumable mutation."""
        self.remote(
            self.control,
            f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
sudo /usr/bin/install -d -m 0700 -o massar-ops -g massar \
  {IMPORT_ROOT} {STATE_ROOT} {SHARED_ROOT}/.cluster-health
sudo /usr/bin/install -d -m 2755 -o massar-ops -g massar {SHARED_ROOT}/public
sudo /usr/bin/install -d -m 2750 -o massar-ops -g massar \
  {SHARED_ROOT}/protected {SHARED_ROOT}/private {SHARED_ROOT}/live-support
test "$(stat -c '%U:%G:%a' {IMPORT_ROOT})" = "massar-ops:massar:700"
test "$(stat -c '%U:%G:%a' {STATE_ROOT})" = "massar-ops:massar:700"
test "$(stat -c '%U:%G:%a' {SHARED_ROOT}/.cluster-health)" = "massar-ops:massar:700"
""",
        )

    def acquire_execution_mutex(
        self,
        candidate: str,
        action: str,
        claim_id: str,
    ) -> None:
        """Atomically claim the one cluster-wide mutation mutex."""
        self.remote(
            self.control,
            f"""
set -euo pipefail
lock={EXECUTION_LOCK}
created=0
cleanup_partial_lock() {{
  if test "$created" -eq 1; then
    rm -f "$lock/candidate" "$lock/action" "$lock/claim-id"
    rmdir "$lock" 2>/dev/null || true
  fi
}}
mkdir -m 0700 "$lock"
created=1
trap cleanup_partial_lock ERR
printf '%s\n' {shlex.quote(candidate)} > "$lock/candidate"
printf '%s\n' {shlex.quote(action)} > "$lock/action"
printf '%s\n' {shlex.quote(claim_id)} > "$lock/claim-id"
chmod 0600 "$lock/candidate" "$lock/action" "$lock/claim-id"
sync "$lock/candidate" "$lock/action" "$lock/claim-id"
trap - ERR
""",
        )

    def release_execution_mutex(self, claim_id: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
lock={EXECUTION_LOCK}
test "$(cat "$lock/claim-id")" = {shlex.quote(claim_id)}
rm "$lock/candidate" "$lock/action" "$lock/claim-id"
rmdir "$lock"
""",
        )

    def mark_recovery_required(
        self,
        candidate: str,
        operation_id: str,
        phase: str,
        reason: str,
    ) -> None:
        payload = json.dumps(
            {
                "schemaVersion": 1,
                "clusterName": "massar-production",
                "candidateDatabase": candidate,
                "operationId": operation_id,
                "phase": phase,
                "reason": reason[:500],
                "markedAt": iso8601(self.now()),
            },
            separators=(",", ":"),
        )
        self.remote(
            self.control,
            f"""
set -euo pipefail
temporary={RECOVERY_MARKER}.tmp
printf '%s\n' {shlex.quote(payload)} > "$temporary"
chmod 0600 "$temporary"
mv "$temporary" {RECOVERY_MARKER}
sync {RECOVERY_MARKER}
""",
        )

    def assert_recovery_marker_matches(
        self,
        candidate: str,
        operation_id: str,
    ) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
if test -e {RECOVERY_MARKER}; then
  python3 - {RECOVERY_MARKER} {shlex.quote(candidate)} {shlex.quote(operation_id)} <<'PY'
import json,sys
value=json.load(open(sys.argv[1],encoding="utf-8"))
if value.get("candidateDatabase") != sys.argv[2] or value.get("operationId") != sys.argv[3]:
    raise SystemExit("recovery marker belongs to another transition")
PY
fi
""",
        )

    def clear_recovery_marker(self, candidate: str, operation_id: str) -> None:
        self.assert_recovery_marker_matches(candidate, operation_id)
        self.remote(
            self.control,
            f"rm -f {RECOVERY_MARKER}",
        )

    def upload_bundle(
        self,
        bundle: CandidateBundle,
        passphrase_file: Path,
        operation_id: str,
    ) -> dict[str, str]:
        passphrase = passphrase_file.expanduser().resolve()
        if not passphrase.is_file() or passphrase.stat().st_mode & 0o077:
            raise LegacyCutoverError("passphrase file must be a mode-0600 regular file")
        prefix = f"/tmp/massar-legacy-{operation_id}"
        paths = {
            "manifest": f"{prefix}-manifest.json",
            "dump": f"{prefix}-candidate.dump.gpg",
            "files": f"{prefix}-files.tar.gpg",
            "passphrase": f"{prefix}-passphrase",
        }
        for source, destination in (
            (bundle.path, paths["manifest"]),
            (bundle.dump_path, paths["dump"]),
            (bundle.files_path, paths["files"]),
            (passphrase, paths["passphrase"]),
        ):
            self.transport.copy(
                self.target(self.control),
                source,
                destination,
                timeout_seconds=900,
            )
        return paths

    def prepare_remote(
        self,
        candidate: str,
        bundle: CandidateBundle,
        uploaded: dict[str, str],
        operation_id: str,
    ) -> dict[str, object]:
        manifest = shlex.quote(uploaded["manifest"])
        dump = shlex.quote(uploaded["dump"])
        archive = shlex.quote(uploaded["files"])
        passphrase = shlex.quote(uploaded["passphrase"])
        candidate_q = shlex.quote(candidate)
        stage = f"{IMPORT_ROOT}/{operation_id}"
        script = f"""
set -euo pipefail
umask 077
test "$(cat /etc/massar/cluster-id)" = "massar-production"
candidate={candidate_q}
stage={shlex.quote(stage)}
test ! -e "$stage"
install -d -m 0700 "$stage"
install -m 0600 {manifest} "$stage/manifest.json"
install -m 0600 {dump} "$stage/candidate.dump.gpg"
install -m 0600 {archive} "$stage/files.tar.gpg"
install -m 0600 {passphrase} "$stage/passphrase"
rm -f {manifest} {dump} {archive} {passphrase}
test "$(sha256sum "$stage/candidate.dump.gpg" | awk '{{print $1}}')" = {bundle.dump_sha256}
test "$(sha256sum "$stage/files.tar.gpg" | awk '{{print $1}}')" = {bundle.files_sha256}
psql_admin() {{
  sudo docker run --rm -i --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 "$@"' sh "$@"
}}
test "$(printf "select count(*) from pg_database where datname in ('massar_platform','%s');\n" "$candidate" | psql_admin)" = "1"
printf 'create database "%s" with owner massar_app template template0;\n' "$candidate" | psql_admin
touch "$stage/candidate-created"
cleanup_db() {{
  printf 'alter database "%s" with allow_connections false;\n' "$candidate" | psql_admin >/dev/null 2>&1 || true
  printf "select pg_terminate_backend(pid) from pg_stat_activity where datname='%s' and pid<>pg_backend_pid();\n" "$candidate" | psql_admin >/dev/null 2>&1 || true
  printf 'drop database if exists "%s";\n' "$candidate" | psql_admin >/dev/null 2>&1 || true
}}
trap cleanup_db ERR
gpg --batch --quiet --decrypt --pinentry-mode loopback \
  --passphrase-file "$stage/passphrase" "$stage/candidate.dump.gpg" |
  sudo docker run --rm -i --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec pg_restore "$@"' sh \
      -h 127.0.0.1 -p 6432 -U postgres -d "$candidate" \
      --single-transaction --exit-on-error --no-owner --no-acl --role=massar_app
python3 - "$stage" {shlex.quote(SHARED_ROOT)} <<'PY'
import hashlib,json,os,pathlib,subprocess,sys,tarfile
stage=pathlib.Path(sys.argv[1]); shared=pathlib.Path(sys.argv[2])
manifest=json.loads((stage/"manifest.json").read_text())
entries={{item["archivePath"]:item for item in manifest["files"]}}
source=stage/"extracted"; source.mkdir(mode=0o700)
decrypt=subprocess.Popen([
    "gpg","--batch","--quiet","--decrypt","--pinentry-mode","loopback",
    "--passphrase-file",str(stage/"passphrase"),str(stage/"files.tar.gpg")
],stdout=subprocess.PIPE,stderr=subprocess.PIPE)
assert decrypt.stdout is not None
seen=set()
with tarfile.open(fileobj=decrypt.stdout,mode="r|*") as archive:
    for member in archive:
        name=pathlib.PurePosixPath(member.name)
        normalized=name.as_posix()
        if not member.isfile() or name.is_absolute() or any(p in ("",".","..") for p in name.parts):
            raise SystemExit("unsafe archive member")
        if normalized not in entries or normalized in seen:
            raise SystemExit("archive/manifest member mismatch")
        if member.size != entries[normalized]["size"]:
            raise SystemExit("archive member size differs from the manifest")
        seen.add(normalized)
        target=source.joinpath(*name.parts)
        target.parent.mkdir(parents=True,exist_ok=True)
        data=archive.extractfile(member)
        if data is None:
            raise SystemExit("archive member is unreadable")
        digest=hashlib.sha256()
        with target.open("xb") as output:
            while True:
                chunk=data.read(1024*1024)
                if not chunk: break
                digest.update(chunk); output.write(chunk)
        expected=entries[normalized]
        if target.stat().st_size != expected["size"] or digest.hexdigest() != expected["sha256"]:
            raise SystemExit("archive member checksum mismatch")
decrypt.stdout.close()
error=decrypt.stderr.read().decode(errors="replace") if decrypt.stderr else ""
if decrypt.wait() or seen != set(entries):
    raise SystemExit(error[:300] or "archive set mismatch")
plan=[]
for name,item in sorted(entries.items()):
    area_root=shared/item["area"]
    if not area_root.is_dir() or area_root.is_symlink():
        raise SystemExit("shared-storage area root is missing or unsafe")
    destination=area_root/item["relativePath"]
    current=None
    if destination.exists():
        if (not destination.is_file() or destination.is_symlink()
                or destination.stat().st_nlink != 1):
            action="BLOCK_COLLISION"
        else:
            digest=hashlib.sha256()
            with destination.open("rb") as stream:
                for chunk in iter(lambda:stream.read(1024*1024),b""): digest.update(chunk)
            current=digest.hexdigest()
            action="SKIP_IDENTICAL" if current==item["sha256"] else "BLOCK_COLLISION"
    else:
        action="CREATE"
    plan.append({{"area":item["area"],"relativePath":item["relativePath"],
                 "sha256":item["sha256"],"size":item["size"],"action":action,
                 "existingSha256":current}})
(stage/"collision-plan.json").write_text(json.dumps(plan,indent=2)+"\\n")
if any(item["action"]=="BLOCK_COLLISION" for item in plan):
    raise SystemExit("no-overwrite collision detected")
PY
actual="$(
  printf "select 'migration|'||\\"MigrationId\\" from \\"__EFMigrationsHistory\\" order by \\"MigrationId\\"; select 'table|'||quote_ident(c.relname)||'|'||(xpath('/row/count/text()',query_to_xml(format('select count(*) as count from %I',c.relname),false,true,'')))[1]::text from pg_class c where c.relnamespace='public'::regnamespace and c.relkind in ('r','p') order by c.relname;\n" |
  sudo docker run --rm -i --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
      -h 127.0.0.1 -p 6432 -U postgres -d "$candidate" -XAt -v ON_ERROR_STOP=1
)"
printf '%s\n' "$actual" > "$stage/database-snapshot.txt"
python3 - "$stage/manifest.json" "$stage/database-snapshot.txt" <<'PY'
import json,sys
manifest=json.load(open(sys.argv[1],encoding="utf-8"))
migrations=[]; counts={{}}
for line in open(sys.argv[2],encoding="utf-8"):
    kind,_,rest=line.rstrip("\\n").partition("|")
    if kind=="migration": migrations.append(rest)
    elif kind=="table":
        table,_,count=rest.partition("|"); counts[table.strip('"')]=int(count)
if migrations != manifest["migrationIds"]: raise SystemExit("exact migration-set mismatch")
if counts != manifest["tableCounts"]: raise SystemExit("exact table-count mismatch")
PY
critical="$(
  printf "select (select count(*) from pg_index where not indisvalid)+(select count(*) from pg_constraint where connamespace='public'::regnamespace and not convalidated)+(select count(*) from pg_class c where c.relnamespace='public'::regnamespace and c.relkind in ('r','p','S') and pg_get_userbyid(c.relowner)<>'massar_app');\n" |
  sudo docker run --rm -i --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
      -h 127.0.0.1 -p 6432 -U postgres -d "$candidate" -XAt -v ON_ERROR_STOP=1
)"
test "$critical" = "0"
printf 'alter database "%s" with allow_connections false;\n' "$candidate" | psql_admin
printf "select pg_terminate_backend(pid) from pg_stat_activity where datname='%s' and pid<>pg_backend_pid();\n" "$candidate" | psql_admin >/dev/null
release_id="$(python3 -c 'import json; print(json.load(open("/opt/massar/current/manifest.json",encoding="utf-8"))["releaseId"])')"
printf '%s' "$release_id" | grep -Eq '^(git-[0-9a-f]{{7,40}}|src-[0-9a-f]{{40}})$'
python3 - "$stage" {STATE_ROOT}/{candidate}.json "$candidate" \
  {shlex.quote(operation_id)} "$release_id" <<'PY'
import datetime,json,pathlib,sys
stage=pathlib.Path(sys.argv[1]); target=pathlib.Path(sys.argv[2])
plan=json.loads((stage/"collision-plan.json").read_text())
payload={{"schemaVersion":1,"candidateDatabase":sys.argv[3],"operationId":sys.argv[4],
 "releaseId":sys.argv[5],
 "phase":"prepared","preparedAt":datetime.datetime.now(datetime.timezone.utc).isoformat().replace("+00:00","Z"),
 "writesOpened":False,"rollbackDatabase":None,"createdFiles":[],"filesPublished":False,
 "stagingRoot":str(stage),
 "backupId":{json.dumps(bundle.backup_id)},
 "candidateDumpSha256":{json.dumps(bundle.dump_sha256)},
 "fileArchiveSha256":{json.dumps(bundle.files_sha256)},
 "candidateManifestSha256":{json.dumps(sha256_file(bundle.path))},
 "inventorySha256":{json.dumps(sha256_file(self.inventory.path))},
 "expectedMigrationCount":len(json.loads((stage/"manifest.json").read_text())["migrationIds"]),
 "filePlan":plan,
 "collisionManifestSha256":__import__("hashlib").sha256(
     json.dumps(plan,sort_keys=True,separators=(",",":")).encode()).hexdigest(),
 "identicalFileCount":sum(1 for item in plan if item["action"]=="SKIP_IDENTICAL"),
 "collisionCount":sum(1 for item in plan if item["action"]=="BLOCK_COLLISION")}}
temp=target.with_suffix(".tmp"); temp.write_text(json.dumps(payload,indent=2)+"\\n")
temp.chmod(0o640); temp.replace(target)
print(json.dumps(payload))
PY
trap - ERR
rm -f "$stage/candidate.dump.gpg" "$stage/files.tar.gpg" "$stage/passphrase"
"""
        output = self.remote(self.control, script, timeout_seconds=1800)
        try:
            return json.loads(output.splitlines()[-1])
        except (json.JSONDecodeError, IndexError) as exc:
            raise LegacyCutoverError("prepare did not return valid state evidence") from exc

    def cleanup_prepare(self, candidate: str, operation_id: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
candidate={shlex.quote(candidate)}
stage={IMPORT_ROOT}/{shlex.quote(operation_id)}
if test -e "$stage/candidate-created"; then
  sudo docker run --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec dropdb "$@"' sh \
    --if-exists --force -h 127.0.0.1 -p 6432 -U postgres "$candidate" >/dev/null
fi
rm -rf -- "$stage"
rm -f {STATE_ROOT}/{candidate}.json
rm -f /tmp/massar-legacy-{shlex.quote(operation_id)}-*
""",
            timeout_seconds=300,
        )

    def read_state(self, candidate: str) -> dict[str, object]:
        output = self.remote(
            self.control,
            f"cat {STATE_ROOT}/{shlex.quote(candidate)}.json",
        )
        try:
            state = json.loads(output)
        except json.JSONDecodeError as exc:
            raise LegacyCutoverError("remote cutover state is invalid") from exc
        if state.get("candidateDatabase") != candidate:
            raise LegacyCutoverError("remote state belongs to a different candidate")
        validated_operation_id(state.get("operationId"))
        return state

    def write_state(self, candidate: str, state: dict[str, object]) -> None:
        encoded = json.dumps(state, separators=(",", ":"))
        self.remote(
            self.control,
            f"""
set -euo pipefail
temporary={STATE_ROOT}/{shlex.quote(candidate)}.json.tmp
printf '%s\n' {shlex.quote(encoded)} > "$temporary"
chmod 0640 "$temporary"
mv "$temporary" {STATE_ROOT}/{shlex.quote(candidate)}.json
""",
        )

    def publish_staged_files(
        self,
        candidate: str,
        state: dict[str, object],
    ) -> dict[str, object]:
        """Publish staged files only while applications and database writes are quiesced."""
        operation_id = validated_operation_id(state["operationId"])
        expected_stage = f"{IMPORT_ROOT}/{operation_id}"
        if state.get("stagingRoot") != expected_stage:
            raise LegacyCutoverError("prepared state has an unexpected staging root")
        output = self.remote(
            self.control,
            f"""
set -euo pipefail
python3 - {shlex.quote(expected_stage)} {shlex.quote(SHARED_ROOT)} \
  {STATE_ROOT}/{shlex.quote(candidate)}.json {shlex.quote(operation_id)} <<'PY'
import hashlib,json,os,pathlib,stat,sys,uuid,grp

stage=pathlib.Path(sys.argv[1])
shared=sys.argv[2]
state_path=pathlib.Path(sys.argv[3])
operation_id=sys.argv[4]
if str(stage) != {json.dumps(IMPORT_ROOT)}+"/"+operation_id or stage.is_symlink():
    raise SystemExit("unsafe staging root")
plan=json.loads((stage/"collision-plan.json").read_text())
journal=stage/"publish-journal.jsonl"
directory_journal=stage/"directory-journal.jsonl"
events={{}}
if journal.exists():
    with journal.open(encoding="utf-8") as stream:
        for line in stream:
            if not line.strip(): continue
            row=json.loads(line)
            events[(row["area"],row["relativePath"])]=row

def append_event(row):
    with journal.open("a",encoding="utf-8") as stream:
        stream.write(json.dumps(row,separators=(",",":"))+"\\n")
        stream.flush(); os.fsync(stream.fileno())

created_directories=[]
if directory_journal.exists():
    with directory_journal.open(encoding="utf-8") as stream:
        created_directories=[json.loads(line) for line in stream if line.strip()]

def append_directory(row):
    with directory_journal.open("a",encoding="utf-8") as stream:
        stream.write(json.dumps(row,separators=(",",":"))+"\\n")
        stream.flush(); os.fsync(stream.fileno())
    created_directories.append(row)

def digest_fd(fd):
    result=hashlib.sha256()
    os.lseek(fd,0,os.SEEK_SET)
    while True:
        chunk=os.read(fd,1024*1024)
        if not chunk: break
        result.update(chunk)
    os.lseek(fd,0,os.SEEK_SET)
    return result.hexdigest()

flags=os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW
root_fd=os.open(shared,flags)
massar_gid=grp.getgrnam("massar").gr_gid
operator_uid=os.getuid()
created=[]
def require_app_traversal(fd):
    observed=os.fstat(fd)
    if not (
        observed.st_mode & stat.S_IXOTH
        or (observed.st_gid==massar_gid and observed.st_mode & stat.S_IXGRP)
    ):
        raise SystemExit("shared directory is not traversable by the application")
try:
    for item in plan:
        area=item["area"]; relative=pathlib.PurePosixPath(item["relativePath"])
        if area not in {sorted(ALLOWED_AREAS)!r} or relative.is_absolute() or any(
            part in ("",".","..") for part in relative.parts
        ):
            raise SystemExit("unsafe publication path")
        area_fd=os.open(area,flags,dir_fd=root_fd)
        require_app_traversal(area_fd)
        parent_fd=area_fd
        opened=[area_fd]
        try:
            directory_mode=0o755 if area=="public" else 0o750
            prefix=[]
            for part in relative.parts[:-1]:
                prefix.append(part)
                try:
                    os.mkdir(part,directory_mode,dir_fd=parent_fd)
                    os.chown(part,operator_uid,massar_gid,dir_fd=parent_fd,follow_symlinks=False)
                    os.chmod(part,directory_mode,dir_fd=parent_fd,follow_symlinks=False)
                    os.fsync(parent_fd)
                    append_directory({{"area":area,"relativePath":"/".join(prefix)}})
                except FileExistsError:
                    pass
                child=os.open(part,flags,dir_fd=parent_fd)
                require_app_traversal(child)
                opened.append(child); parent_fd=child
            name=relative.name
            expected=item["sha256"]
            try:
                existing=os.open(name,os.O_RDONLY|os.O_NOFOLLOW,dir_fd=parent_fd)
            except FileNotFoundError:
                existing=None
            if item["action"]=="SKIP_IDENTICAL":
                if existing is None or digest_fd(existing)!=expected:
                    raise SystemExit("identical destination changed before cutover")
                os.close(existing)
                continue
            if item["action"]!="CREATE":
                raise SystemExit("blocked collision reached publication")
            previous=events.get((area,item["relativePath"]))
            temporary=(previous or {{}}).get("temporaryName")
            if temporary is None:
                temporary=".massar-legacy-"+operation_id+"-"+uuid.uuid4().hex+".tmp"
                source=stage/"extracted"/area/item["relativePath"]
                if not source.is_file() or source.is_symlink():
                    raise SystemExit("staged source is missing or unsafe")
                source_stream=source.open("rb")
                temp_fd=os.open(
                    temporary,
                    os.O_WRONLY|os.O_CREAT|os.O_EXCL|os.O_NOFOLLOW,
                    0o600,
                    dir_fd=parent_fd,
                )
                try:
                    digest=hashlib.sha256()
                    while True:
                        chunk=source_stream.read(1024*1024)
                        if not chunk: break
                        digest.update(chunk); os.write(temp_fd,chunk)
                    os.fsync(temp_fd)
                    if digest.hexdigest()!=expected:
                        raise SystemExit("staged source checksum changed")
                    mode=0o644 if area=="public" else 0o640
                    os.fchown(temp_fd,operator_uid,massar_gid); os.fchmod(temp_fd,mode)
                finally:
                    os.close(temp_fd); source_stream.close()
                previous={{"status":"intent","area":area,
                    "relativePath":item["relativePath"],"sha256":expected,
                    "size":item["size"],"temporaryName":temporary}}
                append_event(previous); events[(area,item["relativePath"])]=previous
            try:
                temp_fd=os.open(temporary,os.O_RDONLY|os.O_NOFOLLOW,dir_fd=parent_fd)
            except FileNotFoundError:
                temp_fd=None
            if existing is not None:
                if temp_fd is None:
                    if not previous or previous.get("status")!="commit":
                        os.close(existing)
                        raise SystemExit("destination appeared after publication intent")
                    observed=os.fstat(existing)
                    if (
                        observed.st_dev!=previous.get("device")
                        or observed.st_ino!=previous.get("inode")
                    ):
                        os.close(existing)
                        raise SystemExit("committed publication inode was replaced")
                else:
                    left=os.fstat(existing); right=os.fstat(temp_fd)
                    if (left.st_dev,left.st_ino)!=(right.st_dev,right.st_ino):
                        os.close(existing); os.close(temp_fd)
                        raise SystemExit("destination collision during publication")
            else:
                if temp_fd is None:
                    raise SystemExit("publication intent lost its temporary inode")
                os.link(
                    temporary,name,src_dir_fd=parent_fd,dst_dir_fd=parent_fd,
                    follow_symlinks=False,
                )
                os.fsync(parent_fd)
                existing=os.open(name,os.O_RDONLY|os.O_NOFOLLOW,dir_fd=parent_fd)
            if digest_fd(existing)!=expected:
                raise SystemExit("published file checksum mismatch")
            file_stat=os.fstat(existing); expected_mode=0o644 if area=="public" else 0o640
            if (
                file_stat.st_uid!=0 or file_stat.st_gid!=massar_gid
                or stat.S_IMODE(file_stat.st_mode)!=expected_mode
                or not (file_stat.st_mode & stat.S_IRGRP)
            ):
                raise SystemExit("published file ownership/readability mismatch")
            committed={{"status":"commit","area":area,
                "relativePath":item["relativePath"],"sha256":expected,
                "size":item["size"],"temporaryName":temporary,
                "device":file_stat.st_dev,"inode":file_stat.st_ino}}
            if not previous or previous.get("status")!="commit":
                append_event(committed)
                events[(area,item["relativePath"])]=committed
            if temp_fd is not None: os.close(temp_fd)
            os.close(existing)
            try: os.unlink(temporary,dir_fd=parent_fd)
            except FileNotFoundError: pass
            os.fsync(parent_fd)
            created.append({{key:committed[key] for key in (
                "area","relativePath","sha256","size","device","inode"
            )}})
        finally:
            for fd in reversed(opened): os.close(fd)
finally:
    os.close(root_fd)
state=json.loads(state_path.read_text())
if state.get("operationId")!=operation_id or state.get("candidateDatabase")!={json.dumps(candidate)}:
    raise SystemExit("state changed during file publication")
state["createdFiles"]=created
state["createdDirectories"]=created_directories
state["filesPublished"]=True
state["phase"]="files-published"
temporary=state_path.with_suffix(".publish.tmp")
with temporary.open("w",encoding="utf-8") as stream:
    stream.write(json.dumps(state,indent=2)+"\\n"); stream.flush(); os.fsync(stream.fileno())
temporary.chmod(0o640); temporary.replace(state_path)
directory_fd=os.open(state_path.parent,os.O_RDONLY|os.O_DIRECTORY)
try: os.fsync(directory_fd)
finally: os.close(directory_fd)
print(json.dumps(state))
PY
""",
            timeout_seconds=1800,
        )
        try:
            result = json.loads(output.splitlines()[-1])
        except (json.JSONDecodeError, IndexError) as exc:
            raise LegacyCutoverError("file publication did not return valid state") from exc
        return result

    def acquire_cutover_lock(self, candidate: str, operation_id: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
lock={CUTOVER_LOCK}
mkdir "$lock"
printf '%s\n' {shlex.quote(candidate)} > "$lock/candidate"
printf '%s\n' {shlex.quote(operation_id)} > "$lock/operation-id"
sync "$lock/candidate" "$lock/operation-id"
""",
        )

    def assert_cutover_lock(self, candidate: str, operation_id: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
lock={CUTOVER_LOCK}
test "$(cat "$lock/candidate")" = {shlex.quote(candidate)}
test "$(cat "$lock/operation-id")" = {shlex.quote(operation_id)}
""",
        )

    def release_cutover_lock(self, candidate: str, operation_id: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
lock={CUTOVER_LOCK}
if ! test -e "$lock"; then exit 0; fi
test "$(cat "$lock/candidate")" = {shlex.quote(candidate)}
test "$(cat "$lock/operation-id")" = {shlex.quote(operation_id)}
rm "$lock/candidate" "$lock/operation-id"
rmdir "$lock"
""",
        )

    def drain_and_stop_apps(self, operation_id: str) -> None:
        drain_script = """
import socket
for backend in ("node-1","node-2","node-3"):
    sock=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM)
    sock.connect("/run/haproxy/admin.sock")
    sock.sendall(f"set server massar_nodes/{backend} state drain\\n".encode())
    sock.shutdown(socket.SHUT_WR)
    while sock.recv(4096): pass
    sock.close()
"""
        try:
            for node in self.inventory.nodes:
                self.remote(
                    node,
                    f"""
set -euo pipefail
python3 -c {shlex.quote(drain_script)}
marker={STATE_ROOT}/massar-legacy-cutover-{shlex.quote(operation_id)}.containers
test ! -e "$marker"
ids="$(
  sudo docker ps -q \
    --filter label=com.docker.compose.project=massar_production \
    --filter label=com.docker.compose.service=worker
  sudo docker ps -q \
    --filter label=com.docker.compose.project=massar_production \
    --filter label=com.docker.compose.service=backend
  sudo docker ps -q \
    --filter label=com.docker.compose.project=massar_production \
    --filter label=com.docker.compose.service=gateway
)"
test "$(printf '%s\n' "$ids" | sed '/^$/d' | wc -l | tr -d ' ')" -eq 3
printf '%s\n' "$ids" > "$marker"
chmod 0600 "$marker"
printf '%s\n' "$ids" | sed '/^$/d' | xargs sudo docker stop --time 45 >/dev/null
""",
                    timeout_seconds=180,
                )
        except Exception:
            self.recover_apps(operation_id)
            raise

    def recover_apps(self, operation_id: str) -> None:
        undrain_script = """
import socket
for backend in ("node-1","node-2","node-3"):
    sock=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM)
    sock.connect("/run/haproxy/admin.sock")
    sock.sendall(f"set server massar_nodes/{backend} state ready\\n".encode())
    sock.shutdown(socket.SHUT_WR)
    while sock.recv(4096): pass
    sock.close()
"""
        for node in self.inventory.nodes:
            self.remote(
                node,
                f"""
set -euo pipefail
marker={STATE_ROOT}/massar-legacy-cutover-{shlex.quote(operation_id)}.containers
if test -s "$marker" && test "$(grep -Ec '^[0-9a-f]{{64}}$' "$marker")" -eq 3; then
  xargs sudo docker start < "$marker" >/dev/null
  rm -f "$marker"
fi
backend_id="$(sudo docker ps -q --filter label=com.docker.compose.project=massar_production --filter label=com.docker.compose.service=backend)"
test -n "$backend_id"
for attempt in $(seq 1 90); do
  test "$(sudo docker inspect --format '{{{{.State.Health.Status}}}}' "$backend_id")" = healthy && break
  test "$attempt" -lt 90 || exit 62
  sleep 2
done
python3 -c {shlex.quote(undrain_script)}
""",
                timeout_seconds=240,
            )

    def start_and_undrain_apps(self, operation_id: str) -> None:
        undrain_script = """
import socket
for backend in ("node-1","node-2","node-3"):
    sock=socket.socket(socket.AF_UNIX,socket.SOCK_STREAM)
    sock.connect("/run/haproxy/admin.sock")
    sock.sendall(f"set server massar_nodes/{backend} state ready\\n".encode())
    sock.shutdown(socket.SHUT_WR)
    while sock.recv(4096): pass
    sock.close()
"""
        for node in self.inventory.nodes:
            self.remote(
                node,
                f"""
set -euo pipefail
marker={STATE_ROOT}/massar-legacy-cutover-{shlex.quote(operation_id)}.containers
if test -e "$marker"; then
  test -s "$marker"
  test "$(grep -Ec '^[0-9a-f]{{64}}$' "$marker")" -eq 3
  xargs sudo docker start < "$marker" >/dev/null
fi
running="$(
  sudo docker ps -q --filter label=com.docker.compose.project=massar_production --filter label=com.docker.compose.service=worker
  sudo docker ps -q --filter label=com.docker.compose.project=massar_production --filter label=com.docker.compose.service=backend
  sudo docker ps -q --filter label=com.docker.compose.project=massar_production --filter label=com.docker.compose.service=gateway
)"
test "$(printf '%s\n' "$running" | sed '/^$/d' | wc -l | tr -d ' ')" -eq 3
backend_id="$(sudo docker ps -q --filter label=com.docker.compose.project=massar_production --filter label=com.docker.compose.service=backend)"
for attempt in $(seq 1 90); do
  test "$(sudo docker inspect --format '{{{{.State.Health.Status}}}}' "$backend_id")" = healthy && break
  test "$attempt" -lt 90 || exit 61
  sleep 2
done
python3 -c {shlex.quote(undrain_script)}
rm -f "$marker"
""",
                timeout_seconds=240,
            )

    def database_quiesce(self, candidate: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
  -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 <<'SQL'
ALTER DATABASE "{LIVE_DATABASE}" WITH ALLOW_CONNECTIONS false;
ALTER DATABASE "{candidate}" WITH ALLOW_CONNECTIONS false;
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname IN ('{LIVE_DATABASE}','{candidate}') AND pid <> pg_backend_pid();
DO $guard$
BEGIN
  IF EXISTS (
    SELECT FROM pg_stat_activity
    WHERE datname IN ('{LIVE_DATABASE}','{candidate}')
  ) THEN
    RAISE EXCEPTION 'database sessions remain after quiesce';
  END IF;
END
$guard$;
SQL
""",
        )

    def atomic_swap(self, candidate: str, rollback: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
  -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 <<'SQL'
BEGIN;
DO $guard$
BEGIN
  IF NOT (
    (SELECT count(*) FROM pg_database WHERE datname='{LIVE_DATABASE}' AND NOT datallowconn)=1
    AND (SELECT count(*) FROM pg_database WHERE datname='{candidate}' AND NOT datallowconn)=1
    AND (SELECT count(*) FROM pg_database WHERE datname='{rollback}')=0
    AND (SELECT count(*) FROM pg_stat_activity WHERE datname IN ('{LIVE_DATABASE}','{candidate}'))=0
  ) THEN
    RAISE EXCEPTION 'database swap preconditions are not satisfied';
  END IF;
END
$guard$;
ALTER DATABASE "{LIVE_DATABASE}" RENAME TO "{rollback}";
ALTER DATABASE "{candidate}" RENAME TO "{LIVE_DATABASE}";
ALTER DATABASE "{LIVE_DATABASE}" WITH ALLOW_CONNECTIONS true;
COMMIT;
SQL
""",
        )

    def atomic_swap_back(self, candidate: str, rollback: str) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
  -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 <<'SQL'
ALTER DATABASE "{LIVE_DATABASE}" WITH ALLOW_CONNECTIONS false;
SELECT pg_terminate_backend(pid) FROM pg_stat_activity
 WHERE datname='{LIVE_DATABASE}' AND pid<>pg_backend_pid();
BEGIN;
DO $guard$
BEGIN
  IF NOT (
    (SELECT count(*) FROM pg_database WHERE datname='{LIVE_DATABASE}' AND NOT datallowconn)=1
    AND (SELECT count(*) FROM pg_database WHERE datname='{rollback}' AND NOT datallowconn)=1
    AND (SELECT count(*) FROM pg_stat_activity WHERE datname='{LIVE_DATABASE}')=0
  ) THEN
    RAISE EXCEPTION 'database swap-back preconditions are not satisfied';
  END IF;
END
$guard$;
ALTER DATABASE "{LIVE_DATABASE}" RENAME TO "{candidate}";
ALTER DATABASE "{rollback}" RENAME TO "{LIVE_DATABASE}";
ALTER DATABASE "{LIVE_DATABASE}" WITH ALLOW_CONNECTIONS true;
COMMIT;
SQL
""",
        )

    def database_name_state(self, candidate: str, rollback: str) -> str:
        names = self.remote(
            self.control,
            f"""
set -euo pipefail
sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
  -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 <<'SQL'
SELECT datname FROM pg_database
 WHERE datname IN ('{LIVE_DATABASE}','{candidate}','{rollback}')
 ORDER BY datname;
SQL
""",
        ).splitlines()
        observed = set(names)
        if observed == {LIVE_DATABASE, candidate}:
            return "prepared"
        if observed == {LIVE_DATABASE, rollback}:
            return "cutover"
        raise LegacyCutoverError("database names are in an ambiguous cutover state")

    def restore_live_connectivity(self) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
printf 'alter database "{LIVE_DATABASE}" with allow_connections true;\n' |
  sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
  -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 >/dev/null
""",
        )

    def close_live_connectivity(self) -> None:
        self.remote(
            self.control,
            f"""
set -euo pipefail
sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql "$@"' sh \
  -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 <<'SQL'
ALTER DATABASE "{LIVE_DATABASE}" WITH ALLOW_CONNECTIONS false;
SELECT pg_terminate_backend(pid) FROM pg_stat_activity
 WHERE datname='{LIVE_DATABASE}' AND pid<>pg_backend_pid();
DO $guard$
BEGIN
  IF EXISTS (
    SELECT FROM pg_stat_activity WHERE datname='{LIVE_DATABASE}'
  ) OR NOT EXISTS (
    SELECT FROM pg_database
    WHERE datname='{LIVE_DATABASE}' AND NOT datallowconn
  ) THEN
    RAISE EXCEPTION 'live database did not close cleanly';
  END IF;
END
$guard$;
SQL
""",
        )

    def post_swap_audit(self, expected_migration_count: int) -> None:
        output = self.remote(
            self.control,
            f"""
set -euo pipefail
sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgapp)"; exec psql "$@"' sh \
  -h 127.0.0.1 -p 6432 -U massar_app -d {LIVE_DATABASE} -XAt -v ON_ERROR_STOP=1 <<'SQL'
SELECT count(*) FROM "__EFMigrationsHistory";
SELECT count(*) FROM pg_index WHERE NOT indisvalid;
SELECT count(*) FROM pg_constraint
 WHERE connamespace='public'::regnamespace AND NOT convalidated;
SQL
""",
        ).splitlines()
        if output != [str(expected_migration_count), "0", "0"]:
            raise LegacyCutoverError("post-swap database audit did not match the candidate")

    def remove_created_files(self, state: dict[str, object]) -> None:
        encoded = json.dumps(state.get("createdFiles", []), separators=(",", ":"))
        directories = json.dumps(
            state.get("createdDirectories", []),
            separators=(",", ":"),
        )
        self.remote(
            self.control,
            f"""
set -euo pipefail
python3 - {shlex.quote(SHARED_ROOT)} {shlex.quote(encoded)} \
  {shlex.quote(directories)} <<'PY'
import hashlib,json,os,pathlib,sys
root=sys.argv[1]; rows=json.loads(sys.argv[2]); directories=json.loads(sys.argv[3])
flags=os.O_RDONLY|os.O_DIRECTORY|os.O_NOFOLLOW
root_fd=os.open(root,flags)
def parent_fd(area,relative):
    current=os.open(area,flags,dir_fd=root_fd); opened=[current]
    parts=pathlib.PurePosixPath(relative).parts
    for part in parts[:-1]:
        current=os.open(part,flags,dir_fd=current); opened.append(current)
    return current,opened,parts[-1]
def digest_fd(fd):
    digest=hashlib.sha256()
    while True:
        chunk=os.read(fd,1024*1024)
        if not chunk: break
        digest.update(chunk)
    return digest.hexdigest()
for item in reversed(rows):
    parent,opened,name=parent_fd(item["area"],item["relativePath"])
    try:
        fd=os.open(name,os.O_RDONLY|os.O_NOFOLLOW,dir_fd=parent)
        try:
            observed=os.fstat(fd)
            if (
                digest_fd(fd)!=item["sha256"]
                or observed.st_dev!=item["device"] or observed.st_ino!=item["inode"]
            ):
                raise SystemExit("created file changed after import; rollback refused")
        finally: os.close(fd)
    finally:
        for value in reversed(opened): os.close(value)
for item in reversed(rows):
    parent,opened,name=parent_fd(item["area"],item["relativePath"])
    try: os.unlink(name,dir_fd=parent); os.fsync(parent)
    finally:
        for value in reversed(opened): os.close(value)
for item in reversed(directories):
    parent,opened,name=parent_fd(item["area"],item["relativePath"])
    try:
        try: os.rmdir(name,dir_fd=parent); os.fsync(parent)
        except OSError as exc:
            if exc.errno not in (39,2): raise
    finally:
        for value in reversed(opened): os.close(value)
os.close(root_fd)
PY
""",
        )

    def cleanup_staging(self, state: dict[str, object]) -> None:
        operation_id = validated_operation_id(state["operationId"])
        stage = str(state.get("stagingRoot") or "")
        expected = f"{IMPORT_ROOT}/{operation_id}"
        if stage != expected:
            raise LegacyCutoverError("refusing to remove an unexpected staging path")
        self.remote(
            self.control,
            f"rm -rf -- {shlex.quote(expected)}",
        )

    def execute_prepare(
        self,
        candidate: str,
        bundle: CandidateBundle,
        passphrase: Path,
    ) -> CutoverOutcome:
        operation_id = str(uuid.uuid4())
        claim_id = str(uuid.uuid4())
        started = self.now()
        error: Exception | None = None
        state: dict[str, object] | None = None
        cleanup_verified = True
        mutex_acquired = False
        cleanup_needed = False
        try:
            self.ensure_operator_roots()
            self.acquire_execution_mutex(candidate, "prepare", claim_id)
            mutex_acquired = True
            self.inspect_cluster()
            cleanup_needed = True
            uploaded = self.upload_bundle(bundle, passphrase, operation_id)
            state = self.prepare_remote(candidate, bundle, uploaded, operation_id)
        except Exception as exc:  # Candidate/file cleanup is mandatory for every operational failure.
            error = exc
            if cleanup_needed:
                try:
                    self.cleanup_prepare(candidate, operation_id)
                except Exception as recovery:
                    cleanup_verified = False
                    error = LegacyCutoverError(f"{exc}; prepare cleanup failed: {recovery}")
        finally:
            if mutex_acquired:
                try:
                    self.release_execution_mutex(claim_id)
                except Exception as recovery:
                    cleanup_verified = False
                    error = LegacyCutoverError(
                        f"{error or 'prepare completed'}; execution mutex release failed: {recovery}"
                    )
        if not cleanup_verified:
            try:
                self.mark_recovery_required(
                    candidate,
                    operation_id,
                    "prepare",
                    str(error),
                )
            except Exception as marker_error:
                error = LegacyCutoverError(
                    f"{error}; durable recovery marker failed: {marker_error}"
                )
        evidence = {
            "schemaVersion": 1,
            "operationId": operation_id,
            "action": "prepare",
            "candidateDatabase": candidate,
            "backupId": bundle.backup_id,
            "startedAt": iso8601(started),
            "completedAt": iso8601(self.now()),
            "result": (
                "prepared"
                if error is None
                else "safe-refusal"
                if cleanup_verified
                else "recovery-required"
            ),
            "writesOpened": False,
            "cleanupVerified": cleanup_verified,
            "state": state,
            "reason": None if error is None else str(error)[:500],
        }
        return CutoverOutcome(evidence, error)

    def execute_cutover(
        self,
        candidate: str,
        backup_gate: dict[str, object],
    ) -> CutoverOutcome:
        started = self.now()
        claim_id = str(uuid.uuid4())
        operation_id = claim_id
        state: dict[str, object] = {}
        rollback = validated_database_name(
            candidate.replace("_candidate_", "_rollback_"),
            "rollback",
        )
        swapped = False
        stopped = False
        cutover_lock_acquired = False
        mutex_acquired = False
        publication_started = False
        recovery_verified = True
        error: Exception | None = None
        try:
            self.ensure_operator_roots()
            self.acquire_execution_mutex(candidate, "cutover", claim_id)
            mutex_acquired = True
            self.inspect_cluster()
            state = self.read_state(candidate)
            operation_id = str(state["operationId"])
            if state.get("phase") != "prepared" or state.get("writesOpened") is not False:
                raise LegacyCutoverError(
                    "cutover requires a prepared, never-opened candidate"
                )
            try:
                self.acquire_cutover_lock(candidate, operation_id)
                cutover_lock_acquired = True
                self.drain_and_stop_apps(operation_id)
                stopped = True
                self.database_quiesce(candidate)
                publication_started = True
                state = self.publish_staged_files(candidate, state)
                self.atomic_swap(candidate, rollback)
                swapped = True
                expected_migrations = state.get("expectedMigrationCount")
                if not isinstance(expected_migrations, int) or expected_migrations <= 0:
                    raise LegacyCutoverError("prepared state lacks the exact migration count")
                self.post_swap_audit(expected_migrations)
                self.close_live_connectivity()
                state.update({
                    "phase": "cutover-pending",
                    "cutoverAt": iso8601(self.now()),
                    "rollbackDatabase": rollback,
                    "writesOpened": False,
                    "backupGate": backup_gate,
                })
                self.write_state(candidate, state)
            except Exception as exc:
                error = exc
                try:
                    name_state = self.database_name_state(candidate, rollback)
                except Exception as recovery:
                    recovery_verified = False
                    name_state = "ambiguous"
                    error = LegacyCutoverError(f"{exc}; cutover-state detection failed: {recovery}")
                if name_state == "cutover":
                    try:
                        self.atomic_swap_back(candidate, rollback)
                    except Exception as recovery:
                        recovery_verified = False
                        error = LegacyCutoverError(f"{exc}; automatic swap-back failed: {recovery}")
                elif name_state == "prepared":
                    try:
                        self.restore_live_connectivity()
                    except Exception as recovery:
                        recovery_verified = False
                        error = LegacyCutoverError(f"{exc}; database recovery failed: {recovery}")
                if publication_started:
                    try:
                        state = self.read_state(candidate)
                        if state.get("filesPublished") is not True:
                            state = self.publish_staged_files(candidate, state)
                        self.remove_created_files(state)
                        state.update({
                            "phase": "prepared",
                            "filesPublished": False,
                            "createdFiles": [],
                            "createdDirectories": [],
                        })
                        self.write_state(candidate, state)
                    except Exception as recovery:
                        recovery_verified = False
                        error = LegacyCutoverError(
                            f"{error}; published-file recovery failed: {recovery}"
                        )
                if stopped:
                    try:
                        self.recover_apps(operation_id)
                    except Exception as recovery:
                        recovery_verified = False
                        error = LegacyCutoverError(f"{error}; application recovery failed: {recovery}")
                if cutover_lock_acquired:
                    try:
                        self.release_cutover_lock(candidate, operation_id)
                    except Exception as recovery:
                        recovery_verified = False
                        error = LegacyCutoverError(
                            f"{error}; cutover lock release failed: {recovery}"
                        )
        except Exception as exc:
            error = error or exc
        finally:
            if mutex_acquired:
                try:
                    self.release_execution_mutex(claim_id)
                except Exception as recovery:
                    recovery_verified = False
                    error = LegacyCutoverError(
                        f"{error or 'cutover completed'}; execution mutex release failed: {recovery}"
                    )
        if error is not None and not recovery_verified:
            try:
                self.mark_recovery_required(
                    candidate,
                    operation_id,
                    str(state.get("phase") or "cutover"),
                    str(error),
                )
            except Exception as marker_error:
                error = LegacyCutoverError(
                    f"{error}; durable recovery marker failed: {marker_error}"
                )
        evidence = {
            "schemaVersion": 1,
            "operationId": operation_id,
            "action": "cutover",
            "candidateDatabase": candidate,
            "rollbackDatabase": rollback,
            "startedAt": iso8601(started),
            "completedAt": iso8601(self.now()),
            "result": (
                "cutover-pending"
                if error is None
                else "safe-refusal"
                if recovery_verified
                else "recovery-required"
            ),
            "atomicRenameCommitted": swapped and error is None,
            "writesOpened": False,
            "databaseConnectionsAllowed": False if error is None else None,
            "applicationsStopped": error is None,
            "recoveryVerified": recovery_verified,
            "reason": None if error is None else str(error)[:500],
        }
        return CutoverOutcome(evidence, error)

    def execute_resume(self, candidate: str) -> CutoverOutcome:
        started = self.now()
        claim_id = str(uuid.uuid4())
        operation_id = claim_id
        state: dict[str, object] = {}
        mutex_acquired = False
        error: Exception | None = None
        try:
            self.ensure_operator_roots()
            self.acquire_execution_mutex(candidate, "resume", claim_id)
            mutex_acquired = True
            self.inspect_cluster(allow_recovery=True)
            state = self.read_state(candidate)
            operation_id = str(state["operationId"])
            self.assert_recovery_marker_matches(candidate, operation_id)
            phase = state.get("phase")
            if phase not in {"cutover-pending", "opening-writes", "complete"}:
                raise LegacyCutoverError(
                    "resume requires cutover-pending or an idempotent forward-recovery phase"
                )
            if phase != "complete":
                self.assert_cutover_lock(candidate, operation_id)
                expected_migrations = state.get("expectedMigrationCount")
                if not isinstance(expected_migrations, int) or expected_migrations <= 0:
                    raise LegacyCutoverError("prepared state lacks the exact migration count")
                if phase == "cutover-pending":
                    state["phase"] = "opening-writes"
                    state["writesOpened"] = True
                    state["writesOpenedAt"] = iso8601(self.now())
                    self.write_state(candidate, state)
                self.restore_live_connectivity()
                self.post_swap_audit(expected_migrations)
                self.start_and_undrain_apps(operation_id)
                state["phase"] = "complete"
                state["writesOpened"] = True
                self.write_state(candidate, state)
            self.release_cutover_lock(candidate, operation_id)
            self.cleanup_staging(state)
            self.clear_recovery_marker(candidate, operation_id)
        except Exception as exc:
            error = exc
        finally:
            if mutex_acquired:
                try:
                    self.release_execution_mutex(claim_id)
                except Exception as recovery:
                    error = LegacyCutoverError(
                        f"{error or 'resume completed'}; execution mutex release failed: {recovery}"
                    )
        if error is not None:
            try:
                self.mark_recovery_required(
                    candidate,
                    operation_id,
                    str(state.get("phase") or "resume"),
                    str(error),
                )
            except Exception as marker_error:
                error = LegacyCutoverError(
                    f"{error}; durable recovery marker failed: {marker_error}"
                )
        return CutoverOutcome({
            "schemaVersion": 1,
            "operationId": operation_id,
            "action": "resume",
            "candidateDatabase": candidate,
            "startedAt": iso8601(started),
            "completedAt": iso8601(self.now()),
            "result": "complete" if error is None else "recovery-required",
            "writesOpened": state.get("writesOpened", False),
            "reason": None if error is None else str(error)[:500],
        }, error)

    def execute_rollback(self, candidate: str) -> CutoverOutcome:
        started = self.now()
        claim_id = str(uuid.uuid4())
        operation_id = claim_id
        state: dict[str, object] = {}
        rollback = ""
        mutex_acquired = False
        recovery_required = False
        error: Exception | None = None
        try:
            self.ensure_operator_roots()
            self.acquire_execution_mutex(candidate, "rollback", claim_id)
            mutex_acquired = True
            self.inspect_cluster(allow_recovery=True)
            state = self.read_state(candidate)
            operation_id = str(state["operationId"])
            rollback = str(state.get("rollbackDatabase") or "")
            self.assert_recovery_marker_matches(candidate, operation_id)
            phase = state.get("phase")
            allowed = {
                "cutover-pending",
                "rollback-database-restored",
                "rollback-opening-writes",
                "rolled-back",
            }
            if phase not in allowed:
                raise LegacyCutoverError(
                    "rollback is permanently refused after candidate writes are opened"
                )
            recovery_required = True
            if phase != "rolled-back":
                self.assert_cutover_lock(candidate, operation_id)
                validated_database_name(rollback, "rollback")
                if phase == "cutover-pending":
                    try:
                        self.atomic_swap_back(candidate, rollback)
                    except Exception:
                        if self.database_name_state(candidate, rollback) != "prepared":
                            raise
                    state["phase"] = "rollback-database-restored"
                    self.write_state(candidate, state)
                    phase = "rollback-database-restored"
                if phase == "rollback-database-restored":
                    if state.get("filesPublished") is True:
                        self.remove_created_files(state)
                    state.update({
                        "phase": "rollback-opening-writes",
                        "filesPublished": False,
                        "createdFiles": [],
                        "createdDirectories": [],
                        "writesOpened": True,
                    })
                    self.write_state(candidate, state)
                self.start_and_undrain_apps(operation_id)
                state.update({
                    "phase": "rolled-back",
                    "rolledBackAt": iso8601(self.now()),
                    "writesOpened": True,
                })
                self.write_state(candidate, state)
            self.release_cutover_lock(candidate, operation_id)
            self.cleanup_staging(state)
            self.clear_recovery_marker(candidate, operation_id)
            recovery_required = False
        except Exception as exc:
            error = exc
        finally:
            if mutex_acquired:
                try:
                    self.release_execution_mutex(claim_id)
                except Exception as recovery:
                    recovery_required = True
                    error = LegacyCutoverError(
                        f"{error or 'rollback completed'}; execution mutex release failed: {recovery}"
                    )
        if error is not None and recovery_required:
            try:
                self.mark_recovery_required(
                    candidate,
                    operation_id,
                    str(state.get("phase") or "rollback"),
                    str(error),
                )
            except Exception as marker_error:
                error = LegacyCutoverError(
                    f"{error}; durable recovery marker failed: {marker_error}"
                )
        return CutoverOutcome({
            "schemaVersion": 1,
            "operationId": operation_id,
            "action": "rollback",
            "candidateDatabase": candidate,
            "rollbackDatabase": rollback or None,
            "startedAt": iso8601(started),
            "completedAt": iso8601(self.now()),
            "result": (
                "rolled-back"
                if error is None
                else "recovery-required"
                if recovery_required
                else "safe-refusal"
            ),
            "writesOpened": state.get("writesOpened", False),
            "reason": None if error is None else str(error)[:500],
        }, error)


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser(description=__doc__)
    value.add_argument("--inventory", required=True, type=Path)
    value.add_argument("--candidate-db", required=True)
    value.add_argument("--evidence-output", required=True, type=Path)
    value.add_argument("--known-hosts", type=Path)
    value.add_argument("--identity", type=Path)
    value.add_argument("--bundle-manifest", type=Path)
    value.add_argument("--passphrase-file", type=Path)
    value.add_argument("--backup-gate", type=Path)
    value.add_argument("action", choices=("prepare", "cutover", "resume", "rollback"))
    mode = value.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return value


def dry_run_evidence(
    action: str,
    candidate: str,
    bundle: CandidateBundle | None,
) -> dict[str, object]:
    now = utc_now()
    return {
        "schemaVersion": 1,
        "operationId": str(uuid.uuid4()),
        "action": action,
        "candidateDatabase": candidate,
        "backupId": bundle.backup_id if bundle else None,
        "startedAt": iso8601(now),
        "completedAt": iso8601(now),
        "result": "dry-run",
        "sshAttempted": False,
        "writesOpened": False,
        "plannedGuards": [
            "exact-three-node-inventory",
            "one-patroni-primary",
            "gluster-zero-heal-and-split-brain",
            "exact-migration-history-and-table-counts",
            "no-overwrite-file-collision-manifest",
            "cluster-wide-execution-mutex",
            "non-routable-file-staging",
            "write-ahead-symlink-safe-file-publication",
            "bound-fresh-backup-gate",
            "three-node-backend-worker-stop",
            "zero-database-sessions",
            "transactional-double-rename",
            "rollback-before-writes-only",
        ],
    }


def main() -> int:
    args = parser().parse_args()
    try:
        inventory = load_cutover_inventory(args.inventory)
        candidate = validated_database_name(args.candidate_db, "candidate")
        bundle = None
        if args.action == "prepare":
            if not args.bundle_manifest or not args.passphrase_file:
                raise LegacyCutoverError("prepare requires --bundle-manifest and --passphrase-file")
            bundle = load_bundle(
                args.bundle_manifest,
                verify_artifacts=not args.dry_run,
                require_cutover_eligible=not args.dry_run,
            )
        if args.dry_run:
            write_evidence(args.evidence_output, dry_run_evidence(args.action, candidate, bundle))
            print(json.dumps({"status": "dry-run", "action": args.action}))
            return 0
        if not args.known_hosts or not args.identity:
            raise LegacyCutoverError("real actions require --known-hosts and --identity")
        transport = StrictSshTransport(args.known_hosts, args.identity)
        runner = LegacyCutover(inventory, transport)
        if args.action == "prepare":
            assert bundle is not None and args.passphrase_file is not None
            outcome = runner.execute_prepare(candidate, bundle, args.passphrase_file)
        elif args.action == "cutover":
            if not args.backup_gate:
                raise LegacyCutoverError("cutover requires --backup-gate")
            state = runner.read_state(candidate)
            prepared_at = dt.datetime.fromisoformat(
                str(state["preparedAt"]).replace("Z", "+00:00")
            )
            gate = validate_backup_gate(
                args.backup_gate,
                prepared_at=prepared_at,
                now=utc_now(),
                cluster_name=str(inventory.cluster["name"]),
                inventory_sha256=sha256_file(inventory.path),
                release_id=str(state["releaseId"]),
                candidate_database=candidate,
                candidate_prepared_at=str(state["preparedAt"]),
                candidate_manifest_sha256=str(state["candidateManifestSha256"]),
                operation_id=str(state["operationId"]),
                candidate_backup_id=str(state["backupId"]),
                candidate_dump_sha256=str(state["candidateDumpSha256"]),
                file_archive_sha256=str(state["fileArchiveSha256"]),
            )
            outcome = runner.execute_cutover(candidate, gate)
        elif args.action == "resume":
            outcome = runner.execute_resume(candidate)
        else:
            outcome = runner.execute_rollback(candidate)
        write_evidence(args.evidence_output, outcome.evidence)
        print(json.dumps({
            "status": outcome.evidence["result"],
            "action": args.action,
            "evidence": str(args.evidence_output),
        }))
        return 0 if outcome.error is None else 6
    except (LegacyCutoverError, OSError, ValueError, json.JSONDecodeError) as exc:
        payload = {
            "schemaVersion": 1,
            "operationId": str(uuid.uuid4()),
            "action": args.action,
            "candidateDatabase": args.candidate_db,
            "startedAt": iso8601(utc_now()),
            "completedAt": iso8601(utc_now()),
            "result": "safe-refusal",
            "writesOpened": False,
            "reason": str(exc)[:500],
        }
        write_evidence(args.evidence_output, payload)
        print(f"legacy cutover blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
