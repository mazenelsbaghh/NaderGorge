#!/usr/bin/env python3
"""Build/export/import immutable release images and verify digest parity."""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import tarfile
import uuid
from pathlib import Path
from typing import Any

from ssh_transport import SshTarget, StrictSshTransport


DIGEST_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
RELEASE_RE = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$")
IMAGES = ("backend", "frontend", "worker", "migrator")
SOURCE_ROOTS = {"backend", "frontend", "worker", "deploy"}
SOURCE_EXCLUDED_PARTS = {
    ".next", ".pytest_cache", ".tmp", "__pycache__",
    "bin", "dist", "node_modules", "obj", "test-results",
}


def command(argv: list[str]) -> str:
    return subprocess.check_output(argv, text=True).strip()


def image_digest(image: str) -> str:
    digest = command(["docker", "image", "inspect", image, "--format", "{{.Id}}"])
    if not DIGEST_RE.fullmatch(digest):
        raise RuntimeError(f"invalid image digest for {image}")
    return digest


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def source_state(repo: Path) -> dict[str, Any]:
    head = command(["git", "-C", str(repo), "rev-parse", "HEAD"])
    listed = source_paths(repo)
    digest = hashlib.sha256()
    for relative in listed:
        encoded = relative.encode("utf-8", errors="surrogateescape")
        path = repo / relative
        if path.is_symlink() or not path.is_file():
            raise RuntimeError(f"release source must be a regular non-symlink file: {relative}")
        digest.update(encoded)
        digest.update(b"\0")
        digest.update(file_sha256(path).encode("ascii"))
        digest.update(b"\0")
    status = subprocess.check_output([
        "git", "-C", str(repo), "status", "--porcelain=v1", "-z", "--untracked-files=all",
    ]).split(b"\0")
    dirty = False
    for entry in status:
        if len(entry) < 4:
            continue
        relative = entry[3:].decode("utf-8", errors="surrogateescape")
        parts = Path(relative).parts
        if (
            parts
            and parts[0] in SOURCE_ROOTS
            and not any(part in SOURCE_EXCLUDED_PARTS for part in parts)
        ):
            dirty = True
            break
    source_digest = digest.hexdigest()
    release_id = f"src-{source_digest[:40]}" if dirty else f"git-{head}"
    return {
        "releaseId": release_id,
        "gitCommit": head,
        "sourceStateSha256": source_digest,
        "dirtySourceSnapshot": dirty,
    }


def source_paths(repo: Path) -> tuple[str, ...]:
    listed = subprocess.check_output(
        [
            "git", "-C", str(repo), "ls-files", "-z",
            "--cached", "--others", "--exclude-standard",
        ],
    ).split(b"\0")
    paths: list[str] = []
    for encoded in sorted(value for value in listed if value):
        relative = encoded.decode("utf-8", errors="surrogateescape")
        parts = Path(relative).parts
        if (
            not parts
            or parts[0] not in SOURCE_ROOTS
            or any(part in SOURCE_EXCLUDED_PARTS for part in parts)
        ):
            continue
        path = repo / relative
        if not path.is_file():
            continue
        paths.append(relative)
    return tuple(paths)


def create_source_snapshot(repo: Path, destination: Path, expected_sha256: str) -> None:
    if destination.exists() or destination.is_symlink():
        raise RuntimeError("release source snapshot destination already exists")
    destination.mkdir(mode=0o700, parents=True)
    digest = hashlib.sha256()
    for relative in source_paths(repo):
        source = repo / relative
        if source.is_symlink() or not source.is_file():
            raise RuntimeError(f"release source must be a regular non-symlink file: {relative}")
        target = destination / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target, follow_symlinks=False)
        encoded = relative.encode("utf-8", errors="surrogateescape")
        digest.update(encoded)
        digest.update(b"\0")
        digest.update(file_sha256(target).encode("ascii"))
        digest.update(b"\0")
    if digest.hexdigest() != expected_sha256:
        raise RuntimeError("source changed while the immutable build snapshot was created")


def write_json_atomic(path: Path, value: dict[str, Any]) -> None:
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    os.replace(temporary, path)


def verify_local_release_artifacts(
    output: Path,
    release_id: str,
    provenance: dict[str, Any],
) -> tuple[dict[str, str], dict[str, Any]]:
    manifest_path = output / "manifest.json"
    if output.is_symlink() or not output.is_dir() or not manifest_path.is_file():
        raise RuntimeError("existing release output is incomplete")
    value = json.loads(manifest_path.read_text(encoding="utf-8"))
    if (
        value.get("schemaVersion") != 1
        or value.get("status") != "success"
        or value.get("releaseId") != release_id
        or any(value.get(key) != expected for key, expected in provenance.items())
    ):
        raise RuntimeError("existing release output provenance does not match current source")
    images = value.get("images")
    if not isinstance(images, dict):
        raise RuntimeError("existing release output image manifest is invalid")
    verify_manifest(images)
    artifacts = ("release-files.tar.gz", *(f"{name}.tar" for name in IMAGES))
    for filename in artifacts:
        artifact = output / filename
        sidecar = output / f"{filename}.sha256"
        if (
            artifact.is_symlink()
            or sidecar.is_symlink()
            or not artifact.is_file()
            or not sidecar.is_file()
            or sidecar.read_text(encoding="utf-8").strip() != file_sha256(artifact)
        ):
            raise RuntimeError(f"existing release artifact is incomplete: {filename}")
    return {name: str(images[name]) for name in IMAGES}, value


