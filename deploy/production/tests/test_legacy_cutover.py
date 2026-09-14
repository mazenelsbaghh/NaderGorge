from __future__ import annotations

import datetime as dt
import hashlib
import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "manage_legacy_cutover",
    SCRIPTS / "manage_legacy_cutover.py",
)
assert SPEC and SPEC.loader
cutover = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = cutover
SPEC.loader.exec_module(cutover)


def candidate() -> str:
    return "massar_platform_candidate_20260727T120000Z"


def bundle_manifest(
    tmp_path: Path,
    *,
    create_artifacts: bool = True,
    authoritative: bool = True,
) -> Path:
    dump = tmp_path / "candidate.dump.gpg"
    files = tmp_path / "files.tar.gpg"
    if create_artifacts:
        dump.write_bytes(b"database")
        files.write_bytes(b"files")
    payload = {
        "schemaVersion": 2,
        "status": "success",
        "backupId": "legacy-20260727T120000Z",
        "candidateMode": "authoritative-final" if authoritative else "rehearsal",
        "eligibleForCutover": authoritative,
        "sourceCapture": {
            "backupId": "legacy-20260727T010203Z-deadbeef",
            "sourceHost": "192.0.2.10",
            "sourceUser": "root",
            "sourceMode": "frozen-writers-held" if authoritative else "frozen-writers",
            "authoritativeSource": authoritative,
            "writersFrozenAtCompletion": authoritative,
            "manifestSha256": "1" * 64,
            "captureEvidenceSha256": "2" * 64,
            "artifactSha256": {
                "database": "3" * 64,
                "assets": "4" * 64,
                "protected": "5" * 64,
                "appData": "6" * 64,
            },
        },
        "sourceBackupId": "legacy-20260727T010203Z-deadbeef",
        "restoreId": "legacy-restore-" + "7" * 32,
        "restoreEvidenceSha256": "8" * 64,
        "validationEvidenceSha256": "9" * 64,
        "candidateDump": {
            "path": str(dump),
            "sha256": hashlib.sha256(b"database").hexdigest(),
        },
        "fileArchive": {
            "path": str(files),
            "sha256": hashlib.sha256(b"files").hexdigest(),
        },
        "migrationIds": ["20260726174622_AddClusterLeases", "20260726182136_EnsureSystemRoles"],
        "tableCounts": {
            "__EFMigrationsHistory": 2,
            "ParentDeviceTokens": 3,
            "users": 63,
        },
        "files": [
            {
                "archivePath": "public/subtitles/lesson.srt",
                "area": "public",
                "relativePath": "subtitles/lesson.srt",
                "size": 7,
                "sha256": hashlib.sha256(b"caption").hexdigest(),
            }
        ],
    }
    path = tmp_path / "bundle.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def inventory():
    return cutover.load_cutover_inventory(
        ROOT / "deploy/production/inventory/production.yml"
    )


def test_bundle_requires_exact_artifact_hashes_and_safe_paths(tmp_path: Path) -> None:
    path = bundle_manifest(tmp_path)
    bundle = cutover.load_bundle(path)
    assert bundle.backup_id == "legacy-20260727T120000Z"
    assert bundle.files[0].relative_path == "subtitles/lesson.srt"

    value = json.loads(path.read_text())
    value["files"][0]["relativePath"] = "../outside"
    value["files"][0]["archivePath"] = "public/../outside"
    path.write_text(json.dumps(value))
    with pytest.raises(cutover.LegacyCutoverError, match="unsafe file path"):
        cutover.load_bundle(path)


@pytest.mark.parametrize(
    ("mutation", "error"),
    [
        ({"schemaVersion": 1}, "schemaVersion 2"),
        ({"eligibleForCutover": False}, "coherent source capture"),
        ({"sourceBackupId": "legacy-other-source"}, "coherent source capture"),
        ({"restoreEvidenceSha256": "not-a-digest"}, "coherent source capture"),
    ],
)
def test_bundle_rejects_provenance_mismatch(
    tmp_path: Path,
    mutation: dict[str, object],
    error: str,
) -> None:
    path = bundle_manifest(tmp_path)
    payload = json.loads(path.read_text(encoding="utf-8"))
    payload.update(mutation)
    path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(cutover.LegacyCutoverError, match=error):
        cutover.load_bundle(path)


