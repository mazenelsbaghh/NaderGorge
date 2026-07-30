#!/usr/bin/env python3
"""Collect one byte-identical current release manifest from all three nodes."""

from __future__ import annotations

import argparse
import base64
import binascii
import datetime as dt
import hashlib
import json
import os
import re
import sys
import tempfile
import uuid
from dataclasses import dataclass
from pathlib import Path
from typing import Protocol

from clusterctl import Inventory, Node, load_inventory
from release_contract import (
    ReleaseContractError,
    ReleaseManifest,
    load_release_manifest,
)
from ssh_transport import SshTarget, StrictSshTransport


MAXIMUM_MANIFEST_BYTES = 1024 * 1024
RELEASE = re.compile(
    r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$"
)
HEX_SHA256 = re.compile(r"^[0-9a-f]{64}$")
REMOTE_FIELDS = {
    "schemaVersion",
    "resolutionMode",
    "nodeLabel",
    "releaseId",
    "releaseRoot",
    "manifestPath",
    "manifestSha256",
    "manifestBase64",
    "images",
    "actualImages",
    "releaseFilesSha256",
    "releaseFilesDigestVerified",
}

REMOTE_READER = r"""
import base64,hashlib,json,os,pathlib,re,stat,subprocess,sys

base=pathlib.Path("/opt/massar/releases")
current=pathlib.Path("/opt/massar/current")
expected_node=sys.argv[1]
release_re=re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$")
digest_re=re.compile(r"^sha256:[0-9a-f]{64}$")
hex_re=re.compile(r"^[0-9a-f]{64}$")
maximum=1024*1024
services={"backend":"backend","worker":"worker","landing":"frontend","student":"frontend","admin":"frontend","teacher":"frontend","staff":"frontend","gateway":None}
image_names=("backend","frontend","worker","migrator")

def docker(*args):
    completed=subprocess.run(
        ["/usr/bin/sudo","/usr/bin/docker",*args],
        text=True,capture_output=True,check=False,timeout=20,
    )
    if completed.returncode != 0:
        raise SystemExit("docker inspection failed")
    return completed.stdout

container_ids=[
    line.strip() for line in docker(
        "ps","-q","--filter","label=com.docker.compose.project=massar_production"
    ).splitlines() if line.strip()
]
if not container_ids:
    raise SystemExit("no Massar production containers were found")
try:
    inspected=json.loads(docker("inspect",*container_ids))
except json.JSONDecodeError:
    raise SystemExit("docker container inspection returned invalid JSON")
by_service={}
release_labels=set()
node_labels=set()
running_image_ids={}
for item in inspected:
    labels=item.get("Config",{}).get("Labels") or {}
    service=labels.get("com.docker.compose.service")
    if service not in services:
        continue
    if service in by_service:
        raise SystemExit("multiple containers exist for one required service")
    state=item.get("State") or {}
    health=(state.get("Health") or {}).get("Status")
    if state.get("Status")!="running" or health!="healthy":
        raise SystemExit("a required production service is not running and healthy")
    release_label=labels.get("net.massar.release")
    node_label=labels.get("net.massar.node")
    if not isinstance(release_label,str) or not release_re.fullmatch(release_label):
        raise SystemExit("a required service has an invalid release label")
    if node_label!=expected_node:
        raise SystemExit("a required service has the wrong node label")
    image_id=item.get("Image")
    if not isinstance(image_id,str) or not digest_re.fullmatch(image_id):
        raise SystemExit("a required service has an invalid running image ID")
    by_service[service]=item
    release_labels.add(release_label)
    node_labels.add(node_label)
    expected_image=services[service]
    if expected_image is not None:
        previous=running_image_ids.setdefault(expected_image,image_id)
        if previous!=image_id:
            raise SystemExit("services using the same release image have different image IDs")
if set(by_service)!=set(services):
    raise SystemExit("the exact eight required production services are not present")
if len(release_labels)!=1 or node_labels!={expected_node}:
    raise SystemExit("production service release or node labels diverge")
running_release=next(iter(release_labels))

for fixed in (pathlib.Path("/opt"), pathlib.Path("/opt/massar"), base):
    info=os.lstat(fixed)
    if stat.S_ISLNK(info.st_mode) or not stat.S_ISDIR(info.st_mode):
        raise SystemExit("fixed release parent is not a real directory")
if os.path.lexists(current):
    resolution_mode="current-pointer"
    current_info=os.lstat(current)
    if not stat.S_ISLNK(current_info.st_mode):
        raise SystemExit("current release pointer must be a symlink")
    raw_target=os.readlink(current)
    candidate=pathlib.Path(raw_target)
    if not candidate.is_absolute():
        candidate=current.parent/candidate
    release_root=candidate.resolve(strict=True)
    if release_root.parent != base or not release_re.fullmatch(release_root.name):
        raise SystemExit("current release pointer escapes the exact release root")
    if release_root.name!=running_release:
        raise SystemExit("current release pointer and Docker release labels diverge")
else:
    resolution_mode="docker-label-fallback"
    release_root=base/running_release
root_info=os.lstat(release_root)
if stat.S_ISLNK(root_info.st_mode) or not stat.S_ISDIR(root_info.st_mode):
    raise SystemExit("resolved release root must be a real directory")
manifest_path=release_root/"manifest.json"
manifest_info=os.lstat(manifest_path)
if stat.S_ISLNK(manifest_info.st_mode) or not stat.S_ISREG(manifest_info.st_mode):
    raise SystemExit("release manifest must be a regular non-symlink file")
if manifest_info.st_size <= 0 or manifest_info.st_size > maximum:
    raise SystemExit("release manifest size is outside the safe bound")
flags=os.O_RDONLY|getattr(os,"O_NOFOLLOW",0)
descriptor=os.open(manifest_path,flags)
try:
    data=b""
    while len(data)<=maximum:
        chunk=os.read(descriptor,min(65536,maximum+1-len(data)))
        if not chunk:
            break
        data+=chunk
finally:
    os.close(descriptor)
if not data or len(data)>maximum:
    raise SystemExit("release manifest size changed or exceeded the safe bound")
value=json.loads(data.decode("utf-8"))
common={
 "schemaVersion","releaseId","createdAt","platform","images","status",
 "nodeCount","digestParity","distribution",
}
source_required=common|{"gitCommit","sourceStateSha256","dirtySourceSnapshot"}
legacy_required=common|{"sealedLegacyProvenance"}
images=value.get("images")
if (
    not isinstance(value,dict)
    or set(value) not in (source_required,legacy_required)
    or value.get("releaseId")!=release_root.name
    or not release_re.fullmatch(str(value.get("releaseId","")))
    or not isinstance(images,dict)
    or set(images) not in (
        {"backend","frontend","worker","migrator"},
        {"backend","frontend","worker"},
    )
    or any(not isinstance(item,str) or not digest_re.fullmatch(item)
           for item in images.values())
):
    raise SystemExit("release manifest identity or image digests are invalid")
if set(value)==legacy_required:
    provenance=value["sealedLegacyProvenance"]
    if (
        not value["releaseId"].startswith("prod-")
        or not isinstance(provenance,dict)
        or set(provenance)!={"schemaVersion","type","sealedAt","runtimeBundleSha256","runtimeBundleDigestAlgorithm","sourceReleaseLabel"}
        or provenance.get("schemaVersion")!=2
        or provenance.get("type")!="sealed-legacy-bootstrap"
        or provenance.get("sourceReleaseLabel")!=value["releaseId"]
        or provenance.get("runtimeBundleDigestAlgorithm")!="massar-runtime-bundle-sha256-v1"
        or not hex_re.fullmatch(str(provenance.get("runtimeBundleSha256","")))
    ):
        raise SystemExit("sealed Legacy provenance is invalid")
    if set(images)!={"backend","frontend","worker"}:
        raise SystemExit("sealed Legacy runtime image set is invalid")
else:
    if set(images)!={"backend","frontend","worker","migrator"}:
        raise SystemExit("source-built image set is invalid")
image_names=tuple(sorted(images))
if value["releaseId"]!=running_release:
    raise SystemExit("manifest and Docker release labels diverge")
actual_images={}
for image_name in image_names:
    tag="massar/%s:%s"%(image_name,running_release)
    try:
        image_inspect=json.loads(docker("image","inspect",tag))
    except json.JSONDecodeError:
        raise SystemExit("docker image inspection returned invalid JSON")
    if not isinstance(image_inspect,list) or len(image_inspect)!=1:
        raise SystemExit("a required tagged release image is missing")
    image_id=image_inspect[0].get("Id")
    if not isinstance(image_id,str) or not digest_re.fullmatch(image_id):
        raise SystemExit("a required tagged release image has an invalid ID")
    actual_images[image_name]=image_id
if actual_images!=images:
    raise SystemExit("actual tagged image IDs do not match the release manifest")
for service,image_name in services.items():
    if image_name is not None and running_image_ids[image_name]!=actual_images[image_name]:
        raise SystemExit("a running service image does not match its release tag")
release_file_values={
    node.get("releaseFilesSha256")
    for node in value["distribution"].values()
    if isinstance(node,dict)
}
expected_release_files=(
    next(iter(release_file_values))
    if len(release_file_values)==1 else None
)
sidecar=release_root/".release-files.sha256"
release_files_sha=None
release_files_verified=False
if os.path.lexists(sidecar):
    sidecar_info=os.lstat(sidecar)
    if (
        stat.S_ISLNK(sidecar_info.st_mode)
        or not stat.S_ISREG(sidecar_info.st_mode)
        or sidecar_info.st_size<=0
        or sidecar_info.st_size>128
    ):
        raise SystemExit("release-files digest sidecar must be a regular non-symlink file")
    sidecar_descriptor=os.open(sidecar,flags)
    try:
        sidecar_data=os.read(sidecar_descriptor,129)
    finally:
        os.close(sidecar_descriptor)
    try:
        release_files_sha=sidecar_data.decode("ascii").strip()
    except UnicodeDecodeError:
        raise SystemExit("release-files digest sidecar is not ASCII")
    if (
        not hex_re.fullmatch(release_files_sha)
        or release_files_sha!=expected_release_files
    ):
        raise SystemExit("release-files digest sidecar does not match the manifest")
    release_files_verified=True
print(json.dumps({
 "schemaVersion":1,
 "resolutionMode":resolution_mode,
 "nodeLabel":expected_node,
 "releaseId":value["releaseId"],
 "releaseRoot":str(release_root),
 "manifestPath":str(manifest_path),
 "manifestSha256":hashlib.sha256(data).hexdigest(),
 "manifestBase64":base64.b64encode(data).decode("ascii"),
 "images":images,
 "actualImages":actual_images,
 "releaseFilesSha256":release_files_sha,
 "releaseFilesDigestVerified":release_files_verified,
},separators=(",",":")))
"""


