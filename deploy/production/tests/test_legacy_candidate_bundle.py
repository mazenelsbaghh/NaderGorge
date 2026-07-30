from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import os
import stat
import subprocess
import sys
import tarfile
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
SPEC = importlib.util.spec_from_file_location(
    "build_legacy_candidate_bundle",
    SCRIPTS / "build_legacy_candidate_bundle.py",
)
assert SPEC and SPEC.loader
bundle = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = bundle
SPEC.loader.exec_module(bundle)


def validation_evidence(path: Path, **overrides: object) -> Path:
    migrations = ["001_First"]
    counts = {"__EFMigrationsHistory": 1, "users": 7}
    value = {
        "schemaVersion": 1,
        "status": "success",
        "isolated": True,
        "migrationModelMatch": True,
        "criticalFindingCount": 0,
        "backupId": "legacy-20260727T010203Z-deadbeef",
        "restoreId": "legacy-restore-" + "1" * 32,
        "restoreEvidenceSha256": "2" * 64,
        "sourceCapture": {
            "backupId": "legacy-20260727T010203Z-deadbeef",
            "sourceHost": "192.0.2.10",
            "sourceUser": "root",
            "sourceMode": "frozen-writers",
            "authoritativeSource": False,
            "writersFrozenAtCompletion": False,
            "manifestSha256": "3" * 64,
            "captureEvidenceSha256": "4" * 64,
            "artifactSha256": {
                "database": "6" * 64,
                "assets": "7" * 64,
                "protected": "8" * 64,
                "appData": "9" * 64,
            },
        },
        "migrationIds": migrations,
        "tableCounts": counts,
        "tableCountsSha256": bundle.table_counts_sha256(counts),
        "userCount": 7,
        "userWithoutRoleCount": 0,
        "unsupportedProviderCount": 0,
        "stagingFileCount": 0,
        "stagingFileTreeSha256": "5" * 64,
        "resetTableCounts": {
            "VideoPlaybackSessions": 0,
            "cluster_leases": 0,
        },
        "fileReferences": {"missingUnblockedReferences": 0},
    }
    value.update(overrides)
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def source_chain(
    backup: Path,
    evidence: Path,
    *,
    authoritative: bool = False,
) -> Path:
    value = json.loads(evidence.read_text(encoding="utf-8"))
    source_manifest = {
        "schemaVersion": 1,
        "status": "success",
        "backupId": value["backupId"],
        "sourceHost": "192.0.2.10",
        "sourceUser": "root",
        "artifacts": {
            name: {"sha256": digest}
            for name, digest in value["sourceCapture"]["artifactSha256"].items()
        },
    }
    manifest_path = backup / "manifest.json"
    manifest_path.write_text(json.dumps(source_manifest), encoding="utf-8")
    capture_path = backup / "capture-evidence.json"
    capture_path.write_text(json.dumps({
        "schemaVersion": 1,
        "status": "success",
        "backupId": value["backupId"],
    }), encoding="utf-8")
    source = {
        "backupId": value["backupId"],
        "sourceHost": "192.0.2.10",
        "sourceUser": "root",
        "sourceMode": "frozen-writers-held" if authoritative else "frozen-writers",
        "authoritativeSource": authoritative,
        "writersFrozenAtCompletion": authoritative,
        "manifestSha256": bundle.sha256_file(manifest_path),
        "captureEvidenceSha256": bundle.sha256_file(capture_path),
        "artifactSha256": {
            name: entry["sha256"]
            for name, entry in source_manifest["artifacts"].items()
        },
    }
    restore = {
        "schemaVersion": 1,
        "status": "success",
        "isolated": True,
        "backupId": value["backupId"],
        "restoreId": value["restoreId"],
        "sourceCapture": source,
    }
    restore_path = backup / "restore-evidence.json"
    restore_path.write_text(json.dumps(restore), encoding="utf-8")
    file_count, file_digest = bundle.staging_file_tree_snapshot(backup)
    value.update({
        "sourceCapture": source,
        "restoreEvidenceSha256": bundle.sha256_file(restore_path),
        "stagingFileCount": file_count,
        "stagingFileTreeSha256": file_digest,
    })
    evidence.write_text(json.dumps(value), encoding="utf-8")
    return restore_path


