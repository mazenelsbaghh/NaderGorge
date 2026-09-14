#!/usr/bin/env python3
"""Measure reuse of the four exact images without application restarts."""
import argparse
import json
from pathlib import Path

from clusterctl import load_inventory, operator_transport, target
from release_contract import load_release_manifest
from remote_build_release import select_builder


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--dry-run", action="store_true")
    action.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    raw = json.loads(args.manifest.read_text())
    manifest = load_release_manifest(args.manifest, raw["releaseId"])
    if args.dry_run:
        print(json.dumps({"status": "dry-run", "releaseId": manifest.release_id, "steps": ["verify-immutable-source", "refresh-base-identities", "require-existing-input-cache", "verify-registry-and-image-digests"], "applicationRestarts": False}))
        return
    inventory = load_inventory(Path("deploy/production/inventory/production.yml"), require_operator_files=True)
    transport = operator_transport(inventory)
    host = target(inventory, select_builder(inventory))
    workspace = f"/var/lib/massar/builds/{manifest.release_id}"
    transport.run(host, ("sudo", "/usr/local/sbin/massar-remote-builder", "--workspace", workspace,
                        "--release", manifest.release_id, "--source-sha256", manifest.source_state_sha256,
                        "--benchmark-reuse", "--yes"), timeout_seconds=600)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    transport.fetch(host, f"{workspace}/reuse-benchmark.json", args.output, timeout_seconds=60, max_bytes=1024*1024)
    print(args.output.read_text())


if __name__ == "__main__":
    main()