class ManifestCollectionError(RuntimeError):
    """Raised when current release identity cannot be proven safely."""


class Transport(Protocol):
    def run(
        self,
        target: SshTarget,
        remote_argv: tuple[str, ...],
        *,
        timeout_seconds: int = 60,
        check: bool = True,
    ): ...


@dataclass(frozen=True)
class NodeManifest:
    node_id: str
    resolution_mode: str
    node_label: str
    release_id: str
    release_root: str
    manifest_path: str
    sha256: str
    content: bytes
    images: dict[str, str]
    actual_images: dict[str, str]
    release_files_sha256: str | None
    release_files_digest_verified: bool


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def ssh_target(inventory: Inventory, node: Node) -> SshTarget:
    return SshTarget(
        node.id,
        node.public_address,
        str(inventory.cluster["ssh_user"]),
    )


def parse_remote(node_id: str, stdout: str) -> NodeManifest:
    lines = [line for line in stdout.splitlines() if line.strip()]
    if len(lines) != 1:
        raise ManifestCollectionError(f"{node_id} did not return exactly one manifest envelope")
    try:
        value = json.loads(lines[0])
    except json.JSONDecodeError as exc:
        raise ManifestCollectionError(f"{node_id} returned invalid JSON") from exc
    if (
        not isinstance(value, dict)
        or set(value) != REMOTE_FIELDS
        or value.get("schemaVersion") != 1
    ):
        raise ManifestCollectionError(f"{node_id} envelope fields are invalid")
    release_id = value["releaseId"]
    release_root = value["releaseRoot"]
    manifest_path = value["manifestPath"]
    digest = value["manifestSha256"]
    images = value["images"]
    actual_images = value["actualImages"]
    resolution_mode = value["resolutionMode"]
    node_label = value["nodeLabel"]
    release_files_sha256 = value["releaseFilesSha256"]
    release_files_digest_verified = value["releaseFilesDigestVerified"]
    if (
        resolution_mode not in {"current-pointer", "docker-label-fallback"}
        or node_label != node_id
        or not isinstance(release_id, str)
        or not RELEASE.fullmatch(release_id)
        or release_root != f"/opt/massar/releases/{release_id}"
        or manifest_path != f"/opt/massar/releases/{release_id}/manifest.json"
        or not isinstance(digest, str)
        or not HEX_SHA256.fullmatch(digest)
        or not isinstance(images, dict)
        or set(images) not in (
            {"backend", "frontend", "worker", "migrator"},
            {"backend", "frontend", "worker"},
        )
        or any(
            not isinstance(image, str)
            or not re.fullmatch(r"sha256:[0-9a-f]{64}", image)
            for image in images.values()
        )
        or not isinstance(actual_images, dict)
        or actual_images != images
        or not isinstance(release_files_digest_verified, bool)
        or (
            release_files_sha256 is not None
            and (
                not isinstance(release_files_sha256, str)
                or not HEX_SHA256.fullmatch(release_files_sha256)
            )
        )
        or release_files_digest_verified != (release_files_sha256 is not None)
    ):
        raise ManifestCollectionError(f"{node_id} release path or digest identity is invalid")
    encoded = value["manifestBase64"]
    if not isinstance(encoded, str) or len(encoded) > MAXIMUM_MANIFEST_BYTES * 2:
        raise ManifestCollectionError(f"{node_id} manifest payload is outside the safe bound")
    try:
        content = base64.b64decode(encoded, validate=True)
    except (ValueError, binascii.Error) as exc:
        raise ManifestCollectionError(f"{node_id} manifest payload is not valid base64") from exc
    if (
        not content
        or len(content) > MAXIMUM_MANIFEST_BYTES
        or hashlib.sha256(content).hexdigest() != digest
    ):
        raise ManifestCollectionError(f"{node_id} manifest bytes do not match the claimed SHA-256")
    return NodeManifest(
        node_id=node_id,
        resolution_mode=resolution_mode,
        node_label=node_label,
        release_id=release_id,
        release_root=release_root,
        manifest_path=manifest_path,
        sha256=digest,
        content=content,
        images={name: str(images[name]) for name in sorted(images)},
        actual_images={
            name: str(actual_images[name]) for name in sorted(actual_images)
        },
        release_files_sha256=release_files_sha256,
        release_files_digest_verified=release_files_digest_verified,
    )


