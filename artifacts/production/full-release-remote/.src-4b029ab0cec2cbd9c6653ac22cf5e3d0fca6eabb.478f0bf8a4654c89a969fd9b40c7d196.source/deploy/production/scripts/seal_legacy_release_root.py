#!/usr/bin/env python3
"""Root-only helper that seals, verifies, or compensates one Legacy release."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import re
import stat
import subprocess
import sys
from pathlib import Path

BASE = Path("/opt/massar/releases")
CURRENT = Path("/opt/massar/current")
CLUSTER_MARKER = Path("/etc/massar/cluster-id")
FIXED_PARENTS = (Path("/opt"), Path("/opt/massar"), BASE)
MARKERS = Path("/run/massar-legacy-seal")
METADATA = {"manifest.json", ".initial-manifest.sha256", ".release-files.sha256"}
RUNTIME_FILES = (
    "deploy/production/compose/compose.base.yml",
    "deploy/production/compose/compose.app.yml",
    "deploy/production/config/nginx/massar-node.conf.template",
)
SERVICES = {
    "backend": "backend", "worker": "worker", "landing": "frontend",
    "student": "frontend", "admin": "frontend", "teacher": "frontend",
    "staff": "frontend", "gateway": None,
}
IMAGES = ("backend", "frontend", "worker")
RELEASE = re.compile(r"^prod-[0-9]{8}-[a-z0-9-]+$")
HEX = re.compile(r"^[0-9a-f]{64}$")
IMAGE = re.compile(r"^sha256:[0-9a-f]{64}$")
OPERATION = re.compile(r"^[0-9a-f]{32}$")
MAXIMUM_MANIFEST_BYTES = 1024 * 1024


class SealError(RuntimeError):
    pass


def fsync_directory(path: Path) -> None:
    descriptor = os.open(path, os.O_RDONLY)
    try:
        os.fsync(descriptor)
    finally:
        os.close(descriptor)


def docker(*arguments: str) -> str:
    completed = subprocess.run(
        ["/usr/bin/docker", *arguments],
        text=True,
        capture_output=True,
        check=False,
        timeout=20,
    )
    if completed.returncode:
        operation = " ".join(arguments[:2]) if arguments else "unknown"
        raise SealError(f"Docker inspection failed during {operation}")
    return completed.stdout


def release_identity(node_id: str) -> tuple[str, dict[str, str]]:
    container_ids = [
        row for row in docker(
            "ps", "-q", "--filter",
            "label=com.docker.compose.project=massar_production",
        ).splitlines() if row
    ]
    containers = json.loads(docker("inspect", *container_ids))
    found: dict[str, dict[str, object]] = {}
    releases: set[str] = set()
    for container in containers:
        labels = container.get("Config", {}).get("Labels") or {}
        service = labels.get("com.docker.compose.service")
        if service not in SERVICES:
            continue
        if service in found:
            raise SealError("multiple running containers exist for one required service")
        state = container.get("State") or {}
        if (
            state.get("Status") != "running"
            or (state.get("Health") or {}).get("Status") != "healthy"
            or labels.get("net.massar.node") != node_id
        ):
            raise SealError("required service health or node label is invalid")
        release = labels.get("net.massar.release")
        if not isinstance(release, str) or not RELEASE.fullmatch(release):
            raise SealError("required service release label is invalid")
        releases.add(release)
        found[service] = container
    if set(found) != set(SERVICES) or len(releases) != 1:
        raise SealError("exactly eight services with one release are required")
    release_id = releases.pop()
    images: dict[str, str] = {}
    for name in IMAGES:
        legacy_tag = f"massar-{name}:{release_id}"
        inspected = json.loads(docker("image", "inspect", legacy_tag))
        image_id = inspected[0].get("Id") if len(inspected) == 1 else None
        if not isinstance(image_id, str) or not IMAGE.fullmatch(image_id):
            raise SealError("tagged image ID is invalid")
        images[name] = image_id
    for service, image_name in SERVICES.items():
        if image_name is not None and found[service].get("Image") != images[image_name]:
            raise SealError("running service image does not match its release tag")
    return release_id, images


def tree_sha256(root: Path) -> str:
    digest = hashlib.sha256()
    for relative in RUNTIME_FILES:
        path = root / relative
        info = os.lstat(path)
        mode = stat.S_IMODE(info.st_mode)
        if stat.S_ISREG(info.st_mode):
            content_digest = hashlib.sha256()
            descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
            try:
                for chunk in iter(lambda: os.read(descriptor, 1024 * 1024), b""):
                    content_digest.update(chunk)
            finally:
                os.close(descriptor)
            digest.update(
                f"F\\0{relative}\\0{mode:o}\\0{info.st_size}\\0{content_digest.hexdigest()}\\n".encode()
            )
        else:
            raise SealError("runtime bundle contains a symlink or special file")
    return digest.hexdigest()


def inspect(node_id: str, *, require_unsealed: bool = True) -> dict[str, object]:
    if os.geteuid() != 0:
        raise SealError("helper must run as root")
    if CLUSTER_MARKER.read_text().strip() != "massar-production":
        raise SealError("cluster marker is invalid")
    for fixed in FIXED_PARENTS:
        fixed_info = os.lstat(fixed)
        if stat.S_ISLNK(fixed_info.st_mode) or not stat.S_ISDIR(fixed_info.st_mode):
            raise SealError("fixed release parent is not a real directory")
    if os.path.lexists(CURRENT):
        raise SealError("current already exists")
    release_id, images = release_identity(node_id)
    root = BASE / release_id
    root_info = os.lstat(root)
    if stat.S_ISLNK(root_info.st_mode) or not stat.S_ISDIR(root_info.st_mode):
        raise SealError("release root is invalid")
    if require_unsealed and any(os.path.lexists(root / name) for name in METADATA):
        raise SealError("Legacy seal metadata already exists")
    for required in RUNTIME_FILES:
        required_info = os.lstat(root / required)
        if stat.S_ISLNK(required_info.st_mode) or not stat.S_ISREG(required_info.st_mode):
            raise SealError("critical release file is missing or unsafe")
    return {
        "schemaVersion": 1, "status": "ready", "nodeId": node_id,
        "releaseId": release_id, "images": images, "treeSha256": tree_sha256(root),
    }


def marker_path(operation_id: str) -> Path:
    if not OPERATION.fullmatch(operation_id):
        raise SealError("operation ID is invalid")
    return MARKERS / f"{operation_id}.json"


def load_marker(operation_id: str) -> dict[str, object] | None:
    path = marker_path(operation_id)
    if not os.path.lexists(path):
        return None
    info = os.lstat(path)
    if stat.S_ISLNK(info.st_mode) or not stat.S_ISREG(info.st_mode):
        raise SealError("operation marker is invalid")
    value = json.loads(path.read_text())
    if (
        not isinstance(value, dict)
        or set(value) != {
            "releaseId", "treeSha256", "payloadSha256", "files", "aliases"
        }
        or not isinstance(value["files"], dict)
        or not isinstance(value["payloadSha256"], dict)
        or not isinstance(value["aliases"], dict)
    ):
        raise SealError("operation marker contract is invalid")
    return value


def create_marker(
    operation_id: str,
    release_id: str,
    tree_digest: str,
    payloads: dict[str, bytes],
) -> Path:
    MARKERS.mkdir(mode=0o700, parents=True, exist_ok=True)
    parent_info = os.lstat(MARKERS)
    if stat.S_ISLNK(parent_info.st_mode) or not stat.S_ISDIR(parent_info.st_mode):
        raise SealError("operation marker parent is not a real directory")
    path = marker_path(operation_id)
    descriptor = os.open(
        path, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o600
    )
    with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
        json.dump({
            "releaseId": release_id,
            "treeSha256": tree_digest,
            "payloadSha256": {
                name: hashlib.sha256(payload).hexdigest()
                for name, payload in payloads.items()
            },
            "files": {},
            "aliases": {},
        }, stream)
        stream.flush()
        os.fsync(stream.fileno())
    fsync_directory(path.parent)
    return path


def update_marker(path: Path, marker: dict[str, object]) -> None:
    temporary = path.with_name(f".{path.name}.progress")
    temporary.unlink(missing_ok=True)
    descriptor = os.open(
        temporary, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o600
    )
    with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
        json.dump(marker, stream)
        stream.flush()
        os.fsync(stream.fileno())
    os.replace(temporary, path)
    fsync_directory(path.parent)


def apply(
    node_id: str,
    operation_id: str,
    expected_tree: str,
    manifest_base64: str,
) -> dict[str, object]:
    if (
        not isinstance(manifest_base64, str)
        or len(manifest_base64) > MAXIMUM_MANIFEST_BYTES * 2
    ):
        raise SealError("sealed manifest payload exceeds its bound")
    existing_journal = load_marker(operation_id)
    readiness = inspect(node_id, require_unsealed=existing_journal is None)
    if readiness["treeSha256"] != expected_tree or not HEX.fullmatch(expected_tree):
        raise SealError("release tree differs from the approved digest")
    manifest = base64.b64decode(manifest_base64, validate=True)
    if not manifest or len(manifest) > MAXIMUM_MANIFEST_BYTES:
        raise SealError("sealed manifest payload exceeds its bound")
    manifest_value = json.loads(manifest.decode())
    manifest_sha = hashlib.sha256(manifest).hexdigest()
    release_id = str(readiness["releaseId"])
    expected_fields = {
        "schemaVersion", "releaseId", "createdAt", "platform", "images",
        "status", "nodeCount", "digestParity", "distribution",
        "sealedLegacyProvenance",
    }
    provenance = manifest_value.get("sealedLegacyProvenance")
    distribution = manifest_value.get("distribution")
    if (
        set(manifest_value) != expected_fields
        or manifest_value.get("schemaVersion") != 1
        or manifest_value.get("status") != "success"
        or manifest_value.get("platform") != "linux/amd64"
        or manifest_value.get("nodeCount") != 3
        or manifest_value.get("digestParity") is not True
        or manifest_value.get("releaseId") != release_id
        or manifest_value.get("images") != readiness["images"]
        or not isinstance(provenance, dict)
        or set(provenance) != {
            "schemaVersion", "type", "sealedAt", "runtimeBundleSha256",
            "runtimeBundleDigestAlgorithm", "sourceReleaseLabel",
        }
        or provenance.get("schemaVersion") != 2
        or provenance.get("type") != "sealed-legacy-bootstrap"
        or provenance.get("runtimeBundleSha256") != expected_tree
        or provenance.get("runtimeBundleDigestAlgorithm")
        != "massar-runtime-bundle-sha256-v1"
        or provenance.get("sourceReleaseLabel") != release_id
        or not isinstance(distribution, dict)
        or set(distribution) != {"node-1", "node-2", "node-3"}
        or any(
            node != {"status": "verified", "releaseFilesSha256": expected_tree}
            for node in distribution.values()
        )
    ):
        raise SealError("sealed manifest does not bind inspected runtime state")
    root = BASE / release_id
    payloads = {
        "manifest.json": manifest,
        ".initial-manifest.sha256": (manifest_sha + "\n").encode(),
        ".release-files.sha256": (expected_tree + "\n").encode(),
    }
    marker_file = (
        marker_path(operation_id)
        if existing_journal is not None
        else create_marker(operation_id, release_id, expected_tree, payloads)
    )
    journal = existing_journal or load_marker(operation_id)
    assert journal is not None
    if (
        journal["releaseId"] != release_id
        or journal["treeSha256"] != expected_tree
        or journal["payloadSha256"] != {
            name: hashlib.sha256(payload).hexdigest()
            for name, payload in payloads.items()
        }
    ):
        raise SealError("existing operation marker binds different release evidence")
    identities = dict(journal["files"])
    try:
        for name, expected_image in readiness["images"].items():
            standard_tag = f"massar/{name}:{release_id}"
            inspected = subprocess.run(
                ["/usr/bin/docker", "image", "inspect", standard_tag],
                text=True, capture_output=True, check=False, timeout=20,
            )
            if inspected.returncode == 0:
                existing_id = json.loads(inspected.stdout)[0].get("Id")
                if existing_id != expected_image:
                    raise SealError("standard image tag exists with a different ID")
                continue
            journal["aliases"][name] = expected_image
            update_marker(marker_file, journal)
            docker("tag", f"massar-{name}:{release_id}", standard_tag)
            actual = json.loads(docker("image", "inspect", standard_tag))[0].get("Id")
            if actual != expected_image:
                raise SealError("created standard image alias has a different ID")
        for name, payload in payloads.items():
            temporary = root / f".massar-seal-{operation_id}-{name.lstrip('.')}"
            destination = root / name
            identity = identities.get(name)
            if identity is not None:
                if os.path.lexists(destination):
                    current = os.lstat(destination)
                    if [current.st_dev, current.st_ino] != identity:
                        raise SealError("published seal file differs from journal")
                    os.chmod(destination, 0o644, follow_symlinks=False)
                    if os.path.lexists(temporary):
                        temporary_info = os.lstat(temporary)
                        if [temporary_info.st_dev, temporary_info.st_ino] != identity:
                            raise SealError("seal temporary differs from journal")
                        temporary.unlink()
                    continue
                if not os.path.lexists(temporary):
                    raise SealError("journaled seal file has no recoverable inode")
            else:
                if os.path.lexists(temporary):
                    temporary_info = os.lstat(temporary)
                    descriptor = os.open(
                        temporary,
                        os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
                    )
                    try:
                        recovered_payload = os.read(
                            descriptor, MAXIMUM_MANIFEST_BYTES + 1
                        )
                    finally:
                        os.close(descriptor)
                    if (
                        not stat.S_ISREG(temporary_info.st_mode)
                        or recovered_payload != payload
                    ):
                        raise SealError("uncommitted seal temporary is not recoverable")
                else:
                    descriptor = os.open(
                        temporary,
                        os.O_WRONLY | os.O_CREAT | os.O_EXCL
                        | getattr(os, "O_NOFOLLOW", 0),
                        0o600,
                    )
                    with os.fdopen(descriptor, "wb") as stream:
                        stream.write(payload)
                        stream.flush()
                        os.fsync(stream.fileno())
                info = os.lstat(temporary)
                identities[name] = [info.st_dev, info.st_ino]
                journal["files"] = dict(identities)
                update_marker(marker_file, journal)
            os.link(temporary, destination, follow_symlinks=False)
            os.chmod(destination, 0o644, follow_symlinks=False)
            temporary.unlink()
            fsync_directory(root)
    except Exception:
        verify_or_remove(node_id, operation_id, True)
        raise
    return {
        "schemaVersion": 1, "status": "sealed", "nodeId": node_id,
        "releaseId": release_id, "treeSha256": expected_tree,
        "manifestSha256": manifest_sha, "files": identities,
    }


def verify_or_remove(node_id: str, operation_id: str, remove: bool) -> dict[str, object]:
    marker = load_marker(operation_id)
    if marker is None:
        return {"schemaVersion": 1, "status": "not-created", "nodeId": node_id}
    root = BASE / str(marker["releaseId"])
    for name, identity in marker["files"].items():
        path = root / name
        temporary = root / f".massar-seal-{operation_id}-{name.lstrip('.')}"
        if os.path.lexists(path):
            info = os.lstat(path)
            if [info.st_dev, info.st_ino] != identity:
                raise SealError("sealed file inode differs from the operation marker")
        elif not remove:
            raise SealError("sealed operation is only partially published")
        if os.path.lexists(temporary):
            temporary_info = os.lstat(temporary)
            if [temporary_info.st_dev, temporary_info.st_ino] != identity:
                raise SealError("seal temporary inode differs from the operation marker")
    if remove:
        for name, identity in marker["files"].items():
            path = root / name
            temporary = root / f".massar-seal-{operation_id}-{name.lstrip('.')}"
            if os.path.lexists(path):
                info = os.lstat(path)
                if [info.st_dev, info.st_ino] == identity:
                    path.unlink()
            if os.path.lexists(temporary):
                info = os.lstat(temporary)
                if [info.st_dev, info.st_ino] == identity:
                    temporary.unlink()
        for name, payload_sha256 in marker["payloadSha256"].items():
            if name in marker["files"]:
                continue
            temporary = root / f".massar-seal-{operation_id}-{name.lstrip('.')}"
            if os.path.lexists(temporary):
                info = os.lstat(temporary)
                descriptor = os.open(
                    temporary, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
                )
                try:
                    payload = os.read(descriptor, MAXIMUM_MANIFEST_BYTES + 1)
                finally:
                    os.close(descriptor)
                if (
                    not stat.S_ISREG(info.st_mode)
                    or hashlib.sha256(payload).hexdigest() != payload_sha256
                ):
                    raise SealError("unrecorded seal temporary is not operation-owned")
                temporary.unlink()
        for name, expected_image in marker["aliases"].items():
            standard_tag = f"massar/{name}:{marker['releaseId']}"
            inspected_command = subprocess.run(
                ["/usr/bin/docker", "image", "inspect", standard_tag],
                text=True, capture_output=True, check=False, timeout=20,
            )
            if inspected_command.returncode:
                continue
            inspected = json.loads(inspected_command.stdout)
            if len(inspected) != 1 or inspected[0].get("Id") != expected_image:
                raise SealError("standard image alias no longer matches the operation")
            docker("image", "rm", standard_tag)
        marker_path(operation_id).unlink()
        fsync_directory(root)
        fsync_directory(MARKERS)
    return {
        "schemaVersion": 1,
        "status": "removed" if remove else "verified",
        "nodeId": node_id, "releaseId": marker["releaseId"],
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("inspect", "apply", "verify", "remove"))
    parser.add_argument("node_id")
    parser.add_argument("operation_id", nargs="?")
    parser.add_argument("tree_sha256", nargs="?")
    parser.add_argument("manifest_base64", nargs="?")
    args = parser.parse_args(argv)
    try:
        if args.action == "inspect":
            result = inspect(args.node_id)
        elif args.action == "apply":
            result = apply(args.node_id, args.operation_id, args.tree_sha256, args.manifest_base64)
        else:
            result = verify_or_remove(
                args.node_id, args.operation_id, args.action == "remove"
            )
        print(json.dumps(result, separators=(",", ":")))
        return 0
    except (SealError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Legacy release seal blocked: {exc}", file=sys.stderr)
        return 8


if __name__ == "__main__":
    raise SystemExit(main())
