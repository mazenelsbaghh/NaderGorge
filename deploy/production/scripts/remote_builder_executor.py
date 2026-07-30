#!/usr/bin/env python3
"""Run on node-3 to build one immutable release from a streamed snapshot.

This command is deliberately a builder-host program. It is not imported by
the operator-side release path and performs no SSH or local-workstation build.
Redis leadership is intentionally not a placement constraint: Sentinel may
move it while node-3 remains the designated builder.
"""

from __future__ import annotations

import argparse
import datetime as dt
import grp
import hashlib
import json
import os
import pwd
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
import uuid
from pathlib import Path
from typing import Any, Sequence

CLUSTER_ID = "massar-production"
BUILDER_NODE_ID = "node-3"
IMAGES = ("backend", "frontend", "worker", "migrator")
RELEASE_RE = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$")
BUILD_ROOT = Path("/var/lib/massar/builds")
CLUSTER_MARKER = Path("/etc/massar/cluster-id")
NODE_ID_MARKER = Path("/etc/massar/node-id")
DIGEST_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
TEMP_BUILD_RE = re.compile(r"^\.artifacts\.[0-9a-f]{32}\.building$")
LOCK_NAME = ".remote-builder.lock"


class RemoteBuilderError(RuntimeError):
    pass


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def source_digest(source: Path) -> str:
    if source.is_symlink() or not source.is_dir():
        raise RemoteBuilderError("remote source workspace is missing or unsafe")
    digest = hashlib.sha256()
    files: list[Path] = []
    for path in source.rglob("*"):
        if path.is_symlink() or not path.is_file() and not path.is_dir():
            raise RemoteBuilderError("remote source workspace must contain regular files only")
        if path.is_file():
            files.append(path)
    for path in sorted(files, key=lambda item: item.relative_to(source).as_posix()):
        relative = path.relative_to(source).as_posix()
        digest.update(relative.encode("utf-8", errors="surrogateescape"))
        digest.update(b"\0")
        digest.update(sha256_file(path).encode("ascii"))
        digest.update(b"\0")
    return digest.hexdigest()