def validate_manifest_bytes(content: bytes, expected_release: str) -> ReleaseManifest:
    with tempfile.TemporaryDirectory(prefix="massar-current-manifest-") as directory:
        path = Path(directory) / "manifest.json"
        path.write_bytes(content)
        path.chmod(0o600)
        return load_release_manifest(path, expected_release)


def ensure_output_target(
    path: Path,
    label: str,
    *,
    create_parent: bool = True,
) -> Path:
    expanded = path.expanduser()
    if ".." in expanded.parts:
        raise ManifestCollectionError(f"{label} output must not contain traversal")
    absolute = expanded.absolute()
    current = Path(absolute.anchor)
    for part in absolute.parts[1:-1]:
        current /= part
        if not os.path.lexists(current):
            continue
        if current.is_symlink() or not current.is_dir():
            raise ManifestCollectionError(
                f"{label} output parent contains a symlink or non-directory"
            )
    if os.path.lexists(expanded):
        raise ManifestCollectionError(f"{label} output already exists")
    parent = absolute.parent
    if create_parent:
        parent.mkdir(parents=True, exist_ok=True)
    elif parent.exists() and (parent.is_symlink() or not parent.is_dir()):
        raise ManifestCollectionError(f"{label} parent must be a real directory")
    if not create_parent and not parent.exists():
        return absolute
    if parent.is_symlink() or not parent.is_dir():
        raise ManifestCollectionError(f"{label} parent must be a real directory")
    resolved_parent = parent.resolve()
    destination = resolved_parent / expanded.name
    if os.path.lexists(destination):
        raise ManifestCollectionError(f"{label} output already exists")
    return destination


