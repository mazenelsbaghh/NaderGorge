#!/usr/bin/env python3
"""Build the reviewed encrypted cutover candidate from validated local staging."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import io
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tarfile
import uuid
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import BinaryIO, Iterable


CONTAINER = "massar-legacy-stage-166"
DATABASE = "massar_platform"
MIGRATIONS_PATH = Path("backend/src/NaderGorge.Infrastructure/Migrations")
MIGRATION_ATTRIBUTE = re.compile(r'\[Migration\("([^"]+)"\)\]')
ALLOWED_AREAS = frozenset({"public", "protected", "private", "live-support"})
MAX_FILE_BYTES = 2 * 1024**3
MAX_ARCHIVE_BYTES = 20 * 1024**3
SHA256 = re.compile(r"^[0-9a-f]{64}$")
RESTORE_ID = re.compile(r"^legacy-restore-[0-9a-f]{32}$")
SOURCE_ARTIFACTS = frozenset({"database", "assets", "protected", "appData"})


class CandidateBuildError(RuntimeError):
    """Raised when local staging cannot produce a provably safe candidate."""


@dataclass(frozen=True)
class SourceFile:
    source: Path
    archive_path: str
    area: str
    relative_path: str
    size: int
    sha256: str
    device: int
    inode: int
    modified_ns: int

    def manifest_entry(self) -> dict[str, object]:
        return {
            "archivePath": self.archive_path,
            "area": self.area,
            "relativePath": self.relative_path,
            "size": self.size,
            "sha256": self.sha256,
        }


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_relative_path(value: str) -> str:
    if not value or "\\" in value or "\x00" in value:
        raise CandidateBuildError("file paths must be non-empty normalized POSIX paths")
    path = PurePosixPath(value)
    if path.is_absolute() or any(part in {"", ".", ".."} for part in path.parts):
        raise CandidateBuildError(f"unsafe file path: {value}")
    if path.as_posix() != value:
        raise CandidateBuildError(f"file path is not normalized: {value}")
    return value


def validate_passphrase(path: Path, output: Path) -> Path:
    expanded = path.expanduser()
    if expanded.is_symlink():
        raise CandidateBuildError("passphrase file must not be a symlink")
    resolved = expanded.resolve()
    if not resolved.is_file() or stat.S_IMODE(resolved.stat().st_mode) != 0o600:
        raise CandidateBuildError("passphrase must be a mode-0600 regular file")
    if len(resolved.read_bytes().strip()) < 32:
        raise CandidateBuildError("passphrase must contain at least 32 bytes")
    try:
        resolved.relative_to(output)
    except ValueError:
        return resolved
    raise CandidateBuildError("passphrase must be stored outside the candidate bundle")


def table_counts_sha256(table_counts: dict[str, int]) -> str:
    canonical = json.dumps(
        table_counts,
        sort_keys=True,
        separators=(",", ":"),
    ).encode()
    return hashlib.sha256(canonical).hexdigest()


def load_validation_evidence(path: Path) -> dict[str, object]:
    expanded = path.expanduser()
    if expanded.is_symlink() or not expanded.is_file():
        raise CandidateBuildError("staging validation evidence must be a regular file")
    try:
        value = json.loads(expanded.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise CandidateBuildError("staging validation evidence is invalid") from exc
    file_references = value.get("fileReferences")
    reset_counts = value.get("resetTableCounts")
    migration_ids = value.get("migrationIds")
    table_counts = value.get("tableCounts")
    counts_digest = value.get("tableCountsSha256")
    source_capture = value.get("sourceCapture")
    if (
        value.get("schemaVersion") != 1
        or value.get("status") != "success"
        or value.get("isolated") is not True
        or value.get("migrationModelMatch") is not True
        or value.get("criticalFindingCount") != 0
        or value.get("userWithoutRoleCount") != 0
        or value.get("unsupportedProviderCount") != 0
        or not isinstance(value.get("backupId"), str)
        or not isinstance(value.get("restoreId"), str)
        or not RESTORE_ID.fullmatch(str(value.get("restoreId")))
        or not isinstance(value.get("restoreEvidenceSha256"), str)
        or not SHA256.fullmatch(str(value.get("restoreEvidenceSha256")))
        or not isinstance(source_capture, dict)
        or source_capture.get("backupId") != value.get("backupId")
        or not isinstance(source_capture.get("sourceHost"), str)
        or not source_capture.get("sourceHost")
        or not isinstance(source_capture.get("sourceUser"), str)
        or not source_capture.get("sourceUser")
        or source_capture.get("sourceMode")
        not in {"read-only", "frozen-writers", "frozen-writers-held"}
        or source_capture.get("authoritativeSource") is not (
            source_capture.get("sourceMode") == "frozen-writers-held"
        )
        or source_capture.get("writersFrozenAtCompletion")
        is not source_capture.get("authoritativeSource")
        or not isinstance(source_capture.get("manifestSha256"), str)
        or not SHA256.fullmatch(str(source_capture.get("manifestSha256")))
        or not isinstance(source_capture.get("captureEvidenceSha256"), str)
        or not SHA256.fullmatch(str(source_capture.get("captureEvidenceSha256")))
        or not isinstance(source_capture.get("artifactSha256"), dict)
        or set(source_capture["artifactSha256"]) != SOURCE_ARTIFACTS
        or any(
            not isinstance(digest, str) or not SHA256.fullmatch(digest)
            for digest in source_capture["artifactSha256"].values()
        )
        or not isinstance(file_references, dict)
        or file_references.get("missingUnblockedReferences") != 0
        or not isinstance(reset_counts, dict)
        or any(not isinstance(count, int) or count != 0 for count in reset_counts.values())
        or not isinstance(migration_ids, list)
        or not migration_ids
        or migration_ids != sorted(set(migration_ids))
        or any(not isinstance(identifier, str) or not identifier for identifier in migration_ids)
        or not isinstance(table_counts, dict)
        or "__EFMigrationsHistory" not in table_counts
        or any(
            not isinstance(name, str)
            or isinstance(count, bool)
            or not isinstance(count, int)
            or count < 0
            for name, count in table_counts.items()
        )
        or not isinstance(counts_digest, str)
        or not SHA256.fullmatch(counts_digest)
        or counts_digest != table_counts_sha256(table_counts)
        or value.get("userCount") != table_counts.get("users")
        or isinstance(value.get("stagingFileCount"), bool)
        or not isinstance(value.get("stagingFileCount"), int)
        or value.get("stagingFileCount") < 0
        or not isinstance(value.get("stagingFileTreeSha256"), str)
        or not SHA256.fullmatch(str(value.get("stagingFileTreeSha256")))
    ):
        raise CandidateBuildError(
            "staging validation evidence does not prove a clean isolated candidate"
        )
    return value


def load_restore_evidence(
    path: Path,
    validation: dict[str, object],
) -> dict[str, object]:
    expanded = path.expanduser()
    if expanded.is_symlink() or not expanded.is_file():
        raise CandidateBuildError("restore evidence must be a regular file")
    try:
        value = json.loads(expanded.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise CandidateBuildError("restore evidence is invalid") from exc
    if (
        sha256_file(expanded) != validation["restoreEvidenceSha256"]
        or value.get("schemaVersion") != 1
        or value.get("status") != "success"
        or value.get("isolated") is not True
        or value.get("backupId") != validation["backupId"]
        or value.get("restoreId") != validation["restoreId"]
        or value.get("sourceCapture") != validation["sourceCapture"]
    ):
        raise CandidateBuildError(
            "restore evidence does not match staging validation provenance"
        )
    return value


def verify_source_capture(
    backup: Path,
    validation: dict[str, object],
    *,
    authoritative_final: bool,
) -> None:
    source = validation["sourceCapture"]
    assert isinstance(source, dict)
    manifest = backup / "manifest.json"
    capture = backup / "capture-evidence.json"
    if (
        manifest.is_symlink()
        or capture.is_symlink()
        or not manifest.is_file()
        or not capture.is_file()
        or sha256_file(manifest) != source["manifestSha256"]
        or sha256_file(capture) != source["captureEvidenceSha256"]
    ):
        raise CandidateBuildError(
            "source capture digests do not match staging validation provenance"
        )
    try:
        manifest_value = json.loads(manifest.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise CandidateBuildError("source capture manifest is invalid") from exc
    entries = manifest_value.get("artifacts")
    if (
        manifest_value.get("backupId") != validation["backupId"]
        or not isinstance(entries, dict)
        or {
            name: entry.get("sha256")
            for name, entry in entries.items()
            if isinstance(entry, dict)
        }
        != source["artifactSha256"]
    ):
        raise CandidateBuildError(
            "source artifact digests do not match staging validation provenance"
        )
    if authoritative_final and source.get("authoritativeSource") is not True:
        raise CandidateBuildError(
            "authoritative final candidate requires writers frozen at source capture completion"
        )


def staging_file_tree_snapshot(backup: Path) -> tuple[int, str]:
    entries: list[dict[str, object]] = []
    roots = (
        ("assets", backup / "staging-files-assets"),
        ("protected", backup / "staging-files-protected"),
        ("appData", backup / "staging-files-app-data"),
    )
    for area, root in roots:
        if root.is_symlink() or not root.is_dir():
            raise CandidateBuildError(f"validated staging root is missing: {root.name}")
        for path in sorted(root.rglob("*")):
            if path.is_symlink():
                raise CandidateBuildError("staging file tree contains a symlink")
            if path.is_dir():
                continue
            if not path.is_file():
                raise CandidateBuildError(
                    "staging file tree contains a non-regular file"
                )
            entries.append({
                "area": area,
                "path": path.relative_to(root).as_posix(),
                "bytes": path.stat().st_size,
                "sha256": sha256_file(path),
            })
    canonical = json.dumps(
        entries,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode()
    return len(entries), hashlib.sha256(canonical).hexdigest()


def require_unchanged_staging_files(
    backup: Path,
    validation: dict[str, object],
) -> None:
    file_count, file_digest = staging_file_tree_snapshot(backup)
    if (
        file_count != validation["stagingFileCount"]
        or file_digest != validation["stagingFileTreeSha256"]
    ):
        raise CandidateBuildError(
            "staging files changed after validation; fresh evidence is required"
        )


def repository_migration_ids(repository: Path) -> tuple[str, ...]:
    migrations = repository.resolve() / MIGRATIONS_PATH
    if not migrations.is_dir() or migrations.is_symlink():
        raise CandidateBuildError("repository migrations directory is missing or unsafe")
    identifiers: set[str] = set()
    for path in migrations.glob("*.cs"):
        if path.is_symlink() or not path.is_file():
            raise CandidateBuildError("migration sources must be regular files")
        match = MIGRATION_ATTRIBUTE.search(path.read_text(encoding="utf-8"))
        if match:
            identifiers.add(match.group(1))
    ordered = tuple(sorted(identifiers))
    if not ordered:
        raise CandidateBuildError("repository contains no attributed EF migrations")
    return ordered


def run_checked(argv: list[str], *, input_bytes: bytes | None = None) -> str:
    completed = subprocess.run(
        argv,
        input=input_bytes,
        capture_output=True,
        check=False,
    )
    if completed.returncode:
        error = completed.stderr.decode(errors="replace").strip()
        raise CandidateBuildError(error[:1000] or f"{argv[0]} failed")
    return completed.stdout.decode(errors="strict").strip()


def staging_database_snapshot() -> tuple[tuple[str, ...], dict[str, int]]:
    migration_output = run_checked([
        "docker", "exec", CONTAINER,
        "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
        "-U", "postgres", "-d", DATABASE,
        "-c", 'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";',
    ])
    counts_output = run_checked([
        "docker", "exec", CONTAINER,
        "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
        "-U", "postgres", "-d", DATABASE,
        "-c",
        (
            "select coalesce(json_object_agg(name,count order by name),'{}'::json) "
            "from (select c.relname name,"
            "(xpath('/row/count/text()',query_to_xml(format("
            "'select count(*) as count from %I',c.relname),false,true,''"
            ")))[1]::text::bigint count "
            "from pg_class c where c.relnamespace='public'::regnamespace "
            "and c.relkind in ('r','p')) value;"
        ),
    ])
    try:
        counts_value = json.loads(counts_output)
    except json.JSONDecodeError as exc:
        raise CandidateBuildError("staging table-count snapshot is invalid") from exc
    if (
        not isinstance(counts_value, dict)
        or "__EFMigrationsHistory" not in counts_value
        or any(
            not isinstance(name, str)
            or isinstance(count, bool)
            or not isinstance(count, int)
            or count < 0
            for name, count in counts_value.items()
        )
    ):
        raise CandidateBuildError("staging table-count snapshot is incomplete")
    migrations = tuple(line for line in migration_output.splitlines() if line)
    if not migrations or migrations != tuple(sorted(set(migrations))):
        raise CandidateBuildError("staging migration history is not ordered and unique")
    return migrations, dict(sorted(counts_value.items()))


def map_destination(root_name: str, relative: str) -> tuple[str, str]:
    safe_relative_path(relative)
    if root_name == "staging-files-assets":
        prefix = "protected/resources/"
        return (
            ("protected", relative[len("protected/"):])
            if relative.startswith(prefix)
            else ("public", relative)
        )
    if root_name == "staging-files-protected":
        return "protected", safe_relative_path(f"resources/{relative}")
    if root_name != "staging-files-app-data":
        raise CandidateBuildError(f"unsupported staging root: {root_name}")
    first, separator, remainder = relative.partition("/")
    if first in {"protected", "private", "live-support"} and separator and remainder:
        return first, remainder
    if first in {"subtitles", "mindmaps"}:
        return "public", relative
    raise CandidateBuildError(f"unmapped App_Data path: {relative}")


def regular_files(root: Path) -> Iterable[Path]:
    for directory, directory_names, file_names in os.walk(root, followlinks=False):
        base = Path(directory)
        for name in tuple(directory_names):
            candidate = base / name
            if candidate.is_symlink():
                raise CandidateBuildError(f"staging tree contains a symlink: {candidate}")
        for name in file_names:
            candidate = base / name
            if candidate.is_symlink():
                raise CandidateBuildError(f"staging tree contains a symlink: {candidate}")
            if not candidate.is_file():
                raise CandidateBuildError(
                    f"staging tree contains a non-regular file: {candidate}"
                )
            yield candidate


def inspect_source_file(source: Path, area: str, relative: str) -> SourceFile:
    relative = safe_relative_path(relative)
    archive_path = safe_relative_path(f"{area}/{relative}")
    before = source.stat(follow_symlinks=False)
    if not stat.S_ISREG(before.st_mode):
        raise CandidateBuildError(f"candidate source is not a regular file: {source}")
    if before.st_size > MAX_FILE_BYTES:
        raise CandidateBuildError(f"candidate file exceeds the 2 GiB bound: {source}")
    digest = sha256_file(source)
    after = source.stat(follow_symlinks=False)
    identity_before = (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns)
    identity_after = (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)
    if identity_before != identity_after:
        raise CandidateBuildError(f"candidate source changed while hashing: {source}")
    return SourceFile(
        source=source,
        archive_path=archive_path,
        area=area,
        relative_path=relative,
        size=before.st_size,
        sha256=digest,
        device=before.st_dev,
        inode=before.st_ino,
        modified_ns=before.st_mtime_ns,
    )


def collect_source_files(backup: Path) -> tuple[SourceFile, ...]:
    if backup.is_symlink() or not backup.is_dir():
        raise CandidateBuildError("staging backup directory is missing or unsafe")
    roots = tuple(
        backup / name
        for name in (
            "staging-files-assets",
            "staging-files-protected",
            "staging-files-app-data",
        )
    )
    for root in roots:
        if root.is_symlink() or not root.is_dir():
            raise CandidateBuildError(f"validated staging root is missing: {root.name}")
    destinations: dict[tuple[str, str], SourceFile] = {}
    for root in roots:
        for source in regular_files(root):
            relative = source.relative_to(root).as_posix()
            area, destination = map_destination(root.name, relative)
            entry = inspect_source_file(source, area, destination)
            key = (entry.area, entry.relative_path)
            existing = destinations.get(key)
            if existing is None:
                destinations[key] = entry
            elif (existing.size, existing.sha256) != (entry.size, entry.sha256):
                raise CandidateBuildError(
                    f"conflicting staging files map to {entry.area}/{entry.relative_path}"
                )
    ordered = tuple(sorted(destinations.values(), key=lambda item: item.archive_path))
    if sum(item.size for item in ordered) > MAX_ARCHIVE_BYTES:
        raise CandidateBuildError("candidate files exceed the 20 GiB archive bound")
    return ordered


class HashingReader:
    """Read one previously inspected regular file and prove it stayed unchanged."""

    def __init__(self, entry: SourceFile) -> None:
        flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
        descriptor = os.open(entry.source, flags)
        metadata = os.fstat(descriptor)
        identity = (
            metadata.st_dev,
            metadata.st_ino,
            metadata.st_size,
            metadata.st_mtime_ns,
        )
        expected = (entry.device, entry.inode, entry.size, entry.modified_ns)
        if not stat.S_ISREG(metadata.st_mode) or identity != expected:
            os.close(descriptor)
            raise CandidateBuildError(f"candidate source changed before archive: {entry.source}")
        self._stream = os.fdopen(descriptor, "rb")
        self._entry = entry
        self._digest = hashlib.sha256()
        self._bytes_read = 0

    def read(self, size: int = -1) -> bytes:
        chunk = self._stream.read(size)
        self._digest.update(chunk)
        self._bytes_read += len(chunk)
        return chunk

    def verify_and_close(self) -> None:
        metadata = os.fstat(self._stream.fileno())
        self._stream.close()
        identity = (
            metadata.st_dev,
            metadata.st_ino,
            metadata.st_size,
            metadata.st_mtime_ns,
        )
        expected = (
            self._entry.device,
            self._entry.inode,
            self._entry.size,
            self._entry.modified_ns,
        )
        if (
            identity != expected
            or self._bytes_read != self._entry.size
            or self._digest.hexdigest() != self._entry.sha256
        ):
            raise CandidateBuildError(
                f"candidate source changed while archiving: {self._entry.source}"
            )


def write_tar_stream(output: BinaryIO, entries: Iterable[SourceFile]) -> None:
    with tarfile.open(fileobj=output, mode="w|", format=tarfile.PAX_FORMAT) as archive:
        for entry in entries:
            info = tarfile.TarInfo(entry.archive_path)
            info.size = entry.size
            info.mode = 0o644 if entry.area == "public" else 0o640
            info.uid = 0
            info.gid = 0
            info.uname = ""
            info.gname = ""
            info.mtime = 0
            reader = HashingReader(entry)
            try:
                archive.addfile(info, reader)
                reader.verify_and_close()
            except Exception:
                reader._stream.close()
                raise


def gpg_encrypt_argv(passphrase: Path, output: Path) -> list[str]:
    return [
        "gpg", "--batch", "--yes", "--quiet", "--symmetric",
        "--cipher-algo", "AES256", "--pinentry-mode", "loopback",
        "--passphrase-file", str(passphrase), "--output", str(output),
    ]


def encrypt_database_dump(destination: Path, passphrase: Path) -> None:
    dump = subprocess.Popen(
        [
            "docker", "exec", CONTAINER,
            "pg_dump", "-U", "postgres", "-d", DATABASE,
            "-Fc", "--no-owner", "--no-acl", "--serializable-deferrable",
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    assert dump.stdout is not None
    encrypt = subprocess.run(
        gpg_encrypt_argv(passphrase, destination),
        stdin=dump.stdout,
        capture_output=True,
        check=False,
    )
    dump.stdout.close()
    dump_error = dump.stderr.read().decode(errors="replace") if dump.stderr else ""
    dump_code = dump.wait()
    if dump_code or encrypt.returncode:
        destination.unlink(missing_ok=True)
        error = dump_error or encrypt.stderr.decode(errors="replace")
        raise CandidateBuildError(error[:1000] or "encrypted database dump failed")


def encrypt_file_archive(
    destination: Path,
    passphrase: Path,
    entries: tuple[SourceFile, ...],
) -> None:
    encrypt = subprocess.Popen(
        gpg_encrypt_argv(passphrase, destination),
        stdin=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )
    assert encrypt.stdin is not None
    try:
        write_tar_stream(encrypt.stdin, entries)
        encrypt.stdin.close()
        error = encrypt.stderr.read().decode(errors="replace") if encrypt.stderr else ""
        code = encrypt.wait()
    except Exception:
        encrypt.kill()
        encrypt.wait()
        destination.unlink(missing_ok=True)
        raise
    if code:
        destination.unlink(missing_ok=True)
        raise CandidateBuildError(error[:1000] or "encrypted file archive failed")


def decrypt_process(path: Path, passphrase: Path) -> subprocess.Popen:
    return subprocess.Popen(
        [
            "gpg", "--batch", "--quiet", "--decrypt",
            "--pinentry-mode", "loopback",
            "--passphrase-file", str(passphrase), str(path),
        ],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def verify_database_dump(path: Path, passphrase: Path) -> None:
    decrypt = decrypt_process(path, passphrase)
    assert decrypt.stdout is not None
    inspect = subprocess.run(
        ["docker", "run", "--rm", "-i", "postgres:16-alpine", "pg_restore", "--list"],
        stdin=decrypt.stdout,
        capture_output=True,
        check=False,
    )
    decrypt.stdout.close()
    error = decrypt.stderr.read().decode(errors="replace") if decrypt.stderr else ""
    decrypt_code = decrypt.wait()
    if decrypt_code or inspect.returncode:
        details = error or inspect.stderr.decode(errors="replace")
        raise CandidateBuildError(details[:1000] or "encrypted database dump verification failed")


def verify_file_archive(
    path: Path,
    passphrase: Path,
    expected: tuple[SourceFile, ...],
) -> None:
    entries = {item.archive_path: item for item in expected}
    seen: set[str] = set()
    decrypt = decrypt_process(path, passphrase)
    assert decrypt.stdout is not None
    try:
        with tarfile.open(fileobj=decrypt.stdout, mode="r|*") as archive:
            for member in archive:
                name = safe_relative_path(member.name)
                if not member.isfile() or name not in entries or name in seen:
                    raise CandidateBuildError("encrypted archive member set or type is invalid")
                expected_entry = entries[name]
                if member.size != expected_entry.size:
                    raise CandidateBuildError("encrypted archive member size is invalid")
                stream = archive.extractfile(member)
                if stream is None:
                    raise CandidateBuildError("encrypted archive member is unreadable")
                digest = hashlib.sha256()
                size = 0
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    size += len(chunk)
                    digest.update(chunk)
                if size != expected_entry.size or digest.hexdigest() != expected_entry.sha256:
                    raise CandidateBuildError("encrypted archive member checksum is invalid")
                seen.add(name)
    finally:
        decrypt.stdout.close()
    error = decrypt.stderr.read().decode(errors="replace") if decrypt.stderr else ""
    decrypt_code = decrypt.wait()
    if decrypt_code or seen != set(entries):
        raise CandidateBuildError(error[:1000] or "encrypted archive set is incomplete")


def write_manifest(
    path: Path,
    *,
    backup_id: str,
    final_output: Path,
    dump: Path,
    archive: Path,
    migrations: tuple[str, ...],
    table_counts: dict[str, int],
    files: tuple[SourceFile, ...],
    validation: dict[str, object],
    validation_sha256: str,
    authoritative_final: bool,
) -> None:
    payload = {
        "schemaVersion": 2,
        "status": "success",
        "backupId": backup_id,
        "candidateMode": "authoritative-final" if authoritative_final else "rehearsal",
        "eligibleForCutover": authoritative_final,
        "sourceCapture": validation["sourceCapture"],
        "sourceBackupId": validation["backupId"],
        "restoreId": validation["restoreId"],
        "restoreEvidenceSha256": validation["restoreEvidenceSha256"],
        "validationEvidenceSha256": validation_sha256,
        "candidateDump": {
            "path": str(final_output / dump.name),
            "sha256": sha256_file(dump),
        },
        "fileArchive": {
            "path": str(final_output / archive.name),
            "sha256": sha256_file(archive),
        },
        "migrationIds": list(migrations),
        "tableCounts": table_counts,
        "files": [entry.manifest_entry() for entry in files],
    }
    path.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    path.chmod(0o600)


def build_bundle(
    *,
    backup: Path,
    repository: Path,
    validation_evidence: Path,
    restore_evidence: Path,
    passphrase: Path,
    output: Path,
    now: dt.datetime,
    authoritative_final: bool = False,
) -> dict[str, object]:
    if output.exists() or output.is_symlink():
        raise CandidateBuildError("candidate output directory already exists")
    if not output.parent.is_dir() or output.parent.is_symlink():
        raise CandidateBuildError("candidate output parent is missing or unsafe")
    validation = load_validation_evidence(validation_evidence)
    load_restore_evidence(restore_evidence, validation)
    verify_source_capture(
        backup,
        validation,
        authoritative_final=authoritative_final,
    )
    expected_migrations = repository_migration_ids(repository)
    actual_migrations, table_counts = staging_database_snapshot()
    if actual_migrations != expected_migrations:
        raise CandidateBuildError(
            "staging migration history does not exactly match repository migrations"
        )
    if (
        list(actual_migrations) != validation["migrationIds"]
        or table_counts != validation["tableCounts"]
        or table_counts_sha256(table_counts) != validation["tableCountsSha256"]
    ):
        raise CandidateBuildError(
            "staging database changed after validation; fresh evidence is required"
        )
    require_unchanged_staging_files(backup, validation)
    files = collect_source_files(backup)
    temporary = output.parent / f".{output.name}.{uuid.uuid4().hex}.tmp"
    temporary.mkdir(mode=0o700)
    try:
        dump = temporary / "candidate.dump.gpg"
        archive = temporary / "files.tar.gpg"
        encrypt_database_dump(dump, passphrase)
        encrypt_file_archive(archive, passphrase, files)
        dump.chmod(0o600)
        archive.chmod(0o600)
        verify_database_dump(dump, passphrase)
        verify_file_archive(archive, passphrase, files)
        dump_digest = sha256_file(dump)
        timestamp = now.astimezone(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        backup_id = f"legacy-candidate-{timestamp}-{dump_digest[:12]}"
        manifest = temporary / "manifest.json"
        write_manifest(
            manifest,
            backup_id=backup_id,
            final_output=output,
            dump=dump,
            archive=archive,
            migrations=actual_migrations,
            table_counts=table_counts,
            files=files,
            validation=validation,
            validation_sha256=sha256_file(validation_evidence),
            authoritative_final=authoritative_final,
        )
        os.rename(temporary, output)
    except Exception:
        shutil.rmtree(temporary, ignore_errors=True)
        raise
    return {
        "status": "success",
        "backupId": backup_id,
        "manifest": str(output / "manifest.json"),
        "migrationCount": len(actual_migrations),
        "tableCount": len(table_counts),
        "fileCount": len(files),
        "fileBytes": sum(entry.size for entry in files),
        "candidateMode": "authoritative-final" if authoritative_final else "rehearsal",
        "eligibleForCutover": authoritative_final,
        "plaintextArtifactsWritten": False,
        "sshAttempted": False,
    }


def dry_run(
    *,
    backup: Path,
    repository: Path,
    validation_evidence: Path,
    restore_evidence: Path,
    output: Path,
    authoritative_final: bool = False,
) -> dict[str, object]:
    if output.exists() or output.is_symlink():
        raise CandidateBuildError("candidate output directory already exists")
    validation = load_validation_evidence(validation_evidence)
    load_restore_evidence(restore_evidence, validation)
    verify_source_capture(
        backup,
        validation,
        authoritative_final=authoritative_final,
    )
    require_unchanged_staging_files(backup, validation)
    migrations = repository_migration_ids(repository)
    if list(migrations) != validation["migrationIds"]:
        raise CandidateBuildError(
            "staging validation migrations do not exactly match the repository"
        )
    files = collect_source_files(backup)
    return {
        "status": "dry-run",
        "plannedOutput": str(output),
        "repositoryMigrationCount": len(migrations),
        "fileCount": len(files),
        "fileBytes": sum(entry.size for entry in files),
        "candidateMode": "authoritative-final" if authoritative_final else "rehearsal",
        "eligibleForCutover": authoritative_final,
        "dockerAttempted": False,
        "gpgAttempted": False,
        "sshAttempted": False,
        "plaintextArtifactsWritten": False,
    }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build an encrypted cutover bundle from validated local staging."
    )
    parser.add_argument("--backup-dir", required=True, type=Path)
    parser.add_argument("--validation-evidence", required=True, type=Path)
    parser.add_argument("--restore-evidence", required=True, type=Path)
    parser.add_argument("--repository", required=True, type=Path)
    parser.add_argument("--passphrase-file", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    parser.add_argument(
        "--authoritative-final",
        action="store_true",
        help="Require a source capture whose writers remained frozen at completion.",
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    output = args.output_dir.expanduser().resolve()
    try:
        passphrase = validate_passphrase(args.passphrase_file, output)
        if args.dry_run:
            result = dry_run(
                backup=args.backup_dir.expanduser().resolve(),
                repository=args.repository.expanduser().resolve(),
                validation_evidence=args.validation_evidence.expanduser().resolve(),
                restore_evidence=args.restore_evidence.expanduser().resolve(),
                output=output,
                authoritative_final=args.authoritative_final,
            )
        else:
            result = build_bundle(
                backup=args.backup_dir.expanduser().resolve(),
                repository=args.repository.expanduser().resolve(),
                validation_evidence=args.validation_evidence.expanduser().resolve(),
                restore_evidence=args.restore_evidence.expanduser().resolve(),
                passphrase=passphrase,
                output=output,
                now=utc_now(),
                authoritative_final=args.authoritative_final,
            )
    except (CandidateBuildError, OSError) as exc:
        print(json.dumps({"status": "failed", "error": str(exc)}), file=sys.stderr)
        return 2
    print(json.dumps(result, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
