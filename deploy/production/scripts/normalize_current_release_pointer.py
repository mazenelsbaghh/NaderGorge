#!/usr/bin/env python3
"""Normalize an absent current-release pointer on all three proven nodes."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import stat
import sys
import uuid
from pathlib import Path
from typing import Protocol

from clusterctl import Inventory, Node, load_inventory
from collect_current_release_manifest import (
    ensure_output_target,
    publish_without_overwrite,
    stage_file,
)
from release_contract import ReleaseContractError, ReleaseManifest, load_release_manifest
from ssh_transport import SshTarget, StrictSshTransport


ROOT_HELPER = "/usr/local/sbin/massar-normalize-current-release"
MAXIMUM_EVIDENCE_BYTES = 1024 * 1024
NODE_IDS = ("node-1", "node-2", "node-3")


class NormalizationError(RuntimeError):
    pass


class Transport(Protocol):
    def run(
        self,
        target: SshTarget,
        remote_argv: tuple[str, ...],
        *,
        timeout_seconds: int = 60,
        check: bool = True,
    ): ...


def utc_now(now: dt.datetime | None = None) -> str:
    value = now or dt.datetime.now(dt.timezone.utc)
    return value.astimezone(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def parse_timestamp(raw: object) -> dt.datetime:
    if not isinstance(raw, str) or not raw.endswith("Z"):
        raise NormalizationError("collector capturedAt must be UTC")
    try:
        value = dt.datetime.fromisoformat(raw[:-1] + "+00:00")
    except ValueError as exc:
        raise NormalizationError("collector capturedAt is invalid") from exc
    return value.astimezone(dt.timezone.utc)


def read_evidence(path: Path) -> dict[str, object]:
    expanded = path.expanduser()
    info = os.lstat(expanded)
    if (
        stat.S_ISLNK(info.st_mode)
        or not stat.S_ISREG(info.st_mode)
        or info.st_size <= 0
        or info.st_size > MAXIMUM_EVIDENCE_BYTES
    ):
        raise NormalizationError("collector evidence must be a bounded regular file")
    descriptor = os.open(expanded, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    try:
        payload = os.read(descriptor, MAXIMUM_EVIDENCE_BYTES + 1)
    finally:
        os.close(descriptor)
    if not payload or len(payload) > MAXIMUM_EVIDENCE_BYTES:
        raise NormalizationError("collector evidence changed or exceeded its bound")
    try:
        value = json.loads(payload.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise NormalizationError("collector evidence JSON is invalid") from exc
    if not isinstance(value, dict):
        raise NormalizationError("collector evidence must be an object")
    return value


def validate_inputs(
    manifest_path: Path,
    collector_evidence_path: Path,
    *,
    now: dt.datetime | None = None,
) -> tuple[ReleaseManifest, dict[str, object]]:
    evidence = read_evidence(collector_evidence_path)
    manifest = load_release_manifest(
        manifest_path,
        str(evidence.get("releaseId", "")),
    )
    required = {
        "schemaVersion", "status", "clusterId", "capturedAt", "releaseId",
        "manifestSha256", "images", "nodeCount", "byteParity", "nodes",
    }
    if (
        set(evidence) != required
        or evidence.get("schemaVersion") != 1
        or evidence.get("status") != "success"
        or evidence.get("clusterId") != "massar-production"
        or evidence.get("releaseId") != manifest.release_id
        or evidence.get("manifestSha256") != manifest.sha256
        or evidence.get("images") != manifest.images
        or evidence.get("nodeCount") != 3
        or evidence.get("byteParity") is not True
    ):
        raise NormalizationError("collector evidence does not bind the exact manifest")
    captured = parse_timestamp(evidence["capturedAt"])
    current = (now or dt.datetime.now(dt.timezone.utc)).astimezone(dt.timezone.utc)
    age = current - captured
    if age < -dt.timedelta(minutes=2) or age > dt.timedelta(minutes=15):
        raise NormalizationError("collector evidence is not fresh")
    nodes = evidence.get("nodes")
    if not isinstance(nodes, dict) or set(nodes) != set(NODE_IDS):
        raise NormalizationError("collector evidence does not prove the exact three nodes")
    for node_id in NODE_IDS:
        node = nodes[node_id]
        expected_fields = {
            "releaseRoot", "manifestPath", "manifestSha256", "resolutionMode",
            "nodeLabel", "actualImages", "releaseFilesSha256",
            "releaseFilesDigestVerified",
        }
        if (
            not isinstance(node, dict)
            or set(node) != expected_fields
            or node.get("releaseRoot")
            != f"/opt/massar/releases/{manifest.release_id}"
            or node.get("manifestPath")
            != f"/opt/massar/releases/{manifest.release_id}/manifest.json"
            or node.get("manifestSha256") != manifest.sha256
            or node.get("resolutionMode") != "docker-label-fallback"
            or node.get("nodeLabel") != node_id
            or node.get("actualImages") != manifest.images
            or node.get("releaseFilesDigestVerified") is not True
            or node.get("releaseFilesSha256") != manifest.release_files_sha256
        ):
            raise NormalizationError(
                f"collector evidence is invalid for {node_id}"
            )
    return manifest, evidence


def target(inventory: Inventory, node: Node) -> SshTarget:
    return SshTarget(
        node.id,
        node.public_address,
        str(inventory.cluster["ssh_user"]),
    )


def parse_remote(node_id: str, stdout: str, expected_status: set[str]) -> dict[str, object]:
    lines = [line for line in stdout.splitlines() if line.strip()]
    if len(lines) != 1:
        raise NormalizationError(f"{node_id} returned an invalid helper envelope")
    try:
        value = json.loads(lines[0])
    except json.JSONDecodeError as exc:
        raise NormalizationError(f"{node_id} returned invalid helper JSON") from exc
    if (
        not isinstance(value, dict)
        or value.get("schemaVersion") != 1
        or value.get("status") not in expected_status
    ):
        raise NormalizationError(f"{node_id} helper status is invalid")
    return value


def helper_command(
    action: str,
    release_id: str,
    manifest_sha256: str,
    operation_id: str | None = None,
) -> tuple[str, ...]:
    command = (
        "/usr/bin/sudo",
        ROOT_HELPER,
        action,
        release_id,
        manifest_sha256,
    )
    return (*command, operation_id) if operation_id is not None else command


def rollback_operation(
    *,
    inventory: Inventory,
    transport: Transport,
    operation_id: str,
    manifest: ReleaseManifest,
) -> list[str]:
    errors: list[str] = []
    for node in reversed(inventory.nodes):
        try:
            completed = transport.run(
                target(inventory, node),
                helper_command(
                    "remove",
                    manifest.release_id,
                    manifest.sha256,
                    operation_id,
                ),
                timeout_seconds=30,
            )
            parse_remote(node.id, completed.stdout, {"removed", "not-created"})
        except Exception as exc:
            errors.append(f"{node.id}: {exc}")
    return errors


def normalize(
    *,
    inventory: Inventory,
    transport: Transport,
    manifest: ReleaseManifest,
    collector_evidence: dict[str, object],
    evidence_output: Path,
    now: dt.datetime | None = None,
) -> dict[str, object]:
    destination = ensure_output_target(evidence_output, "normalization evidence")
    operation_id = uuid.uuid4().hex
    for node in inventory.nodes:
        completed = transport.run(
            target(inventory, node),
            helper_command("preflight", manifest.release_id, manifest.sha256),
            timeout_seconds=30,
        )
        value = parse_remote(node.id, completed.stdout, {"ready"})
        if (
            value.get("releaseId") != manifest.release_id
            or value.get("manifestSha256") != manifest.sha256
            or value.get("currentAbsent") is not True
        ):
            raise NormalizationError(f"{node.id} preflight identity is invalid")

    applied: dict[str, dict[str, object]] = {}
    try:
        for node in inventory.nodes:
            completed = transport.run(
                target(inventory, node),
                helper_command(
                    "apply",
                    manifest.release_id,
                    manifest.sha256,
                    operation_id,
                ),
                timeout_seconds=30,
            )
            value = parse_remote(node.id, completed.stdout, {"created"})
            if (
                value.get("releaseId") != manifest.release_id
                or value.get("target")
                != f"/opt/massar/releases/{manifest.release_id}"
                or not isinstance(value.get("device"), int)
                or isinstance(value.get("device"), bool)
                or not isinstance(value.get("inode"), int)
                or isinstance(value.get("inode"), bool)
            ):
                raise NormalizationError(f"{node.id} apply identity is invalid")
            applied[node.id] = value
        verified: dict[str, dict[str, object]] = {}
        for node in inventory.nodes:
            completed = transport.run(
                target(inventory, node),
                helper_command(
                    "verify",
                    manifest.release_id,
                    manifest.sha256,
                    operation_id,
                ),
                timeout_seconds=30,
            )
            value = parse_remote(node.id, completed.stdout, {"verified"})
            if (
                value.get("releaseId") != manifest.release_id
                or value.get("target")
                != f"/opt/massar/releases/{manifest.release_id}"
                or value.get("device") != applied[node.id].get("device")
                or value.get("inode") != applied[node.id].get("inode")
            ):
                raise NormalizationError(f"{node.id} post-verification identity differs")
            verified[node.id] = value
        result = {
            "schemaVersion": 1,
            "status": "success",
            "clusterId": "massar-production",
            "operationId": operation_id,
            "completedAt": utc_now(now),
            "releaseId": manifest.release_id,
            "manifestSha256": manifest.sha256,
            "collectorCapturedAt": collector_evidence["capturedAt"],
            "nodeCount": 3,
            "nodes": verified,
        }
        temporary = stage_file(
            destination,
            (json.dumps(result, indent=2, sort_keys=True) + "\n").encode("utf-8"),
        )
        try:
            publish_without_overwrite(temporary, destination)
        finally:
            temporary.unlink(missing_ok=True)
        return result
    except Exception as exc:
        rollback_errors = rollback_operation(
            inventory=inventory,
            transport=transport,
            operation_id=operation_id,
            manifest=manifest,
        )
        if rollback_errors:
            raise NormalizationError(
                f"normalization failed ({exc}); compensating rollback also failed: "
                + "; ".join(rollback_errors)
            ) from exc
        raise


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--collector-evidence", required=True, type=Path)
    parser.add_argument("--evidence-output", required=True, type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    args = parser.parse_args(argv)
    try:
        inventory = load_inventory(args.inventory)
        manifest, collector_evidence = validate_inputs(
            args.manifest,
            args.collector_evidence,
        )
        destination = ensure_output_target(
            args.evidence_output,
            "normalization evidence",
            create_parent=not args.dry_run,
        )
        if args.dry_run:
            print(json.dumps({
                "status": "dry-run",
                "releaseId": manifest.release_id,
                "manifestSha256": manifest.sha256,
                "nodes": list(NODE_IDS),
                "evidenceOutput": str(destination),
                "sshAttempted": False,
            }))
            return 0
        transport = StrictSshTransport(args.known_hosts, args.identity)
        result = normalize(
            inventory=inventory,
            transport=transport,
            manifest=manifest,
            collector_evidence=collector_evidence,
            evidence_output=destination,
        )
        print(json.dumps({
            "status": "success",
            "releaseId": result["releaseId"],
            "operationId": result["operationId"],
            "evidenceOutput": str(destination),
        }))
        return 0
    except (
        NormalizationError,
        ReleaseContractError,
        OSError,
        ValueError,
        json.JSONDecodeError,
    ) as exc:
        print(f"current release normalization blocked: {exc}", file=sys.stderr)
        return 7


if __name__ == "__main__":
    raise SystemExit(main())
