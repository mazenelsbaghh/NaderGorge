from __future__ import annotations

import importlib.util
import json
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
PATH = ROOT / "deploy/production/scripts/restore_legacy_staging.py"
SPEC = importlib.util.spec_from_file_location("restore_legacy_staging", PATH)
assert SPEC and SPEC.loader
restore = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(restore)


def test_staging_restore_is_isolated_migrated_and_resets_replay_state() -> None:
    source = PATH.read_text(encoding="utf-8")
    assert "massar-legacy-stage-166" in source
    assert "refusing to replace existing staging container" in source
    assert "NaderGorge.Migrator" in source
    assert "--no-build" in source
    assert '"cluster_leases"' in source
    assert '"VideoPlaybackSessions"' in source
    assert '"outbox_events"' not in source
    assert '"refresh_tokens"' not in source
    assert '"isolated": True' in source
    assert '"credentialScope": "disposable-local-container"' in source
    assert "--no-same-owner" in source
    assert "is_symlink()" in source
    assert "--passphrase-file" in source
    assert "load_verified_manifest" in source
    assert "resetTableCountsBefore" in source
    assert "captured-before-reset" in source
    assert '"restoreId": restore_id' in source


def create_manifest(backup: Path) -> dict[str, object]:
    backup_id = "legacy-20260727T010203Z-deadbeef"
    entries: dict[str, object] = {}
    for name in restore.REQUIRED_ARTIFACTS:
        artifact = backup / f"{backup_id}-{name}.gpg"
        artifact.write_bytes((name * 4).encode())
        entries[name] = {
            "backupId": backup_id,
            "file": artifact.name,
            "encrypted": True,
            "verified": True,
            "bytes": artifact.stat().st_size,
            "sha256": restore.sha256(artifact),
        }
    manifest: dict[str, object] = {
        "schemaVersion": 1,
        "backupId": backup_id,
        "status": "success",
        "artifacts": entries,
    }
    manifest_path = backup / "manifest.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    (backup / "manifest.sha256").write_text(
        f"{restore.sha256(manifest_path)}  manifest.json\n",
        encoding="utf-8",
    )
    (backup / "capture-evidence.json").write_text(json.dumps({
        "schemaVersion": 1,
        "backupId": backup_id,
        "status": "success",
        "writerRecoveryComplete": True,
        "temporarySnapshotRemoved": True,
    }), encoding="utf-8")
    return manifest


def test_restore_verifies_manifest_and_every_artifact_before_use(
    tmp_path: Path,
) -> None:
    backup = tmp_path / "backup"
    backup.mkdir()
    expected = create_manifest(backup)
    manifest, artifacts, provenance = restore.load_verified_manifest(backup)
    assert manifest["backupId"] == expected["backupId"]
    assert set(artifacts) == set(restore.REQUIRED_ARTIFACTS)
    assert provenance["authoritativeSource"] is False

    artifacts["database"].write_bytes(b"tampered")
    with pytest.raises(RuntimeError, match="artifact SHA-256"):
        restore.load_verified_manifest(backup)


def test_restore_distinguishes_rehearsal_from_authoritative_source(
    tmp_path: Path,
) -> None:
    backup = tmp_path / "backup"
    backup.mkdir()
    manifest = create_manifest(backup)
    manifest["sourceMode"] = "frozen-writers-held"
    manifest["freezeRequested"] = True
    manifest["leaveWritersFrozenRequested"] = True
    manifest_path = backup / "manifest.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    (backup / "manifest.sha256").write_text(
        f"{restore.sha256(manifest_path)}  manifest.json\n",
        encoding="utf-8",
    )
    capture_path = backup / "capture-evidence.json"
    capture_path.write_text(json.dumps({
        "schemaVersion": 1,
        "backupId": manifest["backupId"],
        "status": "success",
        "sourceHost": "192.0.2.10",
        "sourceUser": "root",
        "freezeRequested": True,
        "leaveWritersFrozenRequested": True,
        "writersRunningBeforeFreeze": ["massar_backend", "massar_worker"],
        "writersRestarted": [],
        "writersFrozenAtCompletion": True,
        "writerRecoveryComplete": False,
        "temporarySnapshotRemoved": True,
    }), encoding="utf-8")

    _, _, provenance = restore.load_verified_manifest(backup)

    assert provenance["authoritativeSource"] is True
    assert provenance["writersFrozenAtCompletion"] is True


def test_restore_rejects_claimed_authoritative_source_after_writer_recovery(
    tmp_path: Path,
) -> None:
    backup = tmp_path / "backup"
    backup.mkdir()
    manifest = create_manifest(backup)
    manifest["sourceMode"] = "frozen-writers-held"
    manifest["freezeRequested"] = True
    manifest["leaveWritersFrozenRequested"] = True
    manifest_path = backup / "manifest.json"
    manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
    (backup / "manifest.sha256").write_text(
        f"{restore.sha256(manifest_path)}  manifest.json\n",
        encoding="utf-8",
    )
    capture_path = backup / "capture-evidence.json"
    capture_path.write_text(json.dumps({
        "schemaVersion": 1,
        "backupId": manifest["backupId"],
        "status": "success",
        "sourceHost": "192.0.2.10",
        "sourceUser": "root",
        "freezeRequested": True,
        "leaveWritersFrozenRequested": True,
        "writersRunningBeforeFreeze": ["massar_backend"],
        "writersRestarted": ["massar_backend"],
        "writersFrozenAtCompletion": False,
        "writerRecoveryComplete": True,
        "temporarySnapshotRemoved": True,
    }), encoding="utf-8")

    with pytest.raises(RuntimeError, match="writers remained frozen"):
        restore.load_verified_manifest(backup)


def test_restore_refuses_tampered_manifest_sidecar(tmp_path: Path) -> None:
    backup = tmp_path / "backup"
    backup.mkdir()
    create_manifest(backup)
    manifest_path = backup / "manifest.json"
    manifest_path.write_text(
        manifest_path.read_text(encoding="utf-8") + "\n",
        encoding="utf-8",
    )
    with pytest.raises(RuntimeError, match="manifest SHA-256"):
        restore.load_verified_manifest(backup)


def test_reset_counts_are_captured_before_one_transactional_delete(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    queries: list[str] = []

    def fake_psql(sql: str) -> str:
        queries.append(sql)
        if sql.startswith("SELECT count"):
            return "7"
        return ""

    monkeypatch.setattr(restore, "psql", fake_psql)
    existing = {"VideoPlaybackSessions", "cluster_leases", "unrelated"}
    assert restore.reset_counts(existing) == {
        "VideoPlaybackSessions": 7,
        "cluster_leases": 7,
    }
    reset = restore.reset_replay_state(existing)
    assert reset == ["VideoPlaybackSessions", "cluster_leases"]
    assert queries[-1].startswith("BEGIN;")
    assert 'DELETE FROM "VideoPlaybackSessions";' in queries[-1]
    assert 'DELETE FROM "cluster_leases";' in queries[-1]
    assert queries[-1].endswith("COMMIT;")
