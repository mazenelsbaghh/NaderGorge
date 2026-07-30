#!/usr/bin/env python3
"""Restore the encrypted legacy backup into an isolated local PostgreSQL 16 stage."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import secrets
import shutil
import subprocess
import time
from pathlib import Path
from typing import Any


CONTAINER = "massar-legacy-stage-166"
RESET_TABLES = (
    "VideoPlaybackSessions",
    "cluster_leases",
)
REQUIRED_ARTIFACTS = ("database", "assets", "protected", "appData")
BACKUP_ID_PATTERN = re.compile(r"^legacy-[A-Za-z0-9][A-Za-z0-9._-]{7,119}$")
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


def run(argv: list[str], **kwargs) -> subprocess.CompletedProcess:
    completed = subprocess.run(argv, check=False, text=True, capture_output=True, **kwargs)
    if completed.returncode:
        raise RuntimeError(completed.stderr.strip() or f"{argv[0]} failed")
    return completed


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_passphrase(path: Path, backup_directory: Path) -> Path:
    expanded = path.expanduser()
    if expanded.is_symlink():
        raise RuntimeError("backup passphrase must not be a symlink")
    path = expanded.resolve()
    if not path.is_file() or path.stat().st_mode & 0o077:
        raise RuntimeError("backup passphrase must be a private regular file")
    if len(path.read_bytes().strip()) < 32:
        raise RuntimeError("backup passphrase must contain at least 32 bytes")
    try:
        path.relative_to(backup_directory)
    except ValueError:
        return path
    raise RuntimeError("backup passphrase must be stored outside the backup directory")


def load_verified_manifest(
    backup: Path,
) -> tuple[dict[str, Any], dict[str, Path], dict[str, Any]]:
    manifest_path = backup / "manifest.json"
    checksum_path = backup / "manifest.sha256"
    capture_evidence_path = backup / "capture-evidence.json"
    if (
        manifest_path.is_symlink()
        or checksum_path.is_symlink()
        or capture_evidence_path.is_symlink()
        or not manifest_path.is_file()
        or not checksum_path.is_file()
        or not capture_evidence_path.is_file()
    ):
        raise RuntimeError("capture evidence, manifest and manifest SHA-256 sidecar are required")
    checksum_fields = checksum_path.read_text(encoding="utf-8").strip().split()
    if (
        len(checksum_fields) != 2
        or not SHA256_PATTERN.fullmatch(checksum_fields[0])
        or checksum_fields[1] != manifest_path.name
        or sha256(manifest_path) != checksum_fields[0]
    ):
        raise RuntimeError("manifest SHA-256 verification failed")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    backup_identifier = manifest.get("backupId")
    if (
        manifest.get("schemaVersion") != 1
        or manifest.get("status") != "success"
        or not isinstance(backup_identifier, str)
        or not BACKUP_ID_PATTERN.fullmatch(backup_identifier)
    ):
        raise RuntimeError("verified legacy backup manifest is required")
    capture_evidence = json.loads(capture_evidence_path.read_text(encoding="utf-8"))
    source_mode = manifest.get("sourceMode", "read-only")
    freeze_requested = manifest.get("freezeRequested", False)
    leave_frozen_requested = manifest.get("leaveWritersFrozenRequested", False)
    writers_before = capture_evidence.get("writersRunningBeforeFreeze", [])
    writers_restarted = capture_evidence.get("writersRestarted", [])
    writers_frozen = capture_evidence.get("writersFrozenAtCompletion", False)
    if (
        capture_evidence.get("schemaVersion") != 1
        or capture_evidence.get("backupId") != backup_identifier
        or capture_evidence.get("status") != "success"
        or capture_evidence.get("temporarySnapshotRemoved") is not True
        or source_mode not in {"read-only", "frozen-writers", "frozen-writers-held"}
        or freeze_requested is not (source_mode != "read-only")
        or leave_frozen_requested is not (source_mode == "frozen-writers-held")
        or capture_evidence.get("freezeRequested", freeze_requested) is not freeze_requested
        or capture_evidence.get(
            "leaveWritersFrozenRequested", leave_frozen_requested
        ) is not leave_frozen_requested
        or not isinstance(writers_before, list)
        or not all(isinstance(item, str) for item in writers_before)
        or not isinstance(writers_restarted, list)
        or not all(isinstance(item, str) for item in writers_restarted)
    ):
        raise RuntimeError("legacy backup capture did not complete cleanly")
    authoritative_source = (
        source_mode == "frozen-writers-held"
        and freeze_requested is True
        and leave_frozen_requested is True
        and "massar_backend" in writers_before
        and writers_restarted == []
        and writers_frozen is True
        and capture_evidence.get("writerRecoveryComplete") is False
        and isinstance(capture_evidence.get("sourceHost"), str)
        and bool(capture_evidence["sourceHost"])
        and isinstance(capture_evidence.get("sourceUser"), str)
        and bool(capture_evidence["sourceUser"])
    )
    if source_mode == "frozen-writers-held":
        if not authoritative_source:
            raise RuntimeError(
                "authoritative legacy capture does not prove writers remained frozen"
            )
    elif (
        writers_frozen is not False
        or capture_evidence.get("writerRecoveryComplete") is not True
    ):
        raise RuntimeError("legacy backup writer recovery evidence is inconsistent")
    entries = manifest.get("artifacts")
    if not isinstance(entries, dict) or set(entries) != set(REQUIRED_ARTIFACTS):
        raise RuntimeError("legacy backup manifest has an incomplete artifact set")
    artifacts: dict[str, Path] = {}
    for name in REQUIRED_ARTIFACTS:
        entry = entries.get(name)
        if not isinstance(entry, dict) or entry.get("backupId") != backup_identifier:
            raise RuntimeError(f"legacy backup artifact identity mismatch: {name}")
        filename = entry.get("file")
        expected_sha = entry.get("sha256")
        expected_bytes = entry.get("bytes")
        if (
            not isinstance(filename, str)
            or Path(filename).name != filename
            or not filename.startswith(f"{backup_identifier}-")
            or not isinstance(expected_sha, str)
            or not SHA256_PATTERN.fullmatch(expected_sha)
            or isinstance(expected_bytes, bool)
            or not isinstance(expected_bytes, int)
            or expected_bytes <= 0
            or entry.get("encrypted") is not True
            or entry.get("verified") is not True
        ):
            raise RuntimeError(f"invalid legacy backup artifact contract: {name}")
        artifact = backup / filename
        if artifact.is_symlink() or not artifact.is_file():
            raise RuntimeError(f"legacy backup artifact is missing: {name}")
        if artifact.stat().st_size != expected_bytes or sha256(artifact) != expected_sha:
            raise RuntimeError(f"legacy backup artifact SHA-256 verification failed: {name}")
        artifacts[name] = artifact
    provenance = {
        "backupId": backup_identifier,
        "sourceMode": source_mode,
        "authoritativeSource": authoritative_source,
        "writersFrozenAtCompletion": authoritative_source,
        "sourceHost": capture_evidence.get("sourceHost"),
        "sourceUser": capture_evidence.get("sourceUser"),
        "manifestSha256": checksum_fields[0],
        "captureEvidenceSha256": sha256(capture_evidence_path),
        "artifactSha256": {
            name: str(entries[name]["sha256"])
            for name in REQUIRED_ARTIFACTS
        },
    }
    return manifest, artifacts, provenance


def write_json(path: Path, payload: dict[str, Any], mode: int = 0o640) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temporary.chmod(mode)
    temporary.replace(path)


def decrypt_stream(path: Path, passphrase: Path) -> subprocess.Popen:
    return subprocess.Popen(
        [
            "gpg", "--batch", "--quiet", "--decrypt",
            "--pinentry-mode", "loopback",
            "--passphrase-file", str(passphrase),
            str(path),
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def restore_database(dump: Path, passphrase: Path) -> None:
    decrypt = decrypt_stream(dump, passphrase)
    assert decrypt.stdout is not None
    restore = subprocess.run(
        [
            "docker", "exec", "-i", CONTAINER,
            "pg_restore", "-U", "postgres", "-d", "massar_platform",
            "--no-owner", "--no-acl", "--exit-on-error",
        ],
        stdin=decrypt.stdout,
        capture_output=True,
        check=False,
    )
    decrypt.stdout.close()
    decrypt_error = decrypt.stderr.read().decode(errors="replace") if decrypt.stderr else ""
    decrypt_code = decrypt.wait(timeout=120)
    if decrypt_code or restore.returncode:
        raise RuntimeError(
            (decrypt_error or restore.stderr.decode(errors="replace") or "staging restore failed")[:1000]
        )


def restore_files(archive: Path, passphrase: Path, destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=False)
    decrypt = decrypt_stream(archive, passphrase)
    assert decrypt.stdout is not None
    extract = subprocess.run(
        ["tar", "-xf", "-", "-C", str(destination), "--no-same-owner", "--no-same-permissions"],
        stdin=decrypt.stdout,
        capture_output=True,
        check=False,
    )
    decrypt.stdout.close()
    decrypt_error = decrypt.stderr.read().decode(errors="replace") if decrypt.stderr else ""
    decrypt_code = decrypt.wait(timeout=120)
    if decrypt_code or extract.returncode:
        shutil.rmtree(destination, ignore_errors=True)
        raise RuntimeError(
            (decrypt_error or extract.stderr.decode(errors="replace") or "file restore failed")[:1000]
        )
    if any(path.is_symlink() for path in destination.rglob("*")):
        raise RuntimeError("staging file archive contains a symlink")


def psql(sql: str) -> str:
    return run([
        "docker", "exec", CONTAINER,
        "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
        "-U", "postgres", "-d", "massar_platform", "-c", sql,
    ]).stdout.strip()


def reset_counts(existing: set[str]) -> dict[str, int]:
    result: dict[str, int] = {}
    for table in RESET_TABLES:
        if table not in existing:
            continue
        quoted = '"' + table.replace('"', '""') + '"'
        result[table] = int(psql(f"SELECT count(*) FROM {quoted};"))
    return result


def reset_replay_state(existing: set[str]) -> list[str]:
    tables = [table for table in RESET_TABLES if table in existing]
    statements = ["BEGIN;"]
    for table in tables:
        quoted = '"' + table.replace('"', '""') + '"'
        statements.append(f"DELETE FROM {quoted};")
    statements.append("COMMIT;")
    psql("\n".join(statements))
    return tables


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--backup-dir", required=True, type=Path)
    parser.add_argument("--passphrase-file", required=True, type=Path)
    parser.add_argument("--repository", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    backup = args.backup_dir.expanduser().resolve()
    repository = args.repository.resolve()
    passphrase = validate_passphrase(args.passphrase_file, backup)
    manifest, artifacts, source_capture = load_verified_manifest(backup)
    backup_identifier = str(manifest["backupId"])
    if run(["docker", "ps", "-a", "--filter", f"name=^{CONTAINER}$", "--format", "{{.Names}}"]).stdout.strip():
        raise RuntimeError(f"refusing to replace existing staging container {CONTAINER}")

    password = secrets.token_urlsafe(48)
    restore_id = "legacy-restore-" + secrets.token_hex(16)
    environment = {**os.environ, "POSTGRES_PASSWORD": password}
    run([
        "docker", "run", "-d", "--name", CONTAINER,
        "--label", f"massar.legacy.restore-id={restore_id}",
        "-e", "POSTGRES_PASSWORD",
        "-p", "127.0.0.1::5432",
        "postgres:16-alpine",
    ], env=environment)
    try:
        for _ in range(60):
            ready = subprocess.run(
                ["docker", "exec", CONTAINER, "pg_isready", "-U", "postgres"],
                capture_output=True,
                check=False,
            )
            if ready.returncode == 0:
                break
            time.sleep(1)
        else:
            raise RuntimeError("staging PostgreSQL did not become ready")
        run([
            "docker", "exec", CONTAINER,
            "createdb", "-U", "postgres", "massar_platform",
        ])
        restore_database(artifacts["database"], passphrase)
        port = run(["docker", "port", CONTAINER, "5432/tcp"]).stdout.strip().rsplit(":", 1)[-1]
        connection = (
            f"Host=127.0.0.1;Port={port};Database=massar_platform;"
            f"Username=postgres;Password={password};Pooling=false"
        )
        migration = subprocess.run(
            [
                "dotnet", "run", "--project",
                str(repository / "backend/src/NaderGorge.Migrator/NaderGorge.Migrator.csproj"),
                "--no-build", "--no-launch-profile",
            ],
            text=True,
            capture_output=True,
            check=False,
            env={**os.environ, "ConnectionStrings__DefaultConnection": connection},
        )
        if migration.returncode:
            raise RuntimeError((migration.stderr.strip() or "staging migration failed")[:1000])

        existing = set(psql(
            "select tablename from pg_tables where schemaname='public';"
        ).splitlines())
        counts_before_reset = reset_counts(existing)
        reset_audit = args.output.with_name(args.output.stem + "-reset-audit.json")
        write_json(reset_audit, {
            "schemaVersion": 1,
            "backupId": backup_identifier,
            "restoreId": restore_id,
            "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
            "status": "captured-before-reset",
            "tableCountsBeforeReset": counts_before_reset,
        })
        reset_tables = reset_replay_state(existing)

        migration_count, latest = psql(
            'select count(*)::text || \'|\' || max("MigrationId") from "__EFMigrationsHistory";'
        ).split("|", 1)
        orphan_count = psql(
            "select count(*) from pg_constraint where connamespace='public'::regnamespace and not convalidated;"
        )
        invalid_indexes = psql("select count(*) from pg_index where not indisvalid;")
        user_count = psql('select count(*) from users;')
        assets = backup / "staging-files-assets"
        protected = backup / "staging-files-protected"
        app_data = backup / "staging-files-app-data"
        restore_files(artifacts["assets"], passphrase, assets)
        restore_files(artifacts["protected"], passphrase, protected)
        restore_files(artifacts["appData"], passphrase, app_data)
        file_counts = {
            "assets": sum(1 for path in assets.rglob("*") if path.is_file()),
            "protected": sum(1 for path in protected.rglob("*") if path.is_file()),
            "appData": sum(1 for path in app_data.rglob("*") if path.is_file()),
        }
        payload = {
            "schemaVersion": 1,
            "backupId": backup_identifier,
            "restoreId": restore_id,
            "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
            "status": "success",
            "isolated": True,
            "container": CONTAINER,
            "hostPort": int(port),
            "migrationCount": int(migration_count),
            "latestMigration": latest,
            "unvalidatedConstraintCount": int(orphan_count),
            "invalidIndexCount": int(invalid_indexes),
            "userCount": int(user_count),
            "resetTables": reset_tables,
            "resetTableCountsBefore": counts_before_reset,
            "resetAudit": str(reset_audit),
            "fileCounts": file_counts,
            "sourceCapture": source_capture,
            "credentialScope": "disposable-local-container",
        }
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        args.output.chmod(0o640)
        print(json.dumps({
            "status": "success",
            "backupId": backup_identifier,
            "container": CONTAINER,
            "migrationCount": int(migration_count),
            "userCount": int(user_count),
            "fileCounts": file_counts,
            "output": str(args.output),
        }))
        return 0
    except Exception:
        subprocess.run(["docker", "rm", "-f", CONTAINER], capture_output=True, check=False)
        raise


if __name__ == "__main__":
    raise SystemExit(main())
