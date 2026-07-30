#!/usr/bin/env python3
"""Injected-transport runner for verified remote-builder distribution."""
from __future__ import annotations
from pathlib import Path
from remote_distribution_plan import NODE_IDS, RemoteDistributionPlan
from release_images import file_sha256
from ssh_transport import SshTarget

class RemoteDistributionRunError(RuntimeError): pass

def builder_executor_command(plan: object) -> tuple[str, ...]:
    root = f"/var/lib/massar/builds/{getattr(plan, 'release_id')}"
    staging = f"/tmp/massar-build-source-{getattr(plan, 'release_id')}"
    return ("sudo", "/usr/local/sbin/massar-remote-builder", "--workspace", root, "--source-staging", staging, "--release", str(getattr(plan, "release_id")), "--source-sha256", str(getattr(plan, "source_state_sha256")), "--yes")

class RemoteDistributionRunner:
    def __init__(self, *, inventory: object, transport: object, plan: RemoteDistributionPlan, bundle: Path, manifest: Path) -> None:
        self.inventory, self.transport, self.plan, self.bundle, self.manifest = inventory, transport, plan, bundle, manifest
        self.nodes = tuple(getattr(inventory, "nodes", ()))
        if not bundle.is_file() or not manifest.is_file() or tuple(node.id for node in self.nodes) != NODE_IDS:
            raise RemoteDistributionRunError("runner requires regular local bundle/manifest and exact inventory")
        self.builder = next((node for node in self.nodes if node.id == plan.builder_node_id), None)
        if self.builder is None: raise RemoteDistributionRunError("approved builder is absent")
    def target(self, node: object) -> SshTarget:
        return SshTarget(node.id, node.public_address, self.inventory.cluster["ssh_user"])

    def has_verified_image(self, target: SshTarget, image: str, digest: str) -> bool:
        result = self.transport.run(
            target,
            (
                "bash",
                "-lc",
                "set -euo pipefail; "
                f"test \"$(sudo /usr/bin/docker image inspect {digest} "
                "--format '{{.Id}}')\" = "
                f"'{digest}'",
            ),
            timeout_seconds=60,
            check=False,
        )
        return getattr(result, "returncode", 1) == 0

    def run(self) -> dict[str, object]:
        verified = {}
        bundle_sha, manifest_sha = file_sha256(self.bundle), file_sha256(self.manifest)
        for node in self.nodes:
            target, stage = self.target(node), f"/tmp/massar-{self.plan.release_id}"
            self.transport.run(target, ("bash", "-lc", f"set -euo pipefail; rm -rf {stage}; install -d -m 0700 {stage}"), timeout_seconds=60)
            self.transport.copy(target, self.bundle, f"{stage}/release-files.tar.gz", timeout_seconds=600)
            self.transport.copy(target, self.manifest, f"{stage}/manifest.json", timeout_seconds=120)
            evidence = {}
            for transfer in self.plan.transfers_for_node(node.id):
                if transfer.source_node_id == node.id:
                    # The builder already owns this image and its immutable
                    # archive.  Relaying it through two SSH sessions back to
                    # the same host adds no durability and can deadlock or
                    # time out under load.  Verify the cache archive and the
                    # already-built image in place instead.
                    script = "set -euo pipefail; " + f"printf '%s  %s\\n' '{transfer.archive_sha256}' '{transfer.source_path}' | sha256sum -c -; " + f"test \"$(sudo /usr/bin/docker image inspect massar/{transfer.image}:{self.plan.release_id} --format '{{{{.Id}}}}')\" = '{transfer.image_digest}'"
                elif self.has_verified_image(target, transfer.image, transfer.image_digest):
                    self.transport.run(
                        target,
                        (
                            "bash",
                            "-lc",
                            "set -euo pipefail; "
                            f"sudo /usr/bin/docker tag {transfer.image_digest} "
                            f"massar/{transfer.image}:{self.plan.release_id}; "
                            f"test \"$(sudo /usr/bin/docker image inspect massar/{transfer.image}:{self.plan.release_id} "
                            "--format '{{.Id}}')\" = "
                            f"'{transfer.image_digest}'",
                        ),
                        timeout_seconds=60,
                    )
                    evidence[transfer.image] = {
                        "archiveSha256": transfer.archive_sha256,
                        "imageDigest": transfer.image_digest,
                    }
                    continue
                else:
                    self.transport.stream_remote_file(self.target(self.builder), str(transfer.source_path), target, str(transfer.target_path))
                    script = "set -euo pipefail; " + f"printf '%s  %s\\n' '{transfer.archive_sha256}' '{transfer.target_path}' | sha256sum -c -; " + f"sudo /usr/bin/docker load --input {transfer.target_path} >/dev/null; " + f"test \"$(sudo /usr/bin/docker image inspect massar/{transfer.image}:{self.plan.release_id} --format '{{{{.Id}}}}')\" = '{transfer.image_digest}'; rm -f {transfer.target_path}"
                self.transport.run(target, ("bash", "-lc", script), timeout_seconds=1800)
                evidence[transfer.image] = {"archiveSha256": transfer.archive_sha256, "imageDigest": transfer.image_digest}
            install = f"set -euo pipefail; printf '%s  %s/release-files.tar.gz\\n' '{bundle_sha}' '{stage}' | sha256sum -c -; printf '%s  %s/manifest.json\\n' '{manifest_sha}' '{stage}' | sha256sum -c -; sudo /usr/local/sbin/massar-install-immutable-release install-release {self.plan.release_id} {bundle_sha} {manifest_sha}"
            self.transport.run(target, ("bash", "-lc", install), timeout_seconds=300)
            verified[node.id] = evidence
        return self.plan.final_manifest(verified, file_sha256(self.bundle))
