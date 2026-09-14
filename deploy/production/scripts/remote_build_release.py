#!/usr/bin/env python3
"""Pure contract helpers for releases built on the designated production node.

This module intentionally does not open SSH sessions, invoke a container
runtime, or write archives.  The execution step is deliberately separate:
callers first create this immutable plan, then may use strict SSH to stage
source and stream the resulting artifacts between production nodes without
storing image archives on the operator workstation.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import Mapping, Sequence


BUILDER_NODE_ID = "node-3"
IMAGES = ("backend", "frontend", "worker", "migrator")
RELEASE_RE = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")


class RemoteBuildContractError(ValueError):
    """The remote-build contract cannot safely be formed."""


@dataclass(frozen=True)
class RemoteBuildPlan:
    """A side-effect-free description of the remote build boundary."""

    release_id: str
    source_state_sha256: str
    builder_node_id: str
    workspace: PurePosixPath

    @property
    def source_root(self) -> PurePosixPath:
        return self.workspace / "source"

    @property
    def staging_source_root(self) -> PurePosixPath:
        return PurePosixPath(f"/tmp/massar-build-source-{self.release_id}")

    @property
    def artifact_root(self) -> PurePosixPath:
        return self.workspace / "artifacts"

    @property
    def image_archives(self) -> dict[str, PurePosixPath]:
        """Archive destinations on the builder only, never local paths."""
        return {name: self.artifact_root / f"{name}.tar" for name in IMAGES}

    def as_dict(self) -> dict[str, object]:
        return {
            "releaseId": self.release_id,
            "sourceStateSha256": self.source_state_sha256,
            "builderNodeId": self.builder_node_id,
            "workspace": str(self.workspace),
            "sourceRoot": str(self.source_root),
            "stagingSourceRoot": str(self.staging_source_root),
            "artifactRoot": str(self.artifact_root),
            "imageArchives": {
                name: str(path) for name, path in self.image_archives.items()
            },
            "operatorImageArchives": "forbidden",
        }


def _nodes(inventory: object) -> tuple[object, ...]:
    nodes = tuple(getattr(inventory, "nodes", ()))
    if tuple(str(getattr(node, "id", "")) for node in nodes) != (
        "node-1", "node-2", "node-3"
    ):
        raise RemoteBuildContractError("remote build requires the exact three-node inventory")
    return nodes


def select_builder(inventory: object) -> object:
    """Return the sole approved builder, rejecting ambiguity before any work."""
    builders = tuple(
        node
        for node in _nodes(inventory)
        if "builder" in tuple(getattr(node, "roles", ()))
    )
    if len(builders) != 1 or str(getattr(builders[0], "id", "")) != BUILDER_NODE_ID:
        raise RemoteBuildContractError(
            "remote build requires node-3 as the exactly one builder"
        )
    roles = set(getattr(builders[0], "roles", ()))
    required = {"app", "ingress", "file-arbiter"}
    if not required.issubset(roles):
        raise RemoteBuildContractError(
            "remote builder must retain app, ingress, and file-arbiter roles"
        )
    if roles.intersection({"file-data-primary", "file-data-standby"}):
        raise RemoteBuildContractError(
            "remote builder must not be a Gluster data-brick node"
        )
    return builders[0]


def _validate_provenance(provenance: Mapping[str, object]) -> tuple[str, str]:
    release_id = provenance.get("releaseId")
    source_sha256 = provenance.get("sourceStateSha256")
    if not isinstance(release_id, str) or not RELEASE_RE.fullmatch(release_id):
        raise RemoteBuildContractError("remote build release ID is invalid")
    if not isinstance(source_sha256, str) or not SHA256_RE.fullmatch(source_sha256):
        raise RemoteBuildContractError("remote build source state digest is invalid")
    return release_id, source_sha256


def create_remote_build_plan(
    inventory: object,
    provenance: Mapping[str, object],
) -> RemoteBuildPlan:
    """Validate immutable inputs and return the remote-only artifact contract."""
    builder = select_builder(inventory)
    release_id, source_sha256 = _validate_provenance(provenance)
    workspace = PurePosixPath("/var/lib/massar/builds") / release_id
    if any(part in {"", ".", ".."} for part in workspace.parts):
        raise RemoteBuildContractError("remote builder workspace is unsafe")
    return RemoteBuildPlan(
        release_id=release_id,
        source_state_sha256=source_sha256,
        builder_node_id=str(getattr(builder, "id")),
        workspace=workspace,
    )