def staging_roots(path: Path) -> Path:
    for name in (
        "staging-files-assets",
        "staging-files-protected",
        "staging-files-app-data",
    ):
        (path / name).mkdir(parents=True)
    return path


def private_passphrase(path: Path) -> Path:
    path.write_bytes(b"x" * 48)
    path.chmod(0o600)
    return path


def test_repository_migrations_include_attributed_manual_migrations() -> None:
    migrations = bundle.repository_migration_ids(ROOT)
    assert len(migrations) >= 129
    assert migrations == tuple(sorted(set(migrations)))
    assert "20260613161000_FixFindTheMistakeMissingFields" in migrations
    assert "20260726182136_EnsureSystemRoles" in migrations


def test_manual_migrations_are_discoverable_by_ef_runtime() -> None:
    migration_root = ROOT / "backend/src/NaderGorge.Infrastructure/Migrations"
    for name in (
        "20260706180000_AddSharedPackageItemPrices.cs",
        "20260706193000_AddTeacherStaffPermissions.cs",
    ):
        source = (migration_root / name).read_text(encoding="utf-8")
        assert "[DbContext(typeof(AppDbContext))]" in source
        assert "[Migration(" in source


@pytest.mark.parametrize(
    "overrides",
    [
        {"status": "failed"},
        {"isolated": False},
        {"migrationModelMatch": False},
        {"criticalFindingCount": 1},
        {"userWithoutRoleCount": 1},
        {"unsupportedProviderCount": 1},
        {"resetTableCounts": {"cluster_leases": 1}},
        {"fileReferences": {"missingUnblockedReferences": 1}},
    ],
)
def test_validation_gate_rejects_unproven_staging(
    tmp_path: Path,
    overrides: dict[str, object],
) -> None:
    evidence = validation_evidence(tmp_path / "validation.json", **overrides)
    with pytest.raises(bundle.CandidateBuildError, match="clean isolated"):
        bundle.load_validation_evidence(evidence)


def test_file_mapping_is_explicit_and_deduplicates_identical_content(
    tmp_path: Path,
) -> None:
    backup = staging_roots(tmp_path / "backup")
    (backup / "staging-files-assets/subtitles").mkdir()
    (backup / "staging-files-assets/subtitles/lesson.srt").write_bytes(b"caption")
    (backup / "staging-files-assets/protected/resources").mkdir(parents=True)
    (backup / "staging-files-assets/protected/resources/book.pdf").write_bytes(b"book")
    (backup / "staging-files-protected").joinpath("book.pdf").write_bytes(b"book")
    (backup / "staging-files-app-data/live-support").mkdir()
    (backup / "staging-files-app-data/live-support/chat.bin").write_bytes(b"chat")
    (backup / "staging-files-app-data/private").mkdir()
    (backup / "staging-files-app-data/private/key.bin").write_bytes(b"private")

    entries = bundle.collect_source_files(backup)

    assert [entry.archive_path for entry in entries] == [
        "live-support/chat.bin",
        "private/key.bin",
        "protected/resources/book.pdf",
        "public/subtitles/lesson.srt",
    ]
    assert sum(entry.archive_path == "protected/resources/book.pdf" for entry in entries) == 1


def test_unknown_app_data_path_and_symlink_are_rejected(tmp_path: Path) -> None:
    backup = staging_roots(tmp_path / "backup")
    (backup / "staging-files-app-data/unknown").mkdir()
    (backup / "staging-files-app-data/unknown/value.bin").write_bytes(b"value")
    with pytest.raises(bundle.CandidateBuildError, match="unmapped App_Data"):
        bundle.collect_source_files(backup)

    (backup / "staging-files-app-data/unknown/value.bin").unlink()
    (backup / "staging-files-app-data/unknown").rmdir()
    target = tmp_path / "outside"
    target.write_bytes(b"outside")
    os.symlink(target, backup / "staging-files-assets/link")
    with pytest.raises(bundle.CandidateBuildError, match="symlink"):
        bundle.collect_source_files(backup)


