#!/usr/bin/env python3
"""Root-only, fail-closed helper for normalizing /opt/massar/current."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import stat
import sys
from pathlib import Path


BASE = Path("/opt/massar/releases")
CURRENT = Path("/opt/massar/current")
CLUSTER_MARKER = Path("/etc/massar/cluster-id")
OPERATION_MARKERS = Path("/run/massar-current-normalization")
RELEASE = re.compile(
    r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$"
)
SHA256 = re.compile(r"^[0-9a-f]{64}$")
OPERATION = re.compile(r"^[0-9a-f]{32}$")
MAXIMUM_MANIFEST_BYTES = 4 * 1024 * 1024


class NormalizationError(RuntimeError):
    pass


def inspect_release(release_id: str, manifest_sha256: str) -> Path:
    if os.geteuid() != 0:
        raise NormalizationError("helper must run as root")
    if not RELEASE.fullmatch(release_id) or not SHA256.fullmatch(manifest_sha256):
        raise NormalizationError("release identity arguments are invalid")
    if CLUSTER_MARKER.read_text(encoding="ascii").strip() != "massar-production":
        raise NormalizationError("cluster marker does not identify Massar Production")
    for fixed in (BASE.parent.parent, BASE.parent, BASE):
        info = os.lstat(fixed)
        if stat.S_ISLNK(info.st_mode) or not stat.S_ISDIR(info.st_mode):
            raise NormalizationError("fixed release parent is not a real directory")
    release_root = BASE / release_id
    root_info = os.lstat(release_root)
    if stat.S_ISLNK(root_info.st_mode) or not stat.S_ISDIR(root_info.st_mode):
        raise NormalizationError("release root must be a real directory")
    manifest = release_root / "manifest.json"
    info = os.lstat(manifest)
    if (
        stat.S_ISLNK(info.st_mode)
        or not stat.S_ISREG(info.st_mode)
        or info.st_size <= 0
        or info.st_size > MAXIMUM_MANIFEST_BYTES
    ):
        raise NormalizationError("release manifest is not a bounded regular file")
    descriptor = os.open(manifest, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        content = b""
        while len(content) <= MAXIMUM_MANIFEST_BYTES:
            chunk = os.read(
                descriptor,
                min(65536, MAXIMUM_MANIFEST_BYTES + 1 - len(content)),
            )
            if not chunk:
                break
            content += chunk
    finally:
        os.close(descriptor)
    if (
        not content
        or len(content) > MAXIMUM_MANIFEST_BYTES
        or hashlib.sha256(content).hexdigest() != manifest_sha256
    ):
        raise NormalizationError("release manifest bytes do not match evidence")
    try:
        value = json.loads(content.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise NormalizationError("release manifest JSON is invalid") from exc
    if not isinstance(value, dict) or value.get("releaseId") != release_id:
        raise NormalizationError("release manifest identity is invalid")
    return release_root


def pointer_identity(release_root: Path) -> tuple[int, int]:
    info = os.lstat(CURRENT)
    if not stat.S_ISLNK(info.st_mode) or os.readlink(CURRENT) != str(release_root):
        raise NormalizationError("current pointer target is not exact")
    return info.st_dev, info.st_ino


def inspect_current() -> tuple[str, str, Path]:
    info = os.lstat(CURRENT)
    if not stat.S_ISLNK(info.st_mode):
        raise NormalizationError("current pointer is not a symbolic link")
    raw_target = os.readlink(CURRENT)
    release_root = Path(raw_target)
    if (
        not release_root.is_absolute()
        or release_root.parent != BASE
        or not RELEASE.fullmatch(release_root.name)
    ):
        raise NormalizationError("current pointer target is outside the release root")
    manifest = release_root / "manifest.json"
    manifest_info = os.lstat(manifest)
    if (
        stat.S_ISLNK(manifest_info.st_mode)
        or not stat.S_ISREG(manifest_info.st_mode)
        or manifest_info.st_size <= 0
        or manifest_info.st_size > MAXIMUM_MANIFEST_BYTES
    ):
        raise NormalizationError("current manifest is not a bounded regular file")
    content = manifest.read_bytes()
    digest = hashlib.sha256(content).hexdigest()
    inspect_release(release_root.name, digest)
    return release_root.name, digest, release_root


def switch(
    operation_id: str,
    release_id: str,
    manifest_sha256: str,
) -> dict[str, object]:
    if not OPERATION.fullmatch(operation_id):
        raise NormalizationError("operation ID is invalid")
    target = inspect_release(release_id, manifest_sha256)
    previous_release, previous_manifest_sha256, previous = inspect_current()
    if previous == target:
        return {
            "schemaVersion": 1,
            "status": "already-current",
            "releaseId": release_id,
            "target": str(target),
            "previousReleaseId": previous_release,
            "previousManifestSha256": previous_manifest_sha256,
        }
    temporary = CURRENT.parent / f".current-switch-{operation_id}"
    if os.path.lexists(temporary):
        raise NormalizationError("operation temporary pointer already exists")
    try:
        os.symlink(str(target), temporary)
        os.replace(temporary, CURRENT)
        directory_fd = os.open(CURRENT.parent, os.O_RDONLY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    finally:
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass
    pointer_identity(target)
    return {
        "schemaVersion": 1,
        "status": "switched",
        "releaseId": release_id,
        "target": str(target),
        "previousReleaseId": previous_release,
        "previousManifestSha256": previous_manifest_sha256,
    }


def preflight(release_id: str, manifest_sha256: str) -> dict[str, object]:
    release_root = inspect_release(release_id, manifest_sha256)
    if os.path.lexists(CURRENT):
        raise NormalizationError("current pointer already exists; normalization is refused")
    return {
        "schemaVersion": 1,
        "status": "ready",
        "releaseId": release_id,
        "releaseRoot": str(release_root),
        "manifestSha256": manifest_sha256,
        "currentAbsent": True,
    }


def apply(
    operation_id: str,
    release_id: str,
    manifest_sha256: str,
) -> dict[str, object]:
    if not OPERATION.fullmatch(operation_id):
        raise NormalizationError("operation ID is invalid")
    ready = preflight(release_id, manifest_sha256)
    release_root = Path(str(ready["releaseRoot"]))
    temporary = Path("/opt/massar") / f".current-normalize-{operation_id}"
    if os.path.lexists(temporary):
        raise NormalizationError("operation temporary pointer already exists")
    OPERATION_MARKERS.mkdir(mode=0o700, parents=True, exist_ok=True)
    marker_directory_info = os.lstat(OPERATION_MARKERS)
    if (
        stat.S_ISLNK(marker_directory_info.st_mode)
        or not stat.S_ISDIR(marker_directory_info.st_mode)
    ):
        raise NormalizationError("operation marker parent is not a real directory")
    marker = OPERATION_MARKERS / f"{operation_id}.json"
    if os.path.lexists(marker):
        raise NormalizationError("operation marker already exists")
    marker_identity: tuple[int, int] | None = None
    device = -1
    inode = -1
    try:
        os.symlink(str(release_root), temporary)
        temporary_info = os.lstat(temporary)
        device, inode = temporary_info.st_dev, temporary_info.st_ino
        marker_payload = (
            json.dumps({
            "releaseId": release_id,
            "manifestSha256": manifest_sha256,
            "target": str(release_root),
            "device": device,
            "inode": inode,
            }, sort_keys=True) + "\n"
        ).encode("utf-8")
        descriptor = os.open(
            marker,
            os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0),
            0o600,
        )
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            stream.write(marker_payload)
            stream.flush()
            os.fsync(stream.fileno())
        marker_info = os.lstat(marker)
        marker_identity = (marker_info.st_dev, marker_info.st_ino)
        # Hard-linking the symlink inode gives no-overwrite publication semantics.
        os.link(temporary, CURRENT, follow_symlinks=False)
        temporary.unlink()
        directory_fd = os.open(CURRENT.parent, os.O_RDONLY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    except BaseException:
        try:
            current = os.lstat(CURRENT)
            if (current.st_dev, current.st_ino) == (device, inode):
                CURRENT.unlink()
        except FileNotFoundError:
            pass
        try:
            current_marker = os.lstat(marker)
            if marker_identity == (current_marker.st_dev, current_marker.st_ino):
                marker.unlink()
        except FileNotFoundError:
            pass
        try:
            temporary.unlink()
        except FileNotFoundError:
            pass
        raise
    return {
        "schemaVersion": 1,
        "status": "created",
        "releaseId": release_id,
        "target": str(release_root),
        "device": device,
        "inode": inode,
    }


def verify(
    operation_id: str,
    release_id: str,
    manifest_sha256: str,
) -> dict[str, object]:
    marker = load_operation(operation_id, release_id, manifest_sha256)
    release_root = inspect_release(release_id, manifest_sha256)
    device = int(marker["device"])
    inode = int(marker["inode"])
    actual = pointer_identity(release_root)
    if actual != (device, inode):
        raise NormalizationError("current pointer inode is not the one created")
    return {
        "schemaVersion": 1,
        "status": "verified",
        "releaseId": release_id,
        "target": str(release_root),
        "device": device,
        "inode": inode,
    }


def remove(
    operation_id: str,
    release_id: str,
    manifest_sha256: str,
) -> dict[str, object]:
    marker_path = OPERATION_MARKERS / f"{operation_id}.json"
    if not os.path.lexists(marker_path):
        return {
            "schemaVersion": 1,
            "status": "not-created",
            "releaseId": release_id,
        }
    marker = load_operation(operation_id, release_id, manifest_sha256)
    release_root = inspect_release(release_id, manifest_sha256)
    device = int(marker["device"])
    inode = int(marker["inode"])
    if not os.path.lexists(CURRENT):
        marker_path.unlink()
        return {
            "schemaVersion": 1,
            "status": "not-created",
            "releaseId": release_id,
        }
    actual = pointer_identity(release_root)
    if actual != (device, inode):
        raise NormalizationError("refusing to remove a pointer not created by this operation")
    CURRENT.unlink()
    directory_fd = os.open(CURRENT.parent, os.O_RDONLY)
    try:
        os.fsync(directory_fd)
    finally:
        os.close(directory_fd)
    marker_path.unlink()
    return {
        "schemaVersion": 1,
        "status": "removed",
        "releaseId": release_id,
        "target": str(release_root),
        "device": device,
        "inode": inode,
    }


def load_operation(
    operation_id: str,
    release_id: str,
    manifest_sha256: str,
) -> dict[str, object]:
    if not OPERATION.fullmatch(operation_id):
        raise NormalizationError("operation ID is invalid")
    marker = OPERATION_MARKERS / f"{operation_id}.json"
    info = os.lstat(marker)
    if stat.S_ISLNK(info.st_mode) or not stat.S_ISREG(info.st_mode):
        raise NormalizationError("operation marker is not a regular file")
    value = json.loads(marker.read_text(encoding="utf-8"))
    if (
        not isinstance(value, dict)
        or set(value) != {
            "releaseId", "manifestSha256", "target", "device", "inode"
        }
        or value["releaseId"] != release_id
        or value["manifestSha256"] != manifest_sha256
        or value["target"] != f"/opt/massar/releases/{release_id}"
        or not isinstance(value["device"], int)
        or isinstance(value["device"], bool)
        or value["device"] < 0
        or not isinstance(value["inode"], int)
        or isinstance(value["inode"], bool)
        or value["inode"] < 0
    ):
        raise NormalizationError("operation marker identity is invalid")
    return value


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for command in ("preflight", "apply", "verify", "remove", "switch"):
        item = commands.add_parser(command)
        item.add_argument("release")
        item.add_argument("manifest_sha256")
        if command in {"apply", "verify", "remove", "switch"}:
            item.add_argument("operation_id")
    args = parser.parse_args(argv)
    try:
        if args.command == "preflight":
            result = preflight(args.release, args.manifest_sha256)
        elif args.command == "apply":
            result = apply(args.operation_id, args.release, args.manifest_sha256)
        elif args.command == "verify":
            result = verify(
                args.operation_id,
                args.release,
                args.manifest_sha256,
            )
        elif args.command == "remove":
            result = remove(
                args.operation_id,
                args.release,
                args.manifest_sha256,
            )
        else:
            result = switch(
                args.operation_id,
                args.release,
                args.manifest_sha256,
            )
        print(json.dumps(result, separators=(",", ":")))
        return 0
    except (NormalizationError, OSError, ValueError) as exc:
        print(f"current release normalization blocked: {exc}", file=sys.stderr)
        return 7


if __name__ == "__main__":
    raise SystemExit(main())
