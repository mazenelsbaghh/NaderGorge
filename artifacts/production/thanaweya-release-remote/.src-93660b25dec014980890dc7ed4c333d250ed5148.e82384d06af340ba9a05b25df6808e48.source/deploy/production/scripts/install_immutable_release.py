#!/usr/bin/env python3
"""Root-only publisher for verified immutable Massar release artifacts."""

from __future__ import annotations

import argparse
import fcntl
import hashlib
import json
import os
import pwd
import re
import shutil
import stat
import sys
import tarfile
from pathlib import Path, PurePosixPath


BASE = Path("/opt/massar/releases")
INCOMING = Path("/tmp")
CLUSTER_MARKER = Path("/etc/massar/cluster-id")
LOCK_FILE = Path("/run/massar-install-immutable-release.lock")
OPERATOR = "massar-ops"
RELEASE_RE = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
MAXIMUM_MANIFEST_BYTES = 1024 * 1024
MAXIMUM_BUNDLE_BYTES = 64 * 1024 * 1024
MAXIMUM_BUNDLE_FILES = 20_000
MAXIMUM_EXTRACTED_BYTES = 256 * 1024 * 1024


class ReleaseInstallError(RuntimeError):
    pass


def operator_uid() -> int:
    return pwd.getpwnam(OPERATOR).pw_uid


def validate_identity(release_id: str, *digests: str) -> None:
    if os.geteuid() != 0:
        raise ReleaseInstallError("helper must run as root")
    if not RELEASE_RE.fullmatch(release_id):
        raise ReleaseInstallError("release identity is invalid")
    if any(not SHA256_RE.fullmatch(value) for value in digests):
        raise ReleaseInstallError("release digest is invalid")
    if CLUSTER_MARKER.read_text(encoding="ascii").strip() != "massar-production":
        raise ReleaseInstallError("cluster marker does not identify Massar Production")
    for fixed in (BASE.parent.parent, BASE.parent, BASE):
        info = os.lstat(fixed)
        if stat.S_ISLNK(info.st_mode) or not stat.S_ISDIR(info.st_mode):
            raise ReleaseInstallError("fixed release parent is not a real directory")


def open_operator_file(path: Path, maximum_bytes: int) -> int:
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        info = os.fstat(descriptor)
        if (
            not stat.S_ISREG(info.st_mode)
            or info.st_uid != operator_uid()
            or info.st_size <= 0
            or info.st_size > maximum_bytes
        ):
            raise ReleaseInstallError("incoming artifact is not a bounded operator file")
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


def descriptor_sha256(descriptor: int) -> str:
    digest = hashlib.sha256()
    os.lseek(descriptor, 0, os.SEEK_SET)
    while chunk := os.read(descriptor, 1024 * 1024):
        digest.update(chunk)
    os.lseek(descriptor, 0, os.SEEK_SET)
    return digest.hexdigest()


def descriptor_bytes(descriptor: int, maximum_bytes: int) -> bytes:
    content = b""
    os.lseek(descriptor, 0, os.SEEK_SET)
    while len(content) <= maximum_bytes:
        chunk = os.read(descriptor, min(65536, maximum_bytes + 1 - len(content)))
        if not chunk:
            break
        content += chunk
    os.lseek(descriptor, 0, os.SEEK_SET)
    if not content or len(content) > maximum_bytes:
        raise ReleaseInstallError("incoming artifact exceeds its size contract")
    return content