def test_rehearsal_bundle_is_inspectable_but_never_cutover_eligible(
    tmp_path: Path,
) -> None:
    path = bundle_manifest(tmp_path, authoritative=False)

    rehearsal = cutover.load_bundle(path, require_cutover_eligible=False)

    assert rehearsal.candidate_mode == "rehearsal"
    assert rehearsal.eligible_for_cutover is False
    with pytest.raises(cutover.LegacyCutoverError, match="not eligible"):
        cutover.load_bundle(path)


def test_exact_migration_and_table_count_guards_refuse_drift() -> None:
    migrations = ("001_first", "002_second")
    counts = {"__EFMigrationsHistory": 2, "users": 63}
    cutover.validate_database_snapshot(migrations, counts, migrations, counts)
    with pytest.raises(cutover.LegacyCutoverError, match="migration history"):
        cutover.validate_database_snapshot(
            migrations,
            counts,
            ("002_second",),
            counts,
        )
    with pytest.raises(cutover.LegacyCutoverError, match="table names/counts"):
        cutover.validate_database_snapshot(
            migrations,
            counts,
            migrations,
            {**counts, "users": 62},
        )


def test_collision_plan_never_silently_overwrites() -> None:
    digest = hashlib.sha256(b"value").hexdigest()
    assert cutover.classify_collision(False, digest, None) == "CREATE"
    assert cutover.classify_collision(True, digest, digest) == "SKIP_IDENTICAL"
    assert cutover.classify_collision(True, digest, "0" * 64) == "BLOCK_COLLISION"


def test_backup_gate_requires_fresh_named_restores(tmp_path: Path) -> None:
    prepared = dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc)
    operation_id = "00000000-0000-4000-8000-000000000166"
    gate = {
        "schemaVersion": 2,
        "status": "success",
        "clusterId": "massar-production",
        "inventorySha256": "1" * 64,
        "releaseId": "git-abcdef1234567",
        "candidateDatabase": candidate(),
        "candidatePreparedAt": "2026-07-27T12:00:00Z",
        "candidateManifestSha256": "2" * 64,
        "candidateBackupId": "legacy-20260727T120000Z",
        "candidateDumpSha256": "3" * 64,
        "fileArchiveSha256": "4" * 64,
        "operationId": operation_id,
        "databaseBackupId": "20260727-120000F",
        "fileSnapshotId": "5" * 64,
        "databaseRestoreVerified": True,
        "fileRestoreVerified": True,
        "databaseBackupEvidenceSha256": "6" * 64,
        "databaseRestoreEvidenceSha256": "7" * 64,
        "fileBackupEvidenceSha256": "8" * 64,
        "fileRestoreEvidenceSha256": "9" * 64,
        "capturedAt": "2026-07-27T12:01:00Z",
    }
    path = tmp_path / "gate.json"
    path.write_text(json.dumps(gate))
    arguments = {
        "prepared_at": prepared,
        "now": dt.datetime(2026, 7, 27, 12, 2, tzinfo=dt.timezone.utc),
        "cluster_name": "massar-production",
        "inventory_sha256": "1" * 64,
        "release_id": "git-abcdef1234567",
        "candidate_database": candidate(),
        "candidate_prepared_at": "2026-07-27T12:00:00Z",
        "candidate_manifest_sha256": "2" * 64,
        "operation_id": operation_id,
        "candidate_backup_id": "legacy-20260727T120000Z",
        "candidate_dump_sha256": "3" * 64,
        "file_archive_sha256": "4" * 64,
    }
    assert cutover.validate_backup_gate(path, **arguments)["status"] == "success"
    gate["capturedAt"] = "2026-07-27T11:59:59Z"
    path.write_text(json.dumps(gate))
    with pytest.raises(cutover.LegacyCutoverError, match="recent"):
        cutover.validate_backup_gate(path, **arguments)
    gate["capturedAt"] = "2026-07-27T12:01:00Z"
    gate["operationId"] = "00000000-0000-4000-8000-000000000999"
    path.write_text(json.dumps(gate))
    with pytest.raises(cutover.LegacyCutoverError, match="bound"):
        cutover.validate_backup_gate(path, **arguments)