def stage_file(destination: Path, content: bytes) -> Path:
    temporary = destination.with_name(f".{destination.name}.{uuid.uuid4().hex}.tmp")
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL
    if hasattr(os, "O_NOFOLLOW"):
        flags |= os.O_NOFOLLOW
    descriptor = os.open(temporary, flags, 0o600)
    try:
        with os.fdopen(descriptor, "wb", closefd=True) as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise
    return temporary


def publish_without_overwrite(
    temporary: Path,
    destination: Path,
) -> tuple[int, int]:
    created_identity: tuple[int, int] | None = None
    try:
        os.link(temporary, destination, follow_symlinks=False)
        created = os.lstat(destination)
        created_identity = (created.st_dev, created.st_ino)
        os.chmod(destination, 0o640, follow_symlinks=False)
        temporary.unlink()
        directory_fd = os.open(destination.parent, os.O_RDONLY)
        try:
            os.fsync(directory_fd)
        finally:
            os.close(directory_fd)
    except FileExistsError as exc:
        raise ManifestCollectionError(f"output appeared concurrently: {destination.name}") from exc
    except BaseException:
        if created_identity is not None:
            try:
                current = os.lstat(destination)
                if (current.st_dev, current.st_ino) == created_identity:
                    destination.unlink()
            except FileNotFoundError:
                pass
        raise
    assert created_identity is not None
    return created_identity