def validate_manifest(content: bytes, release_id: str) -> dict[str, object]:
    try:
        value = json.loads(content.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ReleaseInstallError("release manifest JSON is invalid") from exc
    if (
        not isinstance(value, dict)
        or value.get("schemaVersion") != 1
        or value.get("status") != "success"
        or value.get("releaseId") != release_id
        or not isinstance(value.get("images"), dict)
        or set(value["images"]) != {"backend", "frontend", "worker", "migrator"}
        or any(
            not isinstance(digest, str)
            or not re.fullmatch(r"sha256:[0-9a-f]{64}", digest)
            for digest in value["images"].values()
        )
    ):
        raise ReleaseInstallError("release manifest identity is invalid")
    return value


def safe_member_path(member: tarfile.TarInfo) -> Path:
    pure = PurePosixPath(member.name)
    if (
        pure.is_absolute()
        or ".." in pure.parts
        or len(pure.parts) < 2
        or pure.parts[:2] != ("deploy", "production")
    ):
        raise ReleaseInstallError("release bundle contains an invalid path")
    if not (member.isdir() or member.isfile()):
        raise ReleaseInstallError("release bundle contains a non-regular member")
    return Path(*pure.parts)


def extract_bundle(bundle_descriptor: int, staging: Path) -> None:
    seen: set[Path] = set()
    total_bytes = 0
    file_count = 0
    with os.fdopen(os.dup(bundle_descriptor), "rb") as stream:
        with tarfile.open(fileobj=stream, mode="r:gz") as archive:
            for member in archive:
                relative = safe_member_path(member)
                if relative in seen:
                    raise ReleaseInstallError("release bundle contains a duplicate path")
                seen.add(relative)
                target = staging / relative
                if member.isdir():
                    target.mkdir(mode=0o755, parents=True, exist_ok=True)
                    continue
                file_count += 1
                total_bytes += member.size
                if (
                    file_count > MAXIMUM_BUNDLE_FILES
                    or total_bytes > MAXIMUM_EXTRACTED_BYTES
                    or member.size < 0
                ):
                    raise ReleaseInstallError("release bundle exceeds extraction limits")
                source = archive.extractfile(member)
                if source is None:
                    raise ReleaseInstallError("release bundle member cannot be read")
                target.parent.mkdir(mode=0o755, parents=True, exist_ok=True)
                mode = 0o755 if member.mode & 0o111 else 0o644
                descriptor = os.open(
                    target,
                    os.O_WRONLY
                    | os.O_CREAT
                    | os.O_EXCL
                    | getattr(os, "O_NOFOLLOW", 0),
                    mode,
                )
                written = 0
                try:
                    with os.fdopen(descriptor, "wb", closefd=True) as output:
                        while chunk := source.read(1024 * 1024):
                            written += len(chunk)
                            if written > member.size:
                                raise ReleaseInstallError(
                                    "release bundle member exceeds declared size"
                                )
                            output.write(chunk)
                        output.flush()
                        os.fsync(output.fileno())
                finally:
                    source.close()
                if written != member.size:
                    raise ReleaseInstallError("release bundle member is truncated")


def write_exclusive(path: Path, content: bytes, mode: int = 0o644) -> None:
    descriptor = os.open(
        path,
        os.O_WRONLY
        | os.O_CREAT
        | os.O_EXCL
        | getattr(os, "O_NOFOLLOW", 0),
        mode,
    )
    with os.fdopen(descriptor, "wb", closefd=True) as stream:
        stream.write(content)
        stream.flush()
        os.fsync(stream.fileno())


def lock() -> object:
    LOCK_FILE.parent.mkdir(mode=0o755, parents=True, exist_ok=True)
    stream = LOCK_FILE.open("a+", encoding="ascii")
    fcntl.flock(stream.fileno(), fcntl.LOCK_EX)
    return stream


def install_release(
    release_id: str,
    bundle_sha256: str,
    manifest_sha256: str,
) -> dict[str, object]:
    validate_identity(release_id, bundle_sha256, manifest_sha256)
    incoming = INCOMING / f"massar-{release_id}"
    incoming_info = os.lstat(incoming)
    if (
        stat.S_ISLNK(incoming_info.st_mode)
        or not stat.S_ISDIR(incoming_info.st_mode)
        or incoming_info.st_uid != operator_uid()
        or incoming_info.st_mode & 0o022
    ):
        raise ReleaseInstallError("incoming release root is unsafe")
    bundle = incoming / "release-files.tar.gz"
    manifest = incoming / "manifest.json"
    bundle_descriptor = open_operator_file(bundle, MAXIMUM_BUNDLE_BYTES)
    manifest_descriptor = open_operator_file(manifest, MAXIMUM_MANIFEST_BYTES)
    try:
        if descriptor_sha256(bundle_descriptor) != bundle_sha256:
            raise ReleaseInstallError("release bundle digest does not match")
        manifest_content = descriptor_bytes(
            manifest_descriptor, MAXIMUM_MANIFEST_BYTES
        )
        if hashlib.sha256(manifest_content).hexdigest() != manifest_sha256:
            raise ReleaseInstallError("release manifest digest does not match")
        validate_manifest(manifest_content, release_id)
        with lock():
            release_root = BASE / release_id
            staging = BASE / f".{release_id}.staging"
            if os.path.lexists(release_root):
                raise ReleaseInstallError("immutable release root already exists")
            if os.path.lexists(staging):
                info = os.lstat(staging)
                if stat.S_ISLNK(info.st_mode) or not stat.S_ISDIR(info.st_mode):
                    raise ReleaseInstallError("release staging path is unsafe")
                shutil.rmtree(staging)
            staging.mkdir(mode=0o755)
            created = True
            try:
                extract_bundle(bundle_descriptor, staging)
                if descriptor_sha256(bundle_descriptor) != bundle_sha256:
                    raise ReleaseInstallError(
                        "release bundle changed during extraction"
                    )
                if not (staging / "deploy/production/compose/compose.app.yml").is_file():
                    raise ReleaseInstallError("release bundle is incomplete")
                write_exclusive(staging / "manifest.json", manifest_content)
                write_exclusive(
                    staging / ".initial-manifest.sha256",
                    (manifest_sha256 + "\n").encode("ascii"),
                )
                write_exclusive(
                    staging / ".release-files.sha256",
                    (bundle_sha256 + "\n").encode("ascii"),
                )
                os.rename(staging, release_root)
                created = False
                directory_fd = os.open(BASE, os.O_RDONLY)
                try:
                    os.fsync(directory_fd)
                finally:
                    os.close(directory_fd)
            finally:
                if created and os.path.lexists(staging):
                    shutil.rmtree(staging)
    finally:
        os.close(bundle_descriptor)
        os.close(manifest_descriptor)
    return {
        "schemaVersion": 1,
        "status": "installed",
        "releaseId": release_id,
        "releaseFilesSha256": bundle_sha256,
        "manifestSha256": manifest_sha256,
    }


def regular_bytes(path: Path, maximum_bytes: int) -> bytes:
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        info = os.fstat(descriptor)
        if not stat.S_ISREG(info.st_mode) or info.st_size > maximum_bytes:
            raise ReleaseInstallError("release metadata is not a bounded regular file")
        return descriptor_bytes(descriptor, maximum_bytes)
    finally:
        os.close(descriptor)


def publish_final_manifest(
    release_id: str,
    manifest_sha256: str,
) -> dict[str, object]:
    validate_identity(release_id, manifest_sha256)
    incoming = INCOMING / f"massar-{release_id}-manifest.json"
    incoming_descriptor = open_operator_file(incoming, MAXIMUM_MANIFEST_BYTES)
    try:
        content = descriptor_bytes(incoming_descriptor, MAXIMUM_MANIFEST_BYTES)
        if hashlib.sha256(content).hexdigest() != manifest_sha256:
            raise ReleaseInstallError("final manifest digest does not match")
        value = validate_manifest(content, release_id)
        if value.get("digestParity") is not True or value.get("nodeCount") != 3:
            raise ReleaseInstallError("final manifest does not prove three-node parity")
        with lock():
            release_root = BASE / release_id
            info = os.lstat(release_root)
            if stat.S_ISLNK(info.st_mode) or not stat.S_ISDIR(info.st_mode):
                raise ReleaseInstallError("release root is not a real directory")
            manifest = release_root / "manifest.json"
            current = regular_bytes(manifest, MAXIMUM_MANIFEST_BYTES)
            current_sha256 = hashlib.sha256(current).hexdigest()
            initial_sha256 = regular_bytes(
                release_root / ".initial-manifest.sha256", 128
            ).decode("ascii").strip()
            if not SHA256_RE.fullmatch(initial_sha256):
                raise ReleaseInstallError("initial manifest marker is invalid")
            if current_sha256 == manifest_sha256:
                status = "verified"
            else:
                if current_sha256 != initial_sha256:
                    raise ReleaseInstallError(
                        "current manifest is neither initial nor requested final"
                    )
                temporary = release_root / ".manifest.json.next"
                if os.path.lexists(temporary):
                    info = os.lstat(temporary)
                    if stat.S_ISLNK(info.st_mode) or not stat.S_ISREG(info.st_mode):
                        raise ReleaseInstallError("manifest staging path is unsafe")
                    temporary.unlink()
                write_exclusive(temporary, content)
                os.replace(temporary, manifest)
                directory_fd = os.open(release_root, os.O_RDONLY)
                try:
                    os.fsync(directory_fd)
                finally:
                    os.close(directory_fd)
                status = "published"
    finally:
        os.close(incoming_descriptor)
    return {
        "schemaVersion": 1,
        "status": status,
        "releaseId": release_id,
        "manifestSha256": manifest_sha256,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    install = commands.add_parser("install-release")
    install.add_argument("release")
    install.add_argument("bundle_sha256")
    install.add_argument("manifest_sha256")
    publish = commands.add_parser("publish-final-manifest")
    publish.add_argument("release")
    publish.add_argument("manifest_sha256")
    args = parser.parse_args(argv)
    try:
        if args.command == "install-release":
            result = install_release(
                args.release, args.bundle_sha256, args.manifest_sha256
            )
        else:
            result = publish_final_manifest(args.release, args.manifest_sha256)
        print(json.dumps(result, separators=(",", ":")))
        return 0
    except (ReleaseInstallError, OSError, ValueError, tarfile.TarError) as exc:
        print(f"immutable release installation blocked: {exc}", file=sys.stderr)
        return 7


if __name__ == "__main__":
    raise SystemExit(main())
