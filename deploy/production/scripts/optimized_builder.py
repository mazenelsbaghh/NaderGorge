#!/usr/bin/env python3
"""Content-addressed builds and mTLS registry publication on node-3."""
from __future__ import annotations

import fcntl
import hashlib
import ipaddress
import json
import os
import re
import ssl
import subprocess
import time
import urllib.request
from pathlib import Path

IMAGES = ("backend", "migrator", "frontend", "worker")
CONFIG = Path("/etc/massar/image-registry.json")
CACHE = Path("/var/lib/massar/image-build-cache")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}")
FRONTEND_ARGS = {
    "NEXT_PUBLIC_API_URL": "https://api.massar-academy.net/api",
    "NEXT_PUBLIC_BACKEND_URL": "https://api.massar-academy.net",
    "NEXT_PUBLIC_WS_URL": "https://ws.massar-academy.net",
    "NEXT_PUBLIC_APP_DOMAIN": "massar-academy.net",
    "NEXT_PUBLIC_APP_URL": "https://app.massar-academy.net",
}


def docker(*arguments: str) -> str:
    return subprocess.check_output(
        ["/usr/bin/docker", *arguments], text=True, timeout=1800,
    ).strip()


def registry_endpoint() -> str:
    metadata = CONFIG.stat()
    if CONFIG.is_symlink() or metadata.st_uid != 0 or metadata.st_mode & 0o022:
        raise RuntimeError("registry configuration must be root-owned and immutable to other users")
    endpoint = json.loads(CONFIG.read_text())["endpoint"]
    host, port = endpoint.split(":")
    if not ipaddress.ip_address(host).is_private or port != "5443":
        raise RuntimeError("registry must use the private overlay on port 5443")
    return endpoint


def registry_manifest(endpoint: str, image: str, reference: str) -> tuple[str, str]:
    certificates = Path("/etc/docker/certs.d") / endpoint
    context = ssl.create_default_context(cafile=str(certificates / "ca.crt"))
    context.load_cert_chain(certificates / "client.cert", certificates / "client.key")
    request = urllib.request.Request(
        f"https://{endpoint}/v2/massar/{image}/manifests/{reference}",
        headers={"Accept": "application/vnd.docker.distribution.manifest.v2+json, application/vnd.oci.image.manifest.v1+json"},
    )
    with urllib.request.urlopen(request, context=context, timeout=30) as response:
        content = response.read(1024 * 1024 + 1)
        declared = response.headers.get("Docker-Content-Digest")
    actual = "sha256:" + hashlib.sha256(content).hexdigest()
    if len(content) > 1024 * 1024 or declared != actual:
        raise RuntimeError("registry manifest digest mismatch")
    config_digest = json.loads(content)["config"]["digest"]
    if not DIGEST.fullmatch(config_digest):
        raise RuntimeError("registry image configuration digest is invalid")
    return actual, config_digest


def context_path(source: Path, image: str) -> Path:
    return source / ("backend" if image == "migrator" else image)


def base_images(source: Path) -> dict[str, str]:
    references = set()
    for image in ("backend", "frontend", "worker"):
        for line in (context_path(source, image) / "Dockerfile").read_text().splitlines():
            if line.upper().startswith("FROM "):
                references.add(line.split()[1])
    digests = {}
    for reference in sorted(references):
        docker("pull", "--platform", "linux/amd64", reference)
        digests[reference] = docker("image", "inspect", reference, "--format", "{{.Id}}")
    return digests


def input_digest(source: Path, image: str, bases: dict[str, str]) -> str:
    # Hash the complete immutable context, including Dockerignore and deletions.
    # This is deliberately conservative: no Git diff heuristic may hide an input.
    context = context_path(source, image)
    digest = hashlib.sha256(f"massar-image-input-v1:{image}:linux/amd64".encode())
    dockerfile = (context / "Dockerfile").read_text()
    references = {line.split()[1] for line in dockerfile.splitlines() if line.upper().startswith("FROM ")}
    used_bases = {ref: identity for ref, identity in bases.items() if ref in references}
    digest.update(json.dumps(used_bases, sort_keys=True).encode())
    if image == "frontend":
        digest.update(json.dumps(FRONTEND_ARGS, sort_keys=True).encode())
    paths = [*context.rglob("*"), source / "deploy/production/scripts/optimized_builder.py"]
    for path in sorted(paths):
        if path.is_symlink():
            raise RuntimeError("image inputs must not contain symlinks")
        if path.is_file():
            digest.update(path.relative_to(source).as_posix().encode() + b"\0")
            digest.update(hashlib.sha256(path.read_bytes()).digest())
    return digest.hexdigest()


def build_image(source: Path, image: str, fingerprint: str, release: str) -> str:
    arguments = ["build", "--pull=false", "--platform", "linux/amd64"]
    if image in {"backend", "migrator"}:
        arguments += ["--target", image]
    if image == "frontend":
        # Web-vitals identify the artifact's source, so unrelated releases can reuse it.
        for key, setting in {**FRONTEND_ARGS, "NEXT_PUBLIC_RELEASE_ID": f"src-{fingerprint[:40]}"}.items():
            arguments += ["--build-arg", f"{key}={setting}"]
    tag = f"massar/{image}:{release}"
    arguments += ["--tag", tag, "--file", str(context_path(source, image) / "Dockerfile"), str(context_path(source, image))]
    docker(*arguments)
    return tag