def test_dry_run_attempts_no_ssh_and_writes_evidence(tmp_path: Path) -> None:
    evidence = tmp_path / "dry-run.json"
    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "manage_legacy_cutover.py"),
            "--inventory",
            str(ROOT / "deploy/production/inventory/production.yml"),
            "--candidate-db",
            candidate(),
            "--evidence-output",
            str(evidence),
            "--bundle-manifest",
            str(bundle_manifest(tmp_path, create_artifacts=False)),
            "--passphrase-file",
            str(tmp_path / "not-needed-in-dry-run"),
            "prepare",
            "--dry-run",
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert completed.returncode == 0, completed.stderr
    payload = json.loads(evidence.read_text())
    assert payload["result"] == "dry-run"
    assert payload["sshAttempted"] is False
    assert "transactional-double-rename" in payload["plannedGuards"]
    assert evidence.stat().st_mode & 0o027 == 0


class FakeLegacyCutover(cutover.LegacyCutover):
    def __init__(self, *, fail_at: str | None = None, state: dict | None = None) -> None:
        self.inventory = inventory()
        self.control = self.inventory.nodes[0]
        self.transport = None
        self.now = lambda: dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc)
        self.fail_at = fail_at
        self.events: list[str] = []
        self.database_phase = "prepared"
        self.state = state or {
            "candidateDatabase": candidate(),
            "operationId": "00000000-0000-4000-8000-000000000166",
            "phase": "prepared",
            "preparedAt": "2026-07-27T11:00:00Z",
            "writesOpened": False,
            "rollbackDatabase": None,
            "createdFiles": [],
            "createdDirectories": [],
            "filesPublished": False,
            "stagingRoot": "/var/lib/massar/legacy-import/00000000-0000-4000-8000-000000000166",
            "expectedMigrationCount": 129,
        }

    def inspect_cluster(self, *, allow_recovery=False):
        self.events.append("inspect")

    def ensure_operator_roots(self):
        self.events.append("ensure-operator-roots")

    def acquire_execution_mutex(self, candidate_name, action, claim_id):
        self.events.append(f"acquire-execution:{action}")

    def release_execution_mutex(self, claim_id):
        self.events.append("release-execution")

    def mark_recovery_required(self, candidate_name, operation_id, phase, reason):
        self.events.append(f"mark-recovery:{phase}")

    def assert_recovery_marker_matches(self, candidate_name, operation_id):
        self.events.append("assert-recovery-marker")

    def clear_recovery_marker(self, candidate_name, operation_id):
        self.events.append("clear-recovery-marker")

    def upload_bundle(self, bundle, passphrase_file, operation_id):
        self.events.append("upload")
        return {}

    def prepare_remote(self, candidate_name, bundle, uploaded, operation_id):
        self.events.append("prepare")
        if self.fail_at == "prepare":
            raise cutover.LegacyCutoverError("injected prepare failure")
        return self.state

    def publish_staged_files(self, candidate_name, state):
        self.events.append("publish-files")
        value = dict(state)
        value["phase"] = "files-published"
        value["filesPublished"] = True
        self.state = value
        return dict(value)

    def cleanup_prepare(self, candidate_name, operation_id):
        self.events.append("cleanup-prepare")

    def read_state(self, candidate_name):
        self.events.append("read-state")
        return dict(self.state)

    def write_state(self, candidate_name, state):
        self.events.append(f"write-state:{state['phase']}")
        self.state = dict(state)

    def acquire_cutover_lock(self, candidate_name, operation_id):
        self.events.append("acquire-lock")

    def assert_cutover_lock(self, candidate_name, operation_id):
        self.events.append("assert-lock")

    def release_cutover_lock(self, candidate_name, operation_id):
        self.events.append("release-lock")

    def drain_and_stop_apps(self, operation_id):
        self.events.append("stop-apps")

    def recover_apps(self, operation_id):
        self.events.append("recover-apps")

    def start_and_undrain_apps(self, operation_id):
        self.events.append("start-apps")
        if self.fail_at == "start":
            raise cutover.LegacyCutoverError("injected start failure")

    def database_quiesce(self, candidate_name):
        self.events.append("zero-sessions")

    def atomic_swap(self, candidate_name, rollback):
        self.events.append("atomic-swap")
        self.database_phase = "cutover"
        if self.fail_at == "swap-response":
            raise cutover.LegacyCutoverError("injected lost SSH response after commit")

    def atomic_swap_back(self, candidate_name, rollback):
        self.events.append("atomic-swap-back")
        self.database_phase = "prepared"

    def database_name_state(self, candidate_name, rollback):
        self.events.append("database-name-state")
        return self.database_phase

    def restore_live_connectivity(self):
        self.events.append("restore-connectivity")

    def close_live_connectivity(self):
        self.events.append("close-connectivity")

    def post_swap_audit(self, expected_migration_count):
        self.events.append(f"post-audit:{expected_migration_count}")
        if self.fail_at == "post-audit":
            raise cutover.LegacyCutoverError("injected post-audit failure")

    def remove_created_files(self, state):
        self.events.append("remove-created-files")

    def cleanup_staging(self, state):
        self.events.append("cleanup-staging")


