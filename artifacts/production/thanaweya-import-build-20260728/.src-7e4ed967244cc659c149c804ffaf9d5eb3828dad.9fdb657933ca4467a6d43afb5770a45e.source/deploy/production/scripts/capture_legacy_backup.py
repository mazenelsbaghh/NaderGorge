#!/usr/bin/env python3
"""Stream encrypted, verified backups from the approved legacy test host."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import re
import secrets
import shlex
import subprocess
from pathlib import Path
from typing import Any

from ssh_transport import SshTarget, StrictSshTransport


WRITER_CONTAINERS = ("massar_backend", "massar_worker")
BACKUP_ID_PATTERN = re.compile(r"^legacy-[A-Za-z0-9][A-Za-z0-9._-]{7,119}$")


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


def backup_id(value: str | None = None) -> str:
    result = value or (
        "legacy-"
        + dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        + "-"
        + secrets.token_hex(4)
    )
    if not BACKUP_ID_PATTERN.fullmatch(result):
        raise RuntimeError("backup ID must be a safe legacy-* identifier")
    return result


def write_json(path: Path, payload: dict[str, Any], mode: int = 0o600) -> None:
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temporary.chmod(mode)
    temporary.replace(path)


def write_manifest_sha(manifest: Path) -> Path:
    checksum = manifest.with_suffix(".sha256")
    temporary = checksum.with_suffix(checksum.suffix + ".tmp")
    temporary.write_text(f"{sha256(manifest)}  {manifest.name}\n", encoding="utf-8")
    temporary.chmod(0o600)
    temporary.replace(checksum)
    return checksum


def running_writers(
    transport: StrictSshTransport,
    target: SshTarget,
) -> list[str]:
    result: list[str] = []
    for name in WRITER_CONTAINERS:
        completed = transport.run(
            target,
            ("docker", "inspect", "--format", "{{.State.Running}}", name),
            timeout_seconds=30,
            check=False,
        )
        if completed.returncode == 0 and completed.stdout.strip() == "true":
            result.append(name)
    return result


def stop_writers(
    transport: StrictSshTransport,
    target: SshTarget,
    writers: list[str],
) -> None:
    for name in reversed(writers):
        transport.run(
            target,
            ("docker", "stop", "--time", "30", name),
            timeout_seconds=60,
        )


def restart_writers(
    transport: StrictSshTransport,
    target: SshTarget,
    writers: list[str],
) -> list[str]:
    restarted: list[str] = []
    for name in writers:
        transport.run(target, ("docker", "start", name), timeout_seconds=60)
        completed = transport.run(
            target,
            ("docker", "inspect", "--format", "{{.State.Running}}", name),
            timeout_seconds=30,
            check=False,
        )
        if completed.returncode or completed.stdout.strip() != "true":
            raise RuntimeError(f"legacy writer did not restart cleanly: {name}")
        restarted.append(name)
    return restarted


def snapshot_stopped_backend(
    transport: StrictSshTransport,
    target: SshTarget,
) -> str:
    completed = transport.run(
        target,
        ("docker", "commit", "massar_backend"),
        timeout_seconds=300,
    )
    image_id = completed.stdout.strip()
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", image_id):
        raise RuntimeError("legacy backend snapshot did not return an immutable image ID")
    return image_id


def remove_snapshot(
    transport: StrictSshTransport,
    target: SshTarget,
    image_id: str,
) -> None:
    transport.run(
        target,
        ("docker", "image", "rm", "--force", image_id),
        timeout_seconds=120,
    )


def capture_evidence(
    path: Path,
    *,
    backup_identifier: str,
    status: str,
    source_host: str,
    source_user: str,
    freeze_requested: bool,
    leave_writers_frozen_requested: bool,
    writers_before: list[str],
    writers_restarted: list[str],
    writers_frozen_at_completion: bool,
    temporary_snapshot_removed: bool,
    reason: str | None,
) -> None:
    write_json(path, {
        "schemaVersion": 1,
        "backupId": backup_identifier,
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "status": status,
        "sourceHost": source_host,
        "sourceUser": source_user,
        "freezeRequested": freeze_requested,
        "leaveWritersFrozenRequested": leave_writers_frozen_requested,
        "writersRunningBeforeFreeze": writers_before,
        "writersRestarted": writers_restarted,
        "writersFrozenAtCompletion": writers_frozen_at_completion,
        "writerRecoveryComplete": (
            not freeze_requested
            or (not writers_frozen_at_completion and writers_restarted == writers_before)
        ),
        "temporarySnapshotRemoved": temporary_snapshot_removed,
        "reason": reason,
    })


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def stream_encrypted(
    transport: StrictSshTransport,
    host: str,
    user: str,
    remote_argv: list[str],
    destination: Path,
    passphrase: Path,
) -> None:
    remote_command = shlex.join(remote_argv)
    ssh = subprocess.Popen(
        [*transport.base_args(), f"{user}@{host}", "--", remote_command],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env={**os.environ, "LC_ALL": "C"},
    )
    assert ssh.stdout is not None
    encryption = subprocess.run(
        [
            "gpg", "--batch", "--yes", "--symmetric",
            "--cipher-algo", "AES256",
            "--pinentry-mode", "loopback",
            "--passphrase-file", str(passphrase),
            "--output", str(destination),
        ],
        stdin=ssh.stdout,
        capture_output=True,
        check=False,
    )
    ssh.stdout.close()
    stderr = ssh.stderr.read().decode(errors="replace") if ssh.stderr else ""
    return_code = ssh.wait(timeout=600)
    if return_code or encryption.returncode:
        destination.unlink(missing_ok=True)
        raise RuntimeError(
            (stderr or encryption.stderr.decode(errors="replace") or "backup stream failed")[:500]
        )
    destination.chmod(0o600)


def verify_database_backup(path: Path, passphrase: Path) -> None:
    decrypt = subprocess.Popen(
        [
            "gpg", "--batch", "--quiet", "--decrypt",
            "--pinentry-mode", "loopback",
            "--passphrase-file", str(passphrase),
            str(path),
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    assert decrypt.stdout is not None
    verify = subprocess.run(
        ["docker", "run", "--rm", "-i", "postgres:16-alpine", "pg_restore", "--list"],
        stdin=decrypt.stdout,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        check=False,
    )
    decrypt.stdout.close()
    decrypt_error = decrypt.stderr.read().decode(errors="replace") if decrypt.stderr else ""
    decrypt_code = decrypt.wait(timeout=120)
    if decrypt_code or verify.returncode:
        raise RuntimeError(
            (decrypt_error or verify.stderr.decode(errors="replace") or "database verification failed")[:500]
        )


def verify_tar_backup(path: Path, passphrase: Path) -> None:
    decrypt = subprocess.Popen(
        [
            "gpg", "--batch", "--quiet", "--decrypt",
            "--pinentry-mode", "loopback",
            "--passphrase-file", str(passphrase),
            str(path),
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    assert decrypt.stdout is not None
    verify = subprocess.run(
        ["tar", "-tf", "-"],
        stdin=decrypt.stdout,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        check=False,
    )
    decrypt.stdout.close()
    decrypt_error = decrypt.stderr.read().decode(errors="replace") if decrypt.stderr else ""
    decrypt_code = decrypt.wait(timeout=120)
    if decrypt_code or verify.returncode:
        raise RuntimeError(
            (decrypt_error or verify.stderr.decode(errors="replace") or "file backup verification failed")[:500]
        )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", required=True)
    parser.add_argument("--user", default="root")
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--passphrase-file", required=True, type=Path)
    parser.add_argument("--backup-id")
    parser.add_argument("--freeze-writers", action="store_true")
    parser.add_argument(
        "--leave-writers-frozen",
        action="store_true",
        help=(
            "Keep the approved legacy writers stopped after a successful capture. "
            "Required for an authoritative final cutover capture."
        ),
    )
    args = parser.parse_args()
    if args.leave_writers_frozen and not args.freeze_writers:
        parser.error("--leave-writers-frozen requires --freeze-writers")

    directory = args.output_dir.expanduser().resolve()
    directory.mkdir(parents=True, exist_ok=True)
    directory.chmod(0o700)
    if any(directory.iterdir()):
        raise RuntimeError("legacy backup output directory must be empty")
    passphrase = validate_passphrase(args.passphrase_file, directory)
    backup_identifier = backup_id(args.backup_id)
    transport = StrictSshTransport(args.known_hosts, args.identity)
    target = SshTarget("legacy-test", args.host, args.user)
    artifacts = {
        "database": (
            ["docker", "exec", "massar_db", "sh", "-ec",
             'exec pg_dump -U "${POSTGRES_USER:-postgres}" -d massar_platform -Fc --no-owner --no-acl'],
            directory / f"{backup_identifier}-database.dump.gpg",
            verify_database_backup,
        ),
        "assets": (
            ["docker", "run", "--rm", "-v", "massar_assets:/source:ro",
             "postgres:16.2-alpine", "tar", "-C", "/source", "-cf", "-", "."],
            directory / f"{backup_identifier}-assets.tar.gpg",
            verify_tar_backup,
        ),
        "protected": (
            ["docker", "run", "--rm", "-v", "massar_protected_resources:/source:ro",
             "postgres:16.2-alpine", "tar", "-C", "/source", "-cf", "-", "."],
            directory / f"{backup_identifier}-protected.tar.gpg",
            verify_tar_backup,
        ),
    }
    operation_evidence = directory / "capture-evidence.json"
    writers_before: list[str] = []
    writers_restarted: list[str] = []
    failure: Exception | None = None
    artifact_evidence: dict[str, object] = {}
    manifest = directory / "manifest.json"
    manifest_sha: Path | None = None
    snapshot_image = ""
    temporary_snapshot_removed = True
    writers_frozen_at_completion = False
    try:
        if args.freeze_writers:
            writers_before = running_writers(transport, target)
            if not writers_before:
                raise RuntimeError("freeze requested but no approved legacy writer is running")
            if "massar_backend" not in writers_before:
                raise RuntimeError("freeze requested but the legacy backend writer is not running")
            stop_writers(transport, target, writers_before)
            snapshot_image = snapshot_stopped_backend(transport, target)
            temporary_snapshot_removed = False
            artifacts["appData"] = (
                [
                    "docker", "run", "--rm", "--entrypoint", "tar",
                    snapshot_image, "-C", "/app/App_Data", "-cf", "-", ".",
                ],
                directory / f"{backup_identifier}-app-data.tar.gpg",
                verify_tar_backup,
            )
        else:
            artifacts["appData"] = (
                ["docker", "exec", "massar_backend", "tar", "-C", "/app/App_Data", "-cf", "-", "."],
                directory / f"{backup_identifier}-app-data.tar.gpg",
                verify_tar_backup,
            )
        for name, (command, destination, verifier) in artifacts.items():
            stream_encrypted(
                transport,
                args.host,
                args.user,
                command,
                destination,
                passphrase,
            )
            verifier(destination, passphrase)
            artifact_evidence[name] = {
                "backupId": backup_identifier,
                "file": destination.name,
                "encrypted": True,
                "verified": True,
                "bytes": destination.stat().st_size,
                "sha256": sha256(destination),
            }
        payload = {
            "schemaVersion": 1,
            "backupId": backup_identifier,
            "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
            "status": "success",
            "sourceMode": (
                "frozen-writers-held"
                if args.leave_writers_frozen
                else "frozen-writers"
                if args.freeze_writers
                else "read-only"
            ),
            "freezeRequested": args.freeze_writers,
            "leaveWritersFrozenRequested": args.leave_writers_frozen,
            "artifacts": artifact_evidence,
        }
        write_json(manifest, payload)
        manifest_sha = write_manifest_sha(manifest)
    except Exception as exc:
        failure = exc
    finally:
        if snapshot_image:
            try:
                remove_snapshot(transport, target, snapshot_image)
                temporary_snapshot_removed = True
            except Exception as cleanup_error:
                failure = RuntimeError(
                    f"{failure}; temporary snapshot cleanup failed: {cleanup_error}"
                    if failure
                    else f"temporary snapshot cleanup failed: {cleanup_error}"
                )
        if (
            args.freeze_writers
            and writers_before
            and (failure is not None or not args.leave_writers_frozen)
        ):
            try:
                writers_restarted = restart_writers(transport, target, writers_before)
            except Exception as restart_error:
                failure = RuntimeError(
                    f"{failure}; writer restart failed: {restart_error}"
                    if failure
                    else f"writer restart failed: {restart_error}"
                )
        elif args.leave_writers_frozen and writers_before and failure is None:
            try:
                unexpectedly_running = sorted(
                    set(running_writers(transport, target)).intersection(writers_before)
                )
                if unexpectedly_running:
                    raise RuntimeError(
                        "legacy writers did not remain frozen: "
                        + ", ".join(unexpectedly_running)
                    )
                writers_frozen_at_completion = True
            except Exception as freeze_error:
                failure = freeze_error
                try:
                    writers_restarted = restart_writers(transport, target, writers_before)
                except Exception as restart_error:
                    failure = RuntimeError(
                        f"{failure}; writer restart failed: {restart_error}"
                    )
        capture_evidence(
            operation_evidence,
            backup_identifier=backup_identifier,
            status="failed" if failure else "success",
            source_host=args.host,
            source_user=args.user,
            freeze_requested=args.freeze_writers,
            leave_writers_frozen_requested=args.leave_writers_frozen,
            writers_before=writers_before,
            writers_restarted=writers_restarted,
            writers_frozen_at_completion=writers_frozen_at_completion,
            temporary_snapshot_removed=temporary_snapshot_removed,
            reason=str(failure)[:500] if failure else None,
        )
    if failure:
        raise failure
    assert manifest_sha is not None
    print(json.dumps({
        "status": "success",
        "backupId": backup_identifier,
        "artifactCount": len(artifact_evidence),
        "manifest": str(manifest),
        "manifestSha256": str(manifest_sha),
        "freezeRequested": args.freeze_writers,
        "writersFrozenAtCompletion": writers_frozen_at_completion,
        "writerRecoveryComplete": (
            not args.freeze_writers
            or (not writers_frozen_at_completion and writers_restarted == writers_before)
        ),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
