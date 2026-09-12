#!/usr/bin/env python3
"""Measure direct image transfer using a verified release; never load or deploy it."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from pathlib import Path

from clusterctl import load_inventory, redact, utc_now
from release_contract import load_release_manifest, write_json_atomic
from ssh_transport import SshTarget, StrictSshTransport


def create_benchmark_stage(transport, receiver: SshTarget, stage: str) -> tuple[int, int]:
    script = f"import os,json; os.mkdir({stage!r},0o700); s=os.stat({stage!r}); print(json.dumps([s.st_dev,s.st_ino]))"
    completed = transport.run(receiver, ("python3", "-I", "-c", script))
    return tuple(json.loads(completed.stdout))


def remove_benchmark_stage(transport, receiver: SshTarget, stage: str, identity: tuple[int, int]) -> None:
    script = f"""
import os,shutil,stat
path={stage!r}
observed=os.lstat(path)
if not stat.S_ISDIR(observed.st_mode) or (observed.st_dev,observed.st_ino)!={identity!r}:
    raise SystemExit('benchmark staging ownership changed; refusing cleanup')
shutil.rmtree(path)
"""
    transport.run(receiver, ("python3", "-I", "-c", script))


def benchmark_receiver(transport, builder: SshTarget, node, manifest) -> list[dict]:
    receiver = SshTarget(node.id, node.public_address, builder.user)
    internal_receiver = SshTarget(node.id, node.overlay_address, builder.user)
    stage = f"/tmp/massar-{manifest.release_id}"
    # An existing release staging directory may belong to an active build.
    # mkdir must succeed before this operation acquires cleanup ownership.
    identity = create_benchmark_stage(transport, receiver, stage)
    try:
        transfers = []
        for image in manifest.images:
            artifact = manifest.artifacts[image]
            source = f"/var/lib/massar/builds/{manifest.release_id}/artifacts/{image}.tar"
            receipt = transport.stream_remote_file(builder, source, internal_receiver, f"{stage}/{image}.tar")
            if receipt["sha256"] != artifact["archiveSha256"]:
                raise RuntimeError("transferred archive differs from the verified release manifest")
            transfers.append(receipt)
            print(json.dumps(receipt), flush=True)
        return transfers
    finally:
        remove_benchmark_stage(transport, receiver, stage, identity)


def benchmark(inventory, transport, manifest) -> list[dict]:
    builder_node = inventory.nodes[2]
    builder = SshTarget(builder_node.id, builder_node.public_address, inventory.cluster["ssh_user"])
    transfers = []
    for node in inventory.nodes[:2]:
        transfers.extend(benchmark_receiver(transport, builder, node, manifest))
    return transfers


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--known-hosts", type=Path, required=True)
    parser.add_argument("--identity", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--release", required=True)
    parser.add_argument("--output", type=Path, required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    inventory = load_inventory(args.inventory)
    manifest = load_release_manifest(args.manifest, args.release)
    if not manifest.artifacts:
        raise ValueError("benchmark requires a verified release with archive checksums")
    if args.dry_run:
        print(json.dumps({"status": "dry-run", "source": "node-3", "targets": ["node-1", "node-2"],
                          "release": manifest.release_id, "archivesPerTarget": list(manifest.images),
                          "loadsImages": False, "deploysApplication": False, "cleansOwnStaging": True}))
        return
    args.output.parent.mkdir(parents=True, exist_ok=True)
    evidence = {"status": "running", "startedAt": utc_now(), "release": manifest.release_id}
    started = time.monotonic()
    try:
        transport = StrictSshTransport(args.known_hosts, args.identity)
        evidence["transfers"] = benchmark(inventory, transport, manifest)
        evidence["status"] = "success"
    except (RuntimeError, OSError, ValueError, subprocess.SubprocessError) as exc:
        evidence.update(status="failed", reason=redact(str(exc)))
        raise
    finally:
        evidence.update(completedAt=utc_now(), elapsedSeconds=round(time.monotonic() - started, 3))
        write_json_atomic(args.output, evidence)
    print(json.dumps({"status": "success", "elapsedSeconds": evidence["elapsedSeconds"], "evidence": str(args.output)}))


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, ValueError, subprocess.SubprocessError) as exc:
        print(redact(f"image transfer benchmark failed: {exc}"), file=sys.stderr)
        raise SystemExit(6)