def patroni_role() -> str:
    """Read the unauthenticated loopback Patroni status without logging it."""
    try:
        with urllib.request.urlopen("http://127.0.0.1:8008/patroni", timeout=5) as response:
            value = json.loads(response.read().decode("utf-8"))
    except (OSError, urllib.error.URLError, json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise RemoteBuilderError("cannot safely determine local PostgreSQL role") from exc
    role = value.get("role") if isinstance(value, dict) else None
    if not isinstance(role, str) or role not in {"leader", "master", "primary", "replica", "standby_leader"}:
        raise RemoteBuilderError("cannot safely determine local PostgreSQL role")
    return role


def preflight(*, workspace: Path, release_id: str, expected_source_sha256: str, patroni_role_reader=None) -> Path:
    if not RELEASE_RE.fullmatch(release_id):
        raise RemoteBuilderError("release ID is invalid")
    if not SHA256_RE.fullmatch(expected_source_sha256):
        raise RemoteBuilderError("source state digest is invalid")
    if not CLUSTER_MARKER.is_file() or CLUSTER_MARKER.read_text(encoding="ascii").strip() != CLUSTER_ID:
        raise RemoteBuilderError("builder host is not in massar-production")
    if not NODE_ID_MARKER.is_file() or NODE_ID_MARKER.read_text(encoding="utf-8").strip() != BUILDER_NODE_ID:
        raise RemoteBuilderError("remote build is pinned to node-3")
    root = BUILD_ROOT.resolve()
    candidate = workspace.expanduser().resolve()
    if workspace.is_symlink() or candidate != root / release_id:
        raise RemoteBuilderError("workspace must be the immutable remote release workspace")
    if not candidate.is_dir():
        raise RemoteBuilderError("workspace does not exist")
    patroni_role_reader = patroni_role if patroni_role_reader is None else patroni_role_reader
    if patroni_role_reader() in {"leader", "master", "primary"}:
        raise RemoteBuilderError("remote builder refuses the current PostgreSQL leader")
    actual = source_digest(candidate / "source")
    if actual != expected_source_sha256:
        raise RemoteBuilderError("streamed source digest does not match the release contract")
    return candidate


def command(argv: Sequence[str]) -> str:
    return subprocess.check_output(list(argv), text=True).strip()


def run(argv: Sequence[str]) -> None:
    subprocess.run(list(argv), check=True)


def image_digest(tag: str) -> str:
    digest = command(["sudo", "/usr/bin/docker", "image", "inspect", tag, "--format", "{{.Id}}"])
    if not DIGEST_RE.fullmatch(digest):
        raise RemoteBuilderError(f"invalid image digest for {tag}")
    return digest


def write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(json.dumps(value, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    os.chmod(temporary, 0o600)
    os.replace(temporary, path)


def secure_cache_layout(workspace: Path) -> int:
    """Source stays root-only; relay artifacts are group-readable, never public."""
    if os.geteuid() != 0:
        raise RemoteBuilderError("remote builder helper must run as root")
    try:
        massar_gid = grp.getgrnam("massar").gr_gid
    except KeyError as exc:
        raise RemoteBuilderError("massar group is required for remote artifact relay") from exc
    source = workspace / "source"
    os.chown(workspace, 0, massar_gid)
    os.chmod(workspace, 0o750)
    os.chown(source, 0, 0)
    os.chmod(source, 0o700)
    return massar_gid


def secure_relay_directory(path: Path, massar_gid: int) -> None:
    os.chown(path, 0, massar_gid)
    os.chmod(path, 0o750)


def secure_relay_file(path: Path, massar_gid: int) -> None:
    os.chown(path, 0, massar_gid)
    os.chmod(path, 0o640)


def recover_stale_builds(workspace: Path) -> None:
    """Remove only abandoned UUID build directories inside this exact workspace."""
    lock = workspace / LOCK_NAME
    if lock.exists() or lock.is_symlink():
        if lock.is_symlink() or not lock.is_file():
            raise RemoteBuilderError("remote builder lock is unsafe")
        try:
            pid = int(lock.read_text(encoding="ascii").strip())
            if pid <= 0:
                raise ValueError
        except ValueError as exc:
            raise RemoteBuilderError("remote builder lock is invalid") from exc
        try:
            os.kill(pid, 0)
        except ProcessLookupError:
            lock.unlink()
        except PermissionError as exc:
            raise RemoteBuilderError("remote builder lock process cannot be verified") from exc
        else:
            raise RemoteBuilderError("remote builder is already active")
    for candidate in workspace.iterdir():
        if not TEMP_BUILD_RE.fullmatch(candidate.name):
            continue
        if candidate.is_symlink() or not candidate.is_dir() or candidate.parent != workspace:
            raise RemoteBuilderError("stale remote builder directory is unsafe")
        shutil.rmtree(candidate)


def acquire_build_lock(workspace: Path) -> Path:
    lock = workspace / LOCK_NAME
    try:
        descriptor = os.open(lock, os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, "O_NOFOLLOW", 0), 0o600)
    except FileExistsError as exc:
        raise RemoteBuilderError("remote builder is already active") from exc
    with os.fdopen(descriptor, "w", encoding="ascii") as stream:
        stream.write(f"{os.getpid()}\n")
        stream.flush()
        os.fsync(stream.fileno())
    return lock


def write_failure_record(workspace: Path, massar_gid: int, error: BaseException) -> None:
    reason = str(error).replace("\n", " ")[:500] or "remote builder failed"
    write_json_atomic(workspace / "build-error.json", {"status": "failed", "reason": reason, "createdAt": utc_now()})
    secure_relay_file(workspace / "build-error.json", massar_gid)


def materialize_staged_source(
    *, workspace: Path, release_id: str, expected_source_sha256: str, staging: Path
) -> None:
    expected = Path(f"/tmp/massar-build-source-{release_id}")
    if staging != expected or staging.is_symlink() or not staging.is_dir():
        raise RemoteBuilderError("source staging path is invalid")
    info = staging.stat()
    try:
        operator_uid = pwd.getpwnam("massar-ops").pw_uid
    except KeyError as exc:
        raise RemoteBuilderError("massar-ops account is required for source staging") from exc
    if info.st_uid != operator_uid or info.st_mode & 0o077:
        raise RemoteBuilderError("source staging ownership or mode is unsafe")
    for path in staging.rglob("*"):
        if path.is_symlink() or not path.is_file() and not path.is_dir():
            raise RemoteBuilderError("source staging contains unsafe entries")
    source = workspace / "source"
    if source.exists() or source.is_symlink():
        # A previous attempt may have passed the immutable source into this
        # root-owned workspace and then stopped at a later safety gate (for
        # example, while this node was the database leader).  Reusing it is
        # safe only when the exact source contract still matches; never
        # replace or merge an existing source tree.
        if source.is_symlink() or not source.is_dir() or source_digest(source) != expected_source_sha256:
            raise RemoteBuilderError("immutable workspace source does not match the release contract")
        shutil.rmtree(staging, ignore_errors=True)
        return
    workspace.mkdir(mode=0o700, parents=True, exist_ok=True)
    try:
        shutil.copytree(staging, source, copy_function=shutil.copyfile)
        for path in (source, *source.rglob("*")):
            os.chown(path, 0, 0)
            os.chmod(path, 0o700 if path.is_dir() else 0o600)
    finally:
        shutil.rmtree(staging, ignore_errors=True)


def _build_spec(source: Path, release_id: str) -> dict[str, tuple[Path, Path, list[str]]]:
    frontend_args = [
        "--build-arg", "NEXT_PUBLIC_API_URL=https://api.massar-academy.net/api",
        "--build-arg", "NEXT_PUBLIC_BACKEND_URL=https://api.massar-academy.net",
        "--build-arg", "NEXT_PUBLIC_WS_URL=https://ws.massar-academy.net",
        "--build-arg", "NEXT_PUBLIC_APP_DOMAIN=massar-academy.net",
        "--build-arg", "NEXT_PUBLIC_APP_URL=https://app.massar-academy.net",
        "--build-arg", f"NEXT_PUBLIC_RELEASE_ID={release_id}",
    ]
    return {
        "backend": (source / "backend", source / "backend/Dockerfile", []),
        "frontend": (source / "frontend", source / "frontend/Dockerfile", frontend_args),
        "worker": (source / "worker", source / "worker/Dockerfile", []),
        "migrator": (source / "backend", source / "backend/Dockerfile.migrator", []),
    }


def _existing_manifest(workspace: Path, release_id: str, source_sha256: str) -> dict[str, Any] | None:
    manifest_path = workspace / "builder-manifest.json"
    artifact_root = workspace / "artifacts"
    if not manifest_path.exists() and not artifact_root.exists():
        return None
    if manifest_path.is_symlink() or artifact_root.is_symlink() or not manifest_path.is_file() or not artifact_root.is_dir():
        raise RemoteBuilderError("incomplete immutable builder cache already exists")
    try:
        value = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise RemoteBuilderError("builder cache manifest is invalid") from exc
    if (
        value.get("status") != "success"
        or value.get("releaseId") != release_id
        or value.get("sourceStateSha256") != source_sha256
        or value.get("builderNodeId") != BUILDER_NODE_ID
        or set(value.get("images", {})) != set(IMAGES)
    ):
        raise RemoteBuilderError("builder cache does not match the immutable release contract")
    artifacts = value.get("artifacts")
    if not isinstance(artifacts, dict) or set(artifacts) != set(IMAGES):
        raise RemoteBuilderError("builder cache artifacts are invalid")
    for name in IMAGES:
        entry = artifacts[name]
        path = artifact_root / f"{name}.tar"
        if (
            not isinstance(entry, dict)
            or path.is_symlink()
            or not path.is_file()
            or entry.get("sha256") != sha256_file(path)
        ):
            raise RemoteBuilderError(f"builder cache artifact is invalid: {name}")
    return value


def execute(*, workspace: Path, release_id: str, source_sha256: str, source_staging: Path | None = None) -> dict[str, Any]:
    if source_staging is not None:
        materialize_staged_source(
            workspace=workspace,
            release_id=release_id,
            expected_source_sha256=source_sha256,
            staging=source_staging,
        )
    workspace = preflight(
        workspace=workspace,
        release_id=release_id,
        expected_source_sha256=source_sha256,
    )
    massar_gid = secure_cache_layout(workspace)
    recover_stale_builds(workspace)
    lock = acquire_build_lock(workspace)

    source = workspace / "source"
    temporary = workspace / f".artifacts.{uuid.uuid4().hex}.building"
    if temporary.exists() or temporary.is_symlink():
        raise RemoteBuilderError("temporary builder cache path already exists")
    temporary.mkdir(mode=0o700)
    try:
        existing = _existing_manifest(workspace, release_id, source_sha256)
        if existing is not None:
            secure_relay_directory(workspace / "artifacts", massar_gid)
            for name in IMAGES:
                secure_relay_file(workspace / "artifacts" / f"{name}.tar", massar_gid)
            secure_relay_file(workspace / "builder-manifest.json", massar_gid)
            evidence_path = workspace / "build-evidence.json"
            if evidence_path.is_file() and not evidence_path.is_symlink():
                secure_relay_file(evidence_path, massar_gid)
            shutil.rmtree(temporary, ignore_errors=True)
            return existing
        images: dict[str, str] = {}
        artifacts: dict[str, dict[str, str]] = {}
        for name in IMAGES:
            context, dockerfile, build_args = _build_spec(source, release_id)[name]
            if context.is_symlink() or dockerfile.is_symlink() or not context.is_dir() or not dockerfile.is_file():
                raise RemoteBuilderError(f"remote source lacks the required {name} build input")
            tag = f"massar/{name}:{release_id}"
            run([
                "sudo", "/usr/bin/docker", "build", "--pull", "--platform", "linux/amd64",
                *build_args, "--tag", tag, "--file", str(dockerfile), str(context),
            ])
            platform = command(["sudo", "/usr/bin/docker", "image", "inspect", tag, "--format", "{{.Architecture}}/{{.Os}}"])
            if platform != "amd64/linux":
                raise RemoteBuilderError(f"{name} image has unexpected platform {platform}")
            images[name] = image_digest(tag)
            archive = temporary / f"{name}.tar"
            run(["sudo", "/usr/bin/docker", "save", "--output", str(archive), tag])
            if archive.is_symlink() or not archive.is_file() or archive.stat().st_size <= 0:
                raise RemoteBuilderError(f"{name} image archive was not created")
            secure_relay_file(archive, massar_gid)
            artifacts[name] = {"sha256": sha256_file(archive), "filename": archive.name}
        os.rename(temporary, workspace / "artifacts")
        secure_relay_directory(workspace / "artifacts", massar_gid)
        manifest = {
            "schemaVersion": 1,
            "status": "success",
            "clusterId": CLUSTER_ID,
            "builderNodeId": BUILDER_NODE_ID,
            "releaseId": release_id,
            "sourceStateSha256": source_sha256,
            "createdAt": utc_now(),
            "platform": "linux/amd64",
            "images": images,
            "artifacts": artifacts,
        }
        write_json_atomic(workspace / "builder-manifest.json", manifest)
        secure_relay_file(workspace / "builder-manifest.json", massar_gid)
        evidence = {
            "schemaVersion": 1,
            "status": "success",
            "releaseId": release_id,
            "builderNodeId": BUILDER_NODE_ID,
            "sourceStateSha256": source_sha256,
            "manifestSha256": sha256_file(workspace / "builder-manifest.json"),
            "createdAt": utc_now(),
        }
        write_json_atomic(workspace / "build-evidence.json", evidence)
        secure_relay_file(workspace / "build-evidence.json", massar_gid)
        return manifest
    except BaseException:
        shutil.rmtree(temporary, ignore_errors=True)
        try:
            write_failure_record(workspace, massar_gid, sys.exc_info()[1] or RemoteBuilderError("remote builder failed"))
        except OSError:
            pass
        raise
    finally:
        lock.unlink(missing_ok=True)


def arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", required=True, type=Path)
    parser.add_argument("--source-staging", required=True, type=Path)
    parser.add_argument("--release", required=True)
    parser.add_argument("--source-sha256", required=True)
    parser.add_argument("--yes", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = arguments()
    if not args.yes:
        raise RemoteBuilderError("remote build requires --yes")
    print(json.dumps(execute(
        workspace=args.workspace,
        release_id=args.release,
        source_sha256=args.source_sha256,
        source_staging=args.source_staging,
    ), sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RemoteBuilderError, OSError, subprocess.SubprocessError) as exc:
        print(f"remote builder failed: {exc}", file=sys.stderr)
        raise SystemExit(6)
