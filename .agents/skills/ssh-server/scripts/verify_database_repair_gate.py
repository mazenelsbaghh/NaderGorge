#!/usr/bin/env python3
"""Require a migration gate to target the exact current immutable release."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path


class RepairGateError(RuntimeError):
    pass


def verify(gate_path: Path, manifest_path: Path, release: str) -> None:
    if any(path.is_symlink() or not path.is_file() for path in (gate_path, manifest_path)):
        raise RepairGateError("gate and manifest must be regular files")
    gate = json.loads(gate_path.read_text(encoding="utf-8"))
    manifest_bytes = manifest_path.read_bytes()
    manifest = json.loads(manifest_bytes)
    manifest_sha = hashlib.sha256(manifest_bytes).hexdigest()
    if manifest.get("releaseId") != release:
        raise RepairGateError("repair manifest release changed")
    if (
        gate.get("status") != "success"
        or gate.get("releaseId") != release
        or gate.get("currentReleaseId") != release
        or gate.get("manifestSha256") != manifest_sha
        or gate.get("currentManifestSha256") != manifest_sha
    ):
        raise RepairGateError(
            "DB-only gate is not bound to the exact current release; "
            "a concurrent rollout may have occurred"
        )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--gate", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--release", required=True)
    args = parser.parse_args()
    verify(args.gate, args.manifest, args.release)
    print(json.dumps({"status": "success", "releaseId": args.release}))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RepairGateError, json.JSONDecodeError) as exc:
        print(f"DB-only gate blocked: {exc}", file=sys.stderr)
        raise SystemExit(2)