def save_json(path: Path, document: dict) -> None:
    pending = path.with_suffix(".tmp")
    pending.write_text(json.dumps(document, sort_keys=True) + "\n")
    os.chmod(pending, 0o600)
    pending.replace(path)


def publish_image(source: Path, image: str, release: str, bases: dict) -> dict:
    endpoint = registry_endpoint()
    fingerprint = input_digest(source, image, bases)
    index = CACHE / f"{image}-{fingerprint}.json"
    started = time.monotonic()
    if index.exists():
        if index.is_symlink() or index.stat().st_uid != 0 or index.stat().st_mode & 0o077:
            raise RuntimeError("unsafe image cache index")
        entry = json.loads(index.read_text())
        if entry.get("inputSha256") != fingerprint or not DIGEST.fullmatch(entry.get("registryDigest", "")):
            raise RuntimeError("image cache provenance mismatch")
        registry_digest, identity = registry_manifest(endpoint, image, entry["registryDigest"])
        if identity != entry.get("imageDigest") or registry_digest != entry["registryDigest"]:
            raise RuntimeError("cached image differs from the verified registry artifact")
        docker("pull", f"{endpoint}/massar/{image}@{registry_digest}")
        disposition = "reused"
    else:
        tag = build_image(source, image, fingerprint, release)
        identity = docker("image", "inspect", tag, "--format", "{{.Id}}")
        registry_tag = f"{endpoint}/massar/{image}:{release}"
        docker("tag", tag, registry_tag)
        docker("push", registry_tag)
        registry_digest, registry_identity = registry_manifest(endpoint, image, release)
        if identity != registry_identity:
            raise RuntimeError("published image configuration does not match the build")
        entry = {"imageDigest": identity, "registryDigest": registry_digest, "inputSha256": fingerprint}
        save_json(index, entry)
        disposition = "built"
    docker("tag", identity, f"massar/{image}:{release}")
    if docker("image", "inspect", identity, "--format", "{{.Architecture}}/{{.Os}}") != "amd64/linux":
        raise RuntimeError("image platform differs from the release contract")
    return {**entry, "disposition": disposition, "elapsedSeconds": round(time.monotonic() - started, 3)}


def execute(workspace: Path, release: str, source_sha256: str) -> dict:
    source = workspace / "source"
    installed = Path(__file__).read_bytes()
    if installed != (source / "deploy/production/scripts/optimized_builder.py").read_bytes():
        raise RuntimeError("install the reviewed optimized builder before building this source")
    CACHE.mkdir(mode=0o700, parents=True, exist_ok=True)
    with (CACHE / "build.lock").open("a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        bases = base_images(source)
        entries = {name: publish_image(source, name, release, bases) for name in IMAGES}
    manifest = {
        "schemaVersion": 2, "status": "success", "clusterId": "massar-production",
        "builderNodeId": "node-3", "releaseId": release, "sourceStateSha256": source_sha256,
        "platform": "linux/amd64", "registryEndpoint": registry_endpoint(),
        "images": {name: entry["imageDigest"] for name, entry in entries.items()}, "artifacts": entries,
    }
    save_json(workspace / "builder-manifest.json", manifest)
    save_json(workspace / "build-evidence.json", {"status": "success", "releaseId": release, "images": entries})
    return manifest


def benchmark_reuse(workspace: Path, release: str) -> dict:
    """Verify a warm run without creating a new release or changing its manifest."""
    if Path(__file__).read_bytes() != (workspace / "source/deploy/production/scripts/optimized_builder.py").read_bytes():
        raise RuntimeError("benchmark requires the exact installed build policy")
    original = json.loads((workspace / "builder-manifest.json").read_text())
    if original.get("schemaVersion") != 2 or original.get("releaseId") != release:
        raise RuntimeError("benchmark requires an optimized build of this exact release")
    started = time.monotonic()
    with (CACHE / "build.lock").open("a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        bases = base_images(workspace / "source")
        for image in IMAGES:
            fingerprint = input_digest(workspace / "source", image, bases)
            if fingerprint != original["artifacts"][image]["inputSha256"] or not (CACHE / f"{image}-{fingerprint}.json").is_file():
                raise RuntimeError("inputs or cache changed; benchmark refuses to rebuild an image")
        entries = {image: publish_image(workspace / "source", image, release, bases) for image in IMAGES}
    if any(entry["disposition"] != "reused" or entry["imageDigest"] != original["images"][image] for image, entry in entries.items()):
        raise RuntimeError("warm run did not reuse the exact verified images")
    evidence = {"status": "success", "releaseId": release, "imageBuilds": 0,
                "elapsedSeconds": round(time.monotonic() - started, 3), "images": entries}
    save_json(workspace / "reuse-benchmark.json", evidence)
    return evidence
