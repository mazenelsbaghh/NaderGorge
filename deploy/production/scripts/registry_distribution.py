#!/usr/bin/env python3
"""Pull content-addressed image layers over the inventory's private network."""
from __future__ import annotations

import re
from pathlib import Path

from release_images import file_sha256
from ssh_transport import SshTarget

IMAGES = {"backend", "frontend", "worker", "migrator"}
SHA256 = re.compile(r"[0-9a-f]{64}")
DIGEST = re.compile(r"sha256:[0-9a-f]{64}")


def validate_builder(manifest: dict, inventory: object, provenance: dict) -> dict:
    expected = {"schemaVersion", "status", "clusterId", "builderNodeId", "releaseId", "sourceStateSha256", "platform", "registryEndpoint", "images", "artifacts"}
    nodes = tuple(inventory.nodes)
    if tuple(node.id for node in nodes) != ("node-1", "node-2", "node-3"):
        raise ValueError("registry distribution requires the exact three-node inventory")
    if (set(manifest) != expected or manifest["schemaVersion"] != 2
        or manifest["status"] != "success" or manifest["clusterId"] != "massar-production"
        or manifest["builderNodeId"] != "node-3" or manifest["platform"] != "linux/amd64"
        or manifest["registryEndpoint"] != f"{nodes[2].overlay_address}:5443"
        or manifest["releaseId"] != provenance["releaseId"]
        or manifest["sourceStateSha256"] != provenance["sourceStateSha256"]
        or set(manifest["images"]) != IMAGES or set(manifest["artifacts"]) != IMAGES):
        raise ValueError("registry builder provenance differs from the requested release")
    artifacts = {}
    for image in sorted(IMAGES):
        entry = manifest["artifacts"][image]
        if (set(entry) != {"imageDigest", "registryDigest", "inputSha256", "disposition", "elapsedSeconds"}
            or entry["imageDigest"] != manifest["images"][image]
            or not DIGEST.fullmatch(entry["imageDigest"])
            or not DIGEST.fullmatch(entry["registryDigest"])
            or not SHA256.fullmatch(entry["inputSha256"])
            or entry["disposition"] not in {"built", "reused"}
            or not isinstance(entry["elapsedSeconds"], (int, float)) or entry["elapsedSeconds"] < 0):
            raise ValueError(f"registry artifact is invalid: {image}")
        artifacts[image] = {key: entry[key] for key in ("imageDigest", "registryDigest", "inputSha256")}
    return artifacts


class RegistryDistributionRunner:
    def __init__(self, inventory, transport, builder: dict, directory: Path):
        self.inventory, self.transport, self.builder, self.directory = inventory, transport, builder, directory

    def run(self) -> dict:
        bundle = self.directory / "release-files.tar.gz"
        manifest = self.directory / "manifest.json"
        bundle_sha, manifest_sha = file_sha256(bundle), file_sha256(manifest)
        release = self.builder["releaseId"]
        verified = {}
        for node in self.inventory.nodes:
            target = SshTarget(node.id, node.public_address, self.inventory.cluster["ssh_user"])
            self.pull_images(target)
            stage = f"/tmp/massar-{release}"
            script = f"test \"$(cat /opt/massar/releases/{release}/.release-files.sha256)\" = '{bundle_sha}'"
            existing = self.transport.run(target, ("bash", "-lc", script), timeout_seconds=30, check=False)
            if existing.returncode != 0:
                self.transport.run(target, ("install", "-d", "-m", "0700", stage), timeout_seconds=30)
                try:
                    self.transport.copy(target, bundle, f"{stage}/release-files.tar.gz", timeout_seconds=600)
                    self.transport.copy(target, manifest, f"{stage}/manifest.json", timeout_seconds=120)
                    self.transport.run(target, ("sudo", "/usr/local/sbin/massar-install-immutable-release", "install-release", release, bundle_sha, manifest_sha), timeout_seconds=300)
                finally:
                    self.transport.run(target, ("rm", "-rf", "--", stage), timeout_seconds=30)
            verified[node.id] = {"status": "verified", "releaseFilesSha256": bundle_sha}
        return {"digestParity": True, "distribution": verified}

    def pull_images(self, target: SshTarget) -> None:
        for image, identity in self.builder["images"].items():
            digest = self.builder["artifacts"][image]["registryDigest"]
            reference = f"{self.builder['registryEndpoint']}/massar/{image}@{digest}"
            tag = f"massar/{image}:{self.builder['releaseId']}"
            # Docker checks manifest/blob digests, and we independently check the config ID.
            script = (
                "set -euo pipefail; "
                f"if ! test \"$(sudo /usr/bin/docker image inspect {identity} --format '{{{{.Id}}}}' 2>/dev/null)\" = '{identity}'; then "
                f"sudo /usr/bin/docker pull {reference} >/dev/null; fi; "
                f"test \"$(sudo /usr/bin/docker image inspect {identity} --format '{{{{.Id}}}}')\" = '{identity}'; "
                f"test \"$(sudo /usr/bin/docker image inspect {identity} --format '{{{{.Architecture}}}}/{{{{.Os}}}}')\" = amd64/linux; "
                f"sudo /usr/bin/docker tag {identity} {tag}"
            )
            self.transport.run(target, ("bash", "-lc", script), timeout_seconds=1800)
