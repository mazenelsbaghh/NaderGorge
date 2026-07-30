#!/usr/bin/env python3
"""Pure contract for distributing remote-builder artifacts without local tars."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import Any, Mapping

from remote_build_release import BUILDER_NODE_ID, IMAGES, RELEASE_RE, SHA256_RE


DIGEST_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
NODE_IDS = ("node-1", "node-2", "node-3")


class RemoteDistributionError(ValueError):
    pass


@dataclass(frozen=True)
class RemoteArtifactTransfer:
    """One remote-only artifact relay and its post-load verification contract."""

    image: str
    source_node_id: str
    source_path: PurePosixPath
    target_node_id: str
    target_path: PurePosixPath
    archive_sha256: str
    image_digest: str


@dataclass(frozen=True)
class RemoteDistributionPlan:
    release_id: str
    source_state_sha256: str
    builder_node_id: str
    images: dict[str, str]
    transfers: tuple[RemoteArtifactTransfer, ...]

    def transfers_for_node(self, node_id: str) -> tuple[RemoteArtifactTransfer, ...]:
        if node_id not in NODE_IDS:
            raise RemoteDistributionError("distribution target must be an approved node")
        return tuple(transfer for transfer in self.transfers if transfer.target_node_id == node_id)

    def remaining_nodes(self, verified_nodes: set[str]) -> tuple[str, ...]:
        if not verified_nodes.issubset(set(NODE_IDS)):
            raise RemoteDistributionError("resume evidence contains an unapproved node")
        return tuple(node_id for node_id in NODE_IDS if node_id not in verified_nodes)

    def final_manifest(
        self,
        verification: Mapping[str, Mapping[str, Mapping[str, str]]],
        release_files_sha256: str,
    ) -> dict[str, Any]:
        """Return final-manifest input only after every node proves every byte."""
        if not SHA256_RE.fullmatch(release_files_sha256):
            raise RemoteDistributionError("final manifest requires the release bundle SHA-256")
        if set(verification) != set(NODE_IDS):
            raise RemoteDistributionError("final manifest requires verification from all three nodes")
        distribution: dict[str, dict[str, Any]] = {}
        for node_id in NODE_IDS:
            node_evidence = verification[node_id]
            if set(node_evidence) != set(IMAGES):
                raise RemoteDistributionError(f"{node_id} verification must cover exactly four images")
            for transfer in self.transfers_for_node(node_id):
                observed = node_evidence[transfer.image]
                if (
                    observed.get("archiveSha256") != transfer.archive_sha256
                    or observed.get("imageDigest") != transfer.image_digest
                ):
                    raise RemoteDistributionError(
                        f"{node_id} did not verify {transfer.image} archive and image digest"
                    )
            distribution[node_id] = {
                "status": "verified",
                "releaseFilesSha256": release_files_sha256,
            }
        return {
            "digestParity": True,
            "distribution": distribution,
        }


def _approved_nodes(inventory: object) -> tuple[object, ...]:
    nodes = tuple(getattr(inventory, "nodes", ()))
    if tuple(str(getattr(node, "id", "")) for node in nodes) != NODE_IDS:
        raise RemoteDistributionError("distribution requires the exact three-node inventory")
    builders = tuple(node for node in nodes if "builder" in tuple(getattr(node, "roles", ())))
    if len(builders) != 1 or str(getattr(builders[0], "id", "")) != BUILDER_NODE_ID:
        raise RemoteDistributionError("distribution requires node-3 as the exactly one builder")
    return nodes


def _builder_manifest(value: Mapping[str, Any]) -> tuple[str, str, dict[str, str], dict[str, Mapping[str, str]]]:
    release_id = value.get("releaseId")
    source_sha256 = value.get("sourceStateSha256")
    images = value.get("images")
    artifacts = value.get("artifacts")
    if (
        value.get("schemaVersion") != 1
        or value.get("status") != "success"
        or value.get("clusterId") != "massar-production"
        or value.get("builderNodeId") != BUILDER_NODE_ID
        or value.get("platform") != "linux/amd64"
        or not isinstance(release_id, str)
        or not RELEASE_RE.fullmatch(release_id)
        or not isinstance(source_sha256, str)
        or not SHA256_RE.fullmatch(source_sha256)
        or not isinstance(images, dict)
        or set(images) != set(IMAGES)
        or not isinstance(artifacts, dict)
        or set(artifacts) != set(IMAGES)
    ):
        raise RemoteDistributionError("builder manifest does not satisfy the remote distribution contract")
    normalized_images = {str(name): str(digest) for name, digest in images.items()}
    if any(not DIGEST_RE.fullmatch(digest) for digest in normalized_images.values()):
        raise RemoteDistributionError("builder manifest contains an invalid image digest")
    normalized_artifacts: dict[str, Mapping[str, str]] = {}
    for name in IMAGES:
        entry = artifacts[name]
        if (
            not isinstance(entry, Mapping)
            or entry.get("filename") != f"{name}.tar"
            or not isinstance(entry.get("sha256"), str)
            or not SHA256_RE.fullmatch(str(entry["sha256"]))
        ):
            raise RemoteDistributionError(f"builder manifest artifact is invalid: {name}")
        normalized_artifacts[name] = {
            "filename": str(entry["filename"]),
            "sha256": str(entry["sha256"]),
        }
    return release_id, source_sha256, normalized_images, normalized_artifacts


def create_remote_distribution_plan(
    inventory: object,
    builder_manifest: Mapping[str, Any],
) -> RemoteDistributionPlan:
    """Map only remote cache paths; no operator archive paths are allowed."""
    _approved_nodes(inventory)
    release_id, source_sha256, images, artifacts = _builder_manifest(builder_manifest)
    source_root = PurePosixPath("/var/lib/massar/builds") / release_id / "artifacts"
    transfers: list[RemoteArtifactTransfer] = []
    for node_id in NODE_IDS:
        target_root = PurePosixPath(f"/tmp/massar-{release_id}")
        for name in IMAGES:
            transfers.append(RemoteArtifactTransfer(
                image=name,
                source_node_id=BUILDER_NODE_ID,
                source_path=source_root / artifacts[name]["filename"],
                target_node_id=node_id,
                target_path=target_root / artifacts[name]["filename"],
                archive_sha256=artifacts[name]["sha256"],
                image_digest=images[name],
            ))
    return RemoteDistributionPlan(
        release_id=release_id,
        source_state_sha256=source_sha256,
        builder_node_id=BUILDER_NODE_ID,
        images=images,
        transfers=tuple(transfers),
    )