def test_prepare_failure_always_runs_candidate_and_file_cleanup(tmp_path: Path) -> None:
    runner = FakeLegacyCutover(fail_at="prepare")
    bundle = cutover.load_bundle(bundle_manifest(tmp_path))
    outcome = runner.execute_prepare(candidate(), bundle, tmp_path / "passphrase")
    assert outcome.error is not None
    assert "cleanup-prepare" in runner.events
    assert runner.events.index("cleanup-prepare") < runner.events.index("release-execution")
    assert outcome.evidence["result"] == "safe-refusal"


def test_failure_after_atomic_swap_rolls_back_before_apps_recover() -> None:
    runner = FakeLegacyCutover(fail_at="post-audit")
    outcome = runner.execute_cutover(candidate(), {"status": "success"})
    assert outcome.error is not None
    assert runner.events.index("atomic-swap") < runner.events.index("atomic-swap-back")
    assert runner.events.index("atomic-swap-back") < runner.events.index("recover-apps")
    assert "write-state:cutover-pending" not in runner.events
    assert outcome.evidence["writesOpened"] is False


def test_lost_ssh_response_after_commit_detects_names_and_swaps_back() -> None:
    runner = FakeLegacyCutover(fail_at="swap-response")
    outcome = runner.execute_cutover(candidate(), {"status": "success"})
    assert outcome.error is not None
    assert runner.events.index("atomic-swap") < runner.events.index("database-name-state")
    assert runner.events.index("database-name-state") < runner.events.index("atomic-swap-back")
    assert runner.database_phase == "prepared"
    assert outcome.evidence["recoveryVerified"] is True


def test_successful_cutover_holds_writes_until_explicit_resume() -> None:
    runner = FakeLegacyCutover()
    outcome = runner.execute_cutover(candidate(), {"status": "success"})
    assert outcome.error is None
    assert outcome.evidence["result"] == "cutover-pending"
    assert "start-apps" not in runner.events
    assert "release-lock" not in runner.events
    assert runner.state["writesOpened"] is False
    assert outcome.evidence["databaseConnectionsAllowed"] is False
    assert runner.events.index("post-audit:129") < runner.events.index(
        "close-connectivity"
    )
    assert runner.events.index("close-connectivity") < runner.events.index(
        "write-state:cutover-pending"
    )

    resumed = runner.execute_resume(candidate())
    assert resumed.error is None
    assert runner.events.index("write-state:opening-writes") < runner.events.index(
        "restore-connectivity"
    )
    assert runner.events.index("restore-connectivity") < runner.events.index("start-apps")
    assert runner.events.index("start-apps") < runner.events.index("release-lock")
    assert runner.state["phase"] == "complete"


def test_failed_resume_is_forward_retryable_but_never_rollbackable() -> None:
    runner = FakeLegacyCutover()
    cutover_outcome = runner.execute_cutover(candidate(), {"status": "success"})
    assert cutover_outcome.error is None

    runner.fail_at = "start"
    first = runner.execute_resume(candidate())
    assert first.error is not None
    assert runner.state["phase"] == "opening-writes"
    assert runner.state["writesOpened"] is True
    assert "mark-recovery:opening-writes" in runner.events

    refused = runner.execute_rollback(candidate())
    assert refused.error is not None
    assert runner.state["phase"] == "opening-writes"

    runner.fail_at = None
    second = runner.execute_resume(candidate())
    assert second.error is None
    assert runner.state["phase"] == "complete"
    assert runner.events.count("start-apps") == 2
    assert runner.events.count("restore-connectivity") == 2
    assert "clear-recovery-marker" in runner.events