def resolve_release(repo: Path, requested: str) -> dict[str, Any]:
    provenance = source_state(repo)
    if requested == "auto":
        return provenance
    if not RELEASE_RE.fullmatch(requested):
        raise ValueError("release ID must be auto, git-<commit>, or src-<40-hex-source-digest>")
    if requested != provenance["releaseId"]:
        raise ValueError(
            "release ID does not match the exact current source state; use --release auto"
        )
    return provenance


def build_release(repo: Path, release_id: str, output: Path) -> dict[str, str]:
    if not RELEASE_RE.fullmatch(release_id):
        raise ValueError("release ID must identify a verified Git commit or source snapshot")
    tags = {
        "backend": f"massar/backend:{release_id}",
        "frontend": f"massar/frontend:{release_id}",
        "worker": f"massar/worker:{release_id}",
        "migrator": f"massar/migrator:{release_id}",
    }
    builds = {
        "backend": [repo / "backend", repo / "backend/Dockerfile"],
        "frontend": [repo / "frontend", repo / "frontend/Dockerfile"],
        "worker": [repo / "worker", repo / "worker/Dockerfile"],
        "migrator": [repo / "backend", repo / "backend/Dockerfile.migrator"],
    }
    output.mkdir(parents=True, exist_ok=True)
    digests: dict[str, str] = {}
    for name in IMAGES:
        context, dockerfile = builds[name]
        build_arguments: list[str] = []
        if name == "frontend":
            frontend_contract = {
                "NEXT_PUBLIC_API_URL": "https://api.massar-academy.net/api",
                "NEXT_PUBLIC_BACKEND_URL": "https://api.massar-academy.net",
                "NEXT_PUBLIC_WS_URL": "https://ws.massar-academy.net",
                "NEXT_PUBLIC_APP_DOMAIN": "massar-academy.net",
                "NEXT_PUBLIC_APP_URL": "https://app.massar-academy.net",
            }
            for key, value in frontend_contract.items():
                build_arguments.extend(["--build-arg", f"{key}={value}"])
        subprocess.run(
            [
                "docker",
                "build",
                "--pull",
                "--platform",
                "linux/amd64",
                *build_arguments,
                "--tag",
                tags[name],
                "--file",
                str(dockerfile),
                str(context),
            ],
            check=True,
        )
        digests[name] = image_digest(tags[name])
        architecture = command([
            "docker", "image", "inspect", tags[name],
            "--format", "{{.Architecture}}/{{.Os}}",
        ])
        if architecture != "amd64/linux":
            raise RuntimeError(f"{name} image has unexpected platform {architecture}")
        archive = output / f"{name}.tar"
        subprocess.run(["docker", "save", "--output", str(archive), tags[name]], check=True)
        (output / f"{name}.tar.sha256").write_text(
            file_sha256(archive) + "\n", encoding="utf-8"
        )
    return digests


def create_release_bundle(repo: Path, output: Path) -> Path:
    archive = output / "release-files.tar.gz"
    production = repo / "deploy/production"
    with tarfile.open(archive, "w:gz") as bundle:
        for path in sorted(production.rglob("*")):
            if not path.is_file() or "__pycache__" in path.parts:
                continue
            bundle.add(path, arcname=path.relative_to(repo), recursive=False)
    (output / "release-files.tar.gz.sha256").write_text(
        file_sha256(archive) + "\n",
        encoding="utf-8",
    )
    return archive