def test_conflicting_duplicate_destination_is_rejected(tmp_path: Path) -> None:
    backup = staging_roots(tmp_path / "backup")
    (backup / "staging-files-assets/protected/resources").mkdir(parents=True)
    (backup / "staging-files-protected").joinpath("book.pdf").write_bytes(b"new")
    (backup / "staging-files-assets/protected/resources/book.pdf").write_bytes(b"old")
    with pytest.raises(bundle.CandidateBuildError, match="conflicting"):
        bundle.collect_source_files(backup)


def test_tar_stream_contains_only_exact_regular_manifest_files(tmp_path: Path) -> None:
    backup = staging_roots(tmp_path / "backup")
    (backup / "staging-files-assets/subtitles").mkdir()
    content = b"1\n00:00:00,000 --> 00:00:01,000\nhello\n"
    (backup / "staging-files-assets/subtitles/lesson.srt").write_bytes(content)
    entries = bundle.collect_source_files(backup)
    output = io.BytesIO()

    bundle.write_tar_stream(output, entries)

    output.seek(0)
    with tarfile.open(fileobj=output, mode="r:") as archive:
        members = archive.getmembers()
        assert [member.name for member in members] == ["public/subtitles/lesson.srt"]
        assert all(member.isfile() for member in members)
        assert stat.S_IMODE(members[0].mode) == 0o644
        extracted = archive.extractfile(members[0])
        assert extracted is not None
        assert extracted.read() == content


def test_staging_snapshot_parses_exact_migrations_and_table_counts(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    responses = iter([
        "001_First\n002_Second",
        '{"users": 7, "__EFMigrationsHistory": 2}',
    ])
    monkeypatch.setattr(bundle, "run_checked", lambda argv: next(responses))

    migrations, counts = bundle.staging_database_snapshot()

    assert migrations == ("001_First", "002_Second")
    assert counts == {"__EFMigrationsHistory": 2, "users": 7}


def test_validation_evidence_is_bound_to_exact_current_database(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    backup = staging_roots(tmp_path / "backup")
    evidence = validation_evidence(tmp_path / "validation.json")
    restore_evidence = source_chain(backup, evidence)
    passphrase = private_passphrase(tmp_path / "passphrase")
    monkeypatch.setattr(bundle, "repository_migration_ids", lambda repository: ("001_First",))
    monkeypatch.setattr(
        bundle,
        "staging_database_snapshot",
        lambda: (("001_First",), {"__EFMigrationsHistory": 1, "users": 8}),
    )
    with pytest.raises(bundle.CandidateBuildError, match="changed after validation"):
        bundle.build_bundle(
            backup=backup,
            repository=ROOT,
            validation_evidence=evidence,
            restore_evidence=restore_evidence,
            passphrase=passphrase,
            output=tmp_path / "candidate",
            now=bundle.utc_now(),
        )


def test_dry_run_never_attempts_docker_gpg_or_creates_output(tmp_path: Path) -> None:
    backup = staging_roots(tmp_path / "backup")
    (backup / "staging-files-assets/readme.txt").write_bytes(b"safe")
    evidence = validation_evidence(tmp_path / "validation.json")
    validation = json.loads(evidence.read_text(encoding="utf-8"))
    validation["migrationIds"] = list(bundle.repository_migration_ids(ROOT))
    evidence.write_text(json.dumps(validation), encoding="utf-8")
    restore_evidence = source_chain(backup, evidence)
    passphrase = private_passphrase(tmp_path / "passphrase")
    output = tmp_path / "candidate"

    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "build_legacy_candidate_bundle.py"),
            "--backup-dir", str(backup),
            "--validation-evidence", str(evidence),
            "--restore-evidence", str(restore_evidence),
            "--repository", str(ROOT),
            "--passphrase-file", str(passphrase),
            "--output-dir", str(output),
            "--dry-run",
        ],
        text=True,
        capture_output=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    result = json.loads(completed.stdout)
    assert result["status"] == "dry-run"
    assert result["dockerAttempted"] is False
    assert result["gpgAttempted"] is False
    assert result["sshAttempted"] is False
    assert result["plaintextArtifactsWritten"] is False
    assert not output.exists()


