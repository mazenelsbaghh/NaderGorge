from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
PATH = SCRIPTS / "capture_legacy_backup.py"
SPEC = importlib.util.spec_from_file_location("capture_legacy_backup", PATH)
assert SPEC and SPEC.loader
capture = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(capture)


def test_legacy_backup_streams_encrypted_read_only_and_verifies() -> None:
    source = PATH.read_text(encoding="utf-8")
    assert "--symmetric" in source
    assert "--cipher-algo" in source
    assert "--passphrase-file" in source
    assert "pg_dump" in source
    assert "--no-owner" in source
    assert "--no-acl" in source
    assert "pg_restore\", \"--list" in source
    assert ":/source:ro" in source
    assert '"sourceMode":' in source
    assert '"read-only"' in source
    for forbidden in ("POSTGRES_PASSWORD", "docker cp", "pg_restore -d", "psql -c"):
        assert forbidden not in source


def test_legacy_backup_freeze_has_restart_evidence_and_snapshot_cleanup() -> None:
    source = PATH.read_text(encoding="utf-8")
    assert "--freeze-writers" in source
    assert "--leave-writers-frozen" in source
    assert "finally:" in source
    assert "restart_writers" in source
    assert "capture-evidence.json" in source
    assert '"writerRecoveryComplete"' in source
    assert '"writersFrozenAtCompletion"' in source
    assert "snapshot_stopped_backend" in source
    assert "remove_snapshot" in source
    assert "manifest.sha256" not in source
    assert "write_manifest_sha" in source


def test_authoritative_capture_requires_freeze_and_restarts_on_failure() -> None:
    source = PATH.read_text(encoding="utf-8")
    assert "--leave-writers-frozen requires --freeze-writers" in source
    assert "failure is not None or not args.leave_writers_frozen" in source
    assert '"frozen-writers-held"' in source


def test_passphrase_is_required_private_and_outside_backup_directory(
    tmp_path: Path,
) -> None:
    backup = tmp_path / "backup"
    backup.mkdir()
    secret = tmp_path / "secrets" / "legacy-passphrase"
    secret.parent.mkdir()
    secret.write_text("x" * 64, encoding="utf-8")
    secret.chmod(0o600)
    assert capture.validate_passphrase(secret, backup) == secret.resolve()

    nested = backup / "passphrase"
    nested.write_text("y" * 64, encoding="utf-8")
    nested.chmod(0o600)
    with pytest.raises(RuntimeError, match="outside"):
        capture.validate_passphrase(nested, backup)

    secret.chmod(0o644)
    with pytest.raises(RuntimeError, match="private"):
        capture.validate_passphrase(secret, backup)


def test_backup_id_is_validated_and_generated_once() -> None:
    explicit = "legacy-20260727T010203Z-deadbeef"
    assert capture.backup_id(explicit) == explicit
    assert capture.BACKUP_ID_PATTERN.fullmatch(capture.backup_id())
    with pytest.raises(RuntimeError, match="backup ID"):
        capture.backup_id("../../mixed")