def distribute_release(
    output: Path,
    release_id: str,
    manifest: dict[str, Any],
    nodes: tuple[object, ...],
    ssh_user: str,
    transport: StrictSshTransport,
) -> dict[str, dict[str, str]]:
    if not RELEASE_RE.fullmatch(release_id):
        raise ValueError("invalid release ID")
    bundle = output / "release-files.tar.gz"
    manifest_path = output / "manifest.json"
    expected_bundle = file_sha256(bundle)
    expected_manifest = file_sha256(manifest_path)
    results: dict[str, dict[str, str]] = {}

    for node in nodes:
        node_id = str(getattr(node, "id"))
        target = SshTarget(node_id, str(getattr(node, "public_address")), ssh_user)
        remote_root = f"/tmp/massar-{release_id}"
        release_root = f"/opt/massar/releases/{release_id}"
        resume = transport.run(
            target,
            (
                "bash", "-lc",
                f"""
set -euo pipefail
if ! test -e {release_root}; then
  printf 'missing\n'
elif test -d {release_root} \
  && actual_manifest="$(sha256sum {release_root}/manifest.json | awk '{{print $1}}')" \
  && initial_manifest="$(cat {release_root}/.initial-manifest.sha256)" \
  && {{ test "$actual_manifest" = "{expected_manifest}" || test "$actual_manifest" = "$initial_manifest"; }} \
  && test "$(cat {release_root}/.release-files.sha256)" = "{expected_bundle}" \
  && test "$(sudo /usr/bin/docker image inspect massar/backend:{release_id} --format '{{{{.Id}}}}')" = "{manifest['images']['backend']}" \
  && test "$(sudo /usr/bin/docker image inspect massar/frontend:{release_id} --format '{{{{.Id}}}}')" = "{manifest['images']['frontend']}" \
  && test "$(sudo /usr/bin/docker image inspect massar/worker:{release_id} --format '{{{{.Id}}}}')" = "{manifest['images']['worker']}" \
  && test "$(sudo /usr/bin/docker image inspect massar/migrator:{release_id} --format '{{{{.Id}}}}')" = "{manifest['images']['migrator']}"; then
  printf 'verified\n'
else
  printf 'mismatch\n'
fi
""",
            ),
            timeout_seconds=60,
        ).stdout.strip()
        if resume == "verified":
            results[node_id] = {
                "status": "verified",
                "releaseFilesSha256": expected_bundle,
            }
            continue
        if resume != "missing":
            raise RuntimeError(
                f"{node_id} has a conflicting immutable release root for {release_id}"
            )
        transport.run(
            target,
            ("bash", "-lc", f"set -euo pipefail; rm -rf {remote_root}; install -d -m 0700 {remote_root}"),
        )
        try:
            transport.copy(target, bundle, f"{remote_root}/release-files.tar.gz", timeout_seconds=600)
            transport.copy(target, manifest_path, f"{remote_root}/manifest.json", timeout_seconds=120)
            for name in IMAGES:
                transport.copy(
                    target,
                    output / f"{name}.tar",
                    f"{remote_root}/{name}.tar",
                    timeout_seconds=1200,
                )
                transport.copy(
                    target,
                    output / f"{name}.tar.sha256",
                    f"{remote_root}/{name}.tar.sha256",
                    timeout_seconds=120,
                )
            image_checks = "\n".join(
                f"""(
  cd {remote_root}
  actual_archive_sha256="$(sha256sum {name}.tar | awk '{{print $1}}')"
  expected_archive_sha256="$(cat {name}.tar.sha256)"
  test "$actual_archive_sha256" = "$expected_archive_sha256"
  sudo /usr/bin/docker load --input {name}.tar >/dev/null
  actual="$(sudo /usr/bin/docker image inspect massar/{name}:{release_id} --format '{{{{.Id}}}}')"
  test "$actual" = "{manifest['images'][name]}"
  rm -f {name}.tar {name}.tar.sha256
)"""
                for name in IMAGES
            )
            remote_script = f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
printf '%s  %s\n' '{expected_bundle}' '{remote_root}/release-files.tar.gz' | sha256sum -c -
printf '%s  %s\n' '{expected_manifest}' '{remote_root}/manifest.json' | sha256sum -c -
{image_checks}
sudo /usr/local/sbin/massar-install-immutable-release \
  install-release {release_id} {expected_bundle} {expected_manifest}
release_root="{release_root}"
test -f "$release_root/deploy/production/compose/compose.app.yml"
test "$(sha256sum "$release_root/manifest.json" | awk '{{print $1}}')" = "{expected_manifest}"
"""
            transport.run(
                target,
                ("bash", "-lc", remote_script),
                timeout_seconds=1800,
            )
            results[node_id] = {
                "status": "verified",
                "releaseFilesSha256": expected_bundle,
            }
        finally:
            transport.run(
                target,
                ("bash", "-lc", f"rm -rf {remote_root}"),
                timeout_seconds=60,
                check=False,
            )
    return results


def publish_final_manifest(
    output: Path,
    release_id: str,
    nodes: tuple[object, ...],
    ssh_user: str,
    transport: StrictSshTransport,
) -> None:
    manifest_path = output / "manifest.json"
    expected = file_sha256(manifest_path)
    for node in nodes:
        node_id = str(getattr(node, "id"))
        target = SshTarget(node_id, str(getattr(node, "public_address")), ssh_user)
        temporary = f"/tmp/massar-{release_id}-manifest.json"
        try:
            transport.copy(target, manifest_path, temporary, timeout_seconds=120)
            transport.run(
                target,
                (
                    "bash", "-lc",
                    f"set -euo pipefail; "
                    f"test \"$(cat /etc/massar/cluster-id)\" = massar-production; "
                    f"printf '%s  %s\\n' '{expected}' '{temporary}' | sha256sum -c -; "
                    f"sudo /usr/local/sbin/massar-install-immutable-release "
                    f"publish-final-manifest {release_id} {expected}",
                ),
                timeout_seconds=120,
            )
        finally:
            transport.run(
                target,
                ("rm", "-f", temporary),
                timeout_seconds=30,
                check=False,
            )


def verify_manifest(manifest: dict[str, str]) -> None:
    if set(manifest) != set(IMAGES):
        raise ValueError("release manifest must include exactly the four application images")
    for name, digest in manifest.items():
        if not DIGEST_RE.fullmatch(digest):
            raise ValueError(f"invalid {name} digest")