def test_manifest_contains_complete_authoritative_provenance_contract(
    tmp_path: Path,
) -> None:
    backup = staging_roots(tmp_path / "backup")
    (backup / "staging-files-assets/file.bin").write_bytes(b"file")
    files = bundle.collect_source_files(backup)
    temporary = tmp_path / "temporary"
    final = tmp_path / "final"
    temporary.mkdir()
    dump = temporary / "candidate.dump.gpg"
    archive = temporary / "files.tar.gpg"
    dump.write_bytes(b"encrypted-db")
    archive.write_bytes(b"encrypted-files")
    manifest = temporary / "manifest.json"
    evidence = validation_evidence(tmp_path / "validation.json")
    validation = json.loads(evidence.read_text(encoding="utf-8"))

    bundle.write_manifest(
        manifest,
        backup_id="legacy-candidate-20260727T120000Z-0123456789ab",
        final_output=final,
        dump=dump,
        archive=archive,
        migrations=("001_First",),
        table_counts={"__EFMigrationsHistory": 1, "users": 7},
        files=files,
        validation=validation,
        validation_sha256=bundle.sha256_file(evidence),
        authoritative_final=True,
    )

    value = json.loads(manifest.read_text())
    assert set(value) == {
        "schemaVersion", "status", "backupId", "candidateDump", "fileArchive",
        "migrationIds", "tableCounts", "files", "candidateMode",
        "eligibleForCutover", "sourceCapture", "sourceBackupId", "restoreId",
        "restoreEvidenceSha256", "validationEvidenceSha256",
    }
    assert value["schemaVersion"] == 2
    assert value["candidateMode"] == "authoritative-final"
    assert value["eligibleForCutover"] is True
    assert value["candidateDump"]["path"] == str(final / dump.name)
    assert value["candidateDump"]["sha256"] == hashlib.sha256(
        b"encrypted-db"
    ).hexdigest()
    assert stat.S_IMODE(manifest.stat().st_mode) == 0o600


def test_authoritative_candidate_rejects_recovered_source_writers(
    tmp_path: Path,
) -> None:
    backup = staging_roots(tmp_path / "backup")
    evidence = validation_evidence(tmp_path / "validation.json")
    source_chain(backup, evidence, authoritative=False)
    validation = bundle.load_validation_evidence(evidence)

    with pytest.raises(bundle.CandidateBuildError, match="writers frozen"):
        bundle.verify_source_capture(
            backup,
            validation,
            authoritative_final=True,
        )


def test_candidate_rejects_files_changed_after_validation(tmp_path: Path) -> None:
    backup = staging_roots(tmp_path / "backup")
    file_path = backup / "staging-files-assets/file.bin"
    file_path.write_bytes(b"before")
    evidence = validation_evidence(tmp_path / "validation.json")
    source_chain(backup, evidence)
    validation = bundle.load_validation_evidence(evidence)
    file_path.write_bytes(b"after")

    with pytest.raises(bundle.CandidateBuildError, match="files changed"):
        bundle.require_unchanged_staging_files(backup, validation)


def test_source_contract_streams_plaintext_and_verifies_both_artifacts() -> None:
    source = (SCRIPTS / "build_legacy_candidate_bundle.py").read_text(encoding="utf-8")
    for contract in (
        "pg_dump",
        "--serializable-deferrable",
        "--symmetric",
        "--cipher-algo",
        "AES256",
        "pg_restore",
        "--list",
        "verify_file_archive",
        "plaintextArtifactsWritten",
        "os.rename(temporary, output)",
    ):
        assert contract in source
    assert "candidate.dump.gpg" in source
    assert "files.tar.gpg" in source