def collect(
    *,
    inventory: Inventory,
    transport: Transport,
    manifest_output: Path,
    evidence_output: Path,
) -> dict[str, object]:
    manifest_destination = ensure_output_target(manifest_output, "manifest")
    evidence_destination = ensure_output_target(evidence_output, "evidence")
    if manifest_destination == evidence_destination:
        raise ManifestCollectionError("manifest and evidence outputs must differ")

    node_manifests: list[NodeManifest] = []
    for node in inventory.nodes:
        completed = transport.run(
            ssh_target(inventory, node),
            ("python3", "-c", REMOTE_READER, node.id),
            timeout_seconds=30,
        )
        node_manifests.append(parse_remote(node.id, completed.stdout))
    if [item.node_id for item in node_manifests] != ["node-1", "node-2", "node-3"]:
        raise ManifestCollectionError("collector did not inspect the exact three nodes")
    first = node_manifests[0]
    if any(
        item.release_id != first.release_id
        or item.sha256 != first.sha256
        or item.content != first.content
        or item.images != first.images
        or item.actual_images != first.actual_images
        or item.resolution_mode != first.resolution_mode
        for item in node_manifests[1:]
    ):
        raise ManifestCollectionError(
            "current release manifest bytes, SHA-256, release, or image digests differ"
        )
    validated = validate_manifest_bytes(first.content, first.release_id)
    if validated.sha256 != first.sha256 or validated.images != first.images:
        raise ManifestCollectionError("strict local manifest validation disagrees with nodes")

    evidence = {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": "massar-production",
        "capturedAt": utc_now(),
        "releaseId": first.release_id,
        "manifestSha256": first.sha256,
        "images": first.images,
        "nodeCount": 3,
        "byteParity": True,
        "nodes": {
            item.node_id: {
                "releaseRoot": item.release_root,
                "manifestPath": item.manifest_path,
                "manifestSha256": item.sha256,
                "resolutionMode": item.resolution_mode,
                "nodeLabel": item.node_label,
                "actualImages": item.actual_images,
                "releaseFilesSha256": item.release_files_sha256,
                "releaseFilesDigestVerified": item.release_files_digest_verified,
            }
            for item in node_manifests
        },
    }
    manifest_temporary = stage_file(manifest_destination, first.content)
    evidence_temporary = stage_file(
        evidence_destination,
        (json.dumps(evidence, indent=2, sort_keys=True) + "\n").encode("utf-8"),
    )
    published_manifest: tuple[int, int] | None = None
    try:
        published_manifest = publish_without_overwrite(
            manifest_temporary,
            manifest_destination,
        )
        publish_without_overwrite(evidence_temporary, evidence_destination)
    except BaseException:
        manifest_temporary.unlink(missing_ok=True)
        evidence_temporary.unlink(missing_ok=True)
        if published_manifest is not None:
            try:
                current = os.lstat(manifest_destination)
                if (current.st_dev, current.st_ino) == published_manifest:
                    manifest_destination.unlink()
            except FileNotFoundError:
                pass
        raise
    return evidence


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--manifest-output", required=True, type=Path)
    parser.add_argument("--evidence-output", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args(argv)
    try:
        inventory = load_inventory(args.inventory)
        if args.dry_run:
            manifest_destination = ensure_output_target(
                args.manifest_output,
                "manifest",
                create_parent=False,
            )
            evidence_destination = ensure_output_target(
                args.evidence_output,
                "evidence",
                create_parent=False,
            )
            if manifest_destination == evidence_destination:
                raise ManifestCollectionError("manifest and evidence outputs must differ")
            print(json.dumps({
                "status": "dry-run",
                "nodes": [node.id for node in inventory.nodes],
                "manifestOutput": str(manifest_destination),
                "evidenceOutput": str(evidence_destination),
                "sshAttempted": False,
            }))
            return 0
        manifest_destination = ensure_output_target(args.manifest_output, "manifest")
        evidence_destination = ensure_output_target(args.evidence_output, "evidence")
        if manifest_destination == evidence_destination:
            raise ManifestCollectionError("manifest and evidence outputs must differ")
        transport = StrictSshTransport(args.known_hosts, args.identity)
        evidence = collect(
            inventory=inventory,
            transport=transport,
            manifest_output=manifest_destination,
            evidence_output=evidence_destination,
        )
        print(json.dumps({
            "status": "success",
            "releaseId": evidence["releaseId"],
            "manifestSha256": evidence["manifestSha256"],
            "manifestOutput": str(manifest_destination),
            "evidenceOutput": str(evidence_destination),
        }))
        return 0
    except (
        ManifestCollectionError,
        ReleaseContractError,
        OSError,
        ValueError,
        json.JSONDecodeError,
    ) as exc:
        print(f"current release manifest collection blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
