#!/usr/bin/env python3
"""Seal one byte-identical running Legacy release on all three nodes."""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import hashlib
import json
import sys
import tempfile
import uuid
from pathlib import Path

from clusterctl import Inventory, Node, load_inventory
from collect_current_release_manifest import (
    ensure_output_target, publish_without_overwrite, stage_file,
)
from release_contract import load_release_manifest
from ssh_transport import SshTarget, StrictSshTransport

HELPER = "/usr/local/sbin/massar-seal-legacy-release"


class SealError(RuntimeError):
    pass


def target(inventory: Inventory, node: Node) -> SshTarget:
    return SshTarget(node.id, node.public_address, str(inventory.cluster["ssh_user"]))


def envelope(node_id: str, stdout: str, statuses: set[str]) -> dict[str, object]:
    lines = [line for line in stdout.splitlines() if line.strip()]
    if len(lines) != 1:
        raise SealError(f"{node_id} returned an invalid seal envelope")
    value = json.loads(lines[0])
    if (
        not isinstance(value, dict)
        or value.get("schemaVersion") != 1
        or value.get("status") not in statuses
        or value.get("nodeId") != node_id
    ):
        raise SealError(f"{node_id} seal envelope identity is invalid")
    return value


def sealed_manifest(
    release_id: str,
    images: dict[str, str],
    tree_sha256: str,
    sealed_at: str,
) -> bytes:
    manifest = {
        "schemaVersion": 1,
        "releaseId": release_id,
        "createdAt": sealed_at,
        "platform": "linux/amd64",
        "images": images,
        "status": "success",
        "nodeCount": 3,
        "digestParity": True,
        "distribution": {
            node_id: {"status": "verified", "releaseFilesSha256": tree_sha256}
            for node_id in ("node-1", "node-2", "node-3")
        },
        "sealedLegacyProvenance": {
            "schemaVersion": 2,
            "type": "sealed-legacy-bootstrap",
            "sealedAt": sealed_at,
            "runtimeBundleSha256": tree_sha256,
            "runtimeBundleDigestAlgorithm": "massar-runtime-bundle-sha256-v1",
            "sourceReleaseLabel": release_id,
        },
    }
    return (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode()


def compensate(inventory, transport, operation_id: str) -> list[str]:
    failures: list[str] = []
    for node in reversed(inventory.nodes):
        try:
            completed = transport.run(
                target(inventory, node),
                ("/usr/bin/sudo", HELPER, "remove", node.id, operation_id),
                timeout_seconds=30,
            )
            envelope(node.id, completed.stdout, {"removed", "not-created"})
        except Exception as exc:
            failures.append(f"{node.id}: {exc}")
    return failures


def seal(inventory, transport, evidence_output: Path) -> dict[str, object]:
    destination = ensure_output_target(evidence_output, "Legacy seal evidence")
    inspected: list[dict[str, object]] = []
    for node in inventory.nodes:
        completed = transport.run(
            target(inventory, node),
            ("/usr/bin/sudo", HELPER, "inspect", node.id),
            timeout_seconds=120,
        )
        inspected.append(envelope(node.id, completed.stdout, {"ready"}))
    first = inspected[0]
    if any(
        candidate.get("releaseId") != first.get("releaseId")
        or candidate.get("images") != first.get("images")
        or candidate.get("treeSha256") != first.get("treeSha256")
        for candidate in inspected[1:]
    ):
        raise SealError("release label, images, or deterministic tree differ across nodes")
    sealed_at = dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")
    manifest = sealed_manifest(
        str(first["releaseId"]),
        dict(first["images"]),
        str(first["treeSha256"]),
        sealed_at,
    )
    manifest_sha = hashlib.sha256(manifest).hexdigest()
    with tempfile.NamedTemporaryFile(
        prefix="massar-sealed-", suffix=".json", delete=False
    ) as stream:
        stream.write(manifest)
        manifest_path = Path(stream.name)
    try:
        validated = load_release_manifest(manifest_path, str(first["releaseId"]))
        if validated.sha256 != manifest_sha:
            raise SealError("local sealed manifest validation differs")
    finally:
        manifest_path.unlink(missing_ok=True)
    operation_id = uuid.uuid4().hex
    encoded = base64.b64encode(manifest).decode()
    applied: dict[str, dict[str, object]] = {}
    try:
        for node in inventory.nodes:
            completed = transport.run(
                target(inventory, node),
                (
                    "/usr/bin/sudo", HELPER, "apply", node.id, operation_id,
                    str(first["treeSha256"]), encoded,
                ),
                timeout_seconds=120,
            )
            applied[node.id] = envelope(node.id, completed.stdout, {"sealed"})
        for node in inventory.nodes:
            completed = transport.run(
                target(inventory, node),
                ("/usr/bin/sudo", HELPER, "verify", node.id, operation_id),
                timeout_seconds=30,
            )
            envelope(node.id, completed.stdout, {"verified"})
        result = {
            "schemaVersion": 1, "status": "success",
            "clusterId": "massar-production", "operationId": operation_id,
            "sealedAt": sealed_at, "releaseId": first["releaseId"],
            "treeSha256": first["treeSha256"], "manifestSha256": manifest_sha,
            "images": first["images"], "nodes": applied,
        }
        temporary = stage_file(
            destination,
            (json.dumps(result, indent=2, sort_keys=True) + "\n").encode(),
        )
        try:
            publish_without_overwrite(temporary, destination)
        finally:
            temporary.unlink(missing_ok=True)
        return result
    except Exception as exc:
        failures = compensate(inventory, transport, operation_id)
        if failures:
            raise SealError(f"seal failed ({exc}); cleanup failed: {'; '.join(failures)}") from exc
        raise


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--evidence-output", required=True, type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    args = parser.parse_args(argv)
    try:
        inventory = load_inventory(args.inventory)
        destination = ensure_output_target(
            args.evidence_output, "Legacy seal evidence",
            create_parent=not args.dry_run,
        )
        if args.dry_run:
            print(json.dumps({"status": "dry-run", "nodes": [node.id for node in inventory.nodes], "sshAttempted": False}))
            return 0
        result = seal(
            inventory, StrictSshTransport(args.known_hosts, args.identity), destination
        )
        print(json.dumps({"status": "success", "releaseId": result["releaseId"], "evidenceOutput": str(destination)}))
        return 0
    except (SealError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Legacy release seal blocked: {exc}", file=sys.stderr)
        return 8


if __name__ == "__main__":
    raise SystemExit(main())