def test_rollback_is_refused_permanently_after_writes_open() -> None:
    runner = FakeLegacyCutover(state={
        "candidateDatabase": candidate(),
        "operationId": "00000000-0000-4000-8000-000000000166",
        "phase": "complete",
        "writesOpened": True,
        "rollbackDatabase": "massar_platform_rollback_20260727T120000Z",
        "createdFiles": [],
    })
    outcome = runner.execute_rollback(candidate())
    assert outcome.error is not None
    assert "atomic-swap-back" not in runner.events
    assert "remove-created-files" not in runner.events


def test_rollback_swaps_database_before_deleting_only_created_files() -> None:
    runner = FakeLegacyCutover(state={
        "candidateDatabase": candidate(),
        "operationId": "00000000-0000-4000-8000-000000000166",
        "phase": "cutover-pending",
        "writesOpened": False,
        "rollbackDatabase": "massar_platform_rollback_20260727T120000Z",
        "createdFiles": [{"area": "public", "relativePath": "a", "sha256": "a" * 64}],
        "filesPublished": True,
        "createdDirectories": [],
        "stagingRoot": "/var/lib/massar/legacy-import/00000000-0000-4000-8000-000000000166",
    })
    outcome = runner.execute_rollback(candidate())
    assert outcome.error is None
    assert runner.events.index("atomic-swap-back") < runner.events.index("remove-created-files")
    assert runner.events.index("remove-created-files") < runner.events.index("start-apps")


def test_source_contract_contains_quiesce_and_single_transaction_rename() -> None:
    source = (SCRIPTS / "manage_legacy_cutover.py").read_text(encoding="utf-8")
    assert "StrictSshTransport" in source
    assert "ALLOW_CONNECTIONS false" in source
    assert "pg_terminate_backend" in source
    assert "pg_stat_activity" in source
    assert "ELSE 1/0" not in source
    assert "database swap preconditions are not satisfied" in source
    assert source.index("BEGIN;") < source.index(
        'ALTER DATABASE "{LIVE_DATABASE}" RENAME TO'
    )
    assert "--single-transaction" in source
    assert "BLOCK_COLLISION" in source
    assert "src_dir_fd=parent_fd,dst_dir_fd=parent_fd" in source
    assert "publish-journal.jsonl" in source
    assert "legacy-execution.lock" in source
    assert "legacy-recovery-required.json" in source
    assert "ensure_operator_roots" in source
    assert "massar-ops:massar:700" in source
    for forbidden in (
        "sudo python3",
        "sudo rm",
        "sudo mv",
        "sudo cat",
        "sudo mkdir",
        "sudo rmdir",
        "sudo touch",
        "sudo gpg",
        "sudo sha256sum",
    ):
        assert forbidden not in source
    sudoers = (
        ROOT
        / "deploy/production/config/sudoers/massar-legacy-cutover-gluster"
    ).read_text(encoding="utf-8")
    assert "/usr/sbin/gluster volume heal massar-shared info summary" in sudoers
    assert "/usr/sbin/gluster volume heal massar-shared info split-brain" in sudoers
    cutover_body = source[source.index("def execute_cutover"):source.index("def execute_resume")]
    assert cutover_body.index("self.drain_and_stop_apps") < cutover_body.index(
        "self.publish_staged_files"
    )
    assert cutover_body.index("self.database_quiesce") < cutover_body.index(
        "self.publish_staged_files"
    )
    prepare_body = source[source.index("def prepare_remote"):source.index("def cleanup_prepare")]
    assert "publish-journal.jsonl" not in prepare_body
    assert "rollback is permanently refused after candidate writes are opened" in source
    assert "com.docker.compose.service=worker" in source
    assert "com.docker.compose.service=backend" in source
    clusterctl_source = (SCRIPTS / "clusterctl.py").read_text(encoding="utf-8")
    for action in ("legacy-prepare", "legacy-cutover", "legacy-resume", "legacy-rollback"):
        assert action in clusterctl_source
    assert "manage_legacy_cutover.py" in clusterctl_source
