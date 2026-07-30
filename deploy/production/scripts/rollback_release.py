#!/usr/bin/env python3
"""Application-only rollback wrapper; never performs a database down-migration."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import subprocess
import sys
from pathlib import Path

from release_contract import (
    ReleaseContractError,
    load_release_manifest,
    load_rollback_compatibility_gate,
    read_exact_json,
)


ROLLBACK_POLICY = {
    "applicationOnly": True,
    "databaseAction": "retain-compatible-schema",
    "priorApplicationSmokeRequired": True,
    "forwardFixEvidenceRequiredOnSchemaDefect": True,
}


def build_deploy_command(args: argparse.Namespace) -> list[str]:
    deploy = Path(__file__).with_name("deploy_release.py")
    command = [
        sys.executable, str(deploy),
        "--inventory", str(args.inventory),
        "--known-hosts", str(args.known_hosts),
        "--identity", str(args.identity),
        "--release", args.release,
        "--manifest", str(args.manifest),
        "--rollback-current-manifest", str(args.current_manifest),
        "--rollback-evidence", str(args.compatibility_evidence),
        "--dry-run" if args.dry_run else "--yes",
    ]
    forbidden_options = {
        "--database-restore",
        "--down-migration",
    }
    if forbidden_options.intersection(command):
        raise ReleaseContractError("application rollback attempted a database action")
    return command


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--release", required=True)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--current-manifest", required=True, type=Path)
    parser.add_argument("--compatibility-evidence", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true")
    args = parser.parse_args()

    if not (args.yes or args.dry_run):
        print("rollback blocked: --yes or --dry-run is required", file=sys.stderr)
        return 5
    target_manifest = load_release_manifest(args.manifest, args.release)
    _, current_value = read_exact_json(
        args.current_manifest,
        "rollback current release manifest",
    )
    current_release = current_value.get("releaseId")
    if not isinstance(current_release, str):
        raise ReleaseContractError("rollback current release identity is missing")
    current_manifest = load_release_manifest(
        args.current_manifest,
        current_release,
    )
    load_rollback_compatibility_gate(
        args.compatibility_evidence,
        current_manifest=current_manifest,
        target_manifest=target_manifest,
        now=dt.datetime.now(dt.timezone.utc),
    )

    command = build_deploy_command(args)
    completed = subprocess.run(command, check=False)
    if completed.returncode == 0:
        print(json.dumps({
            "release": args.release,
            "status": "dry-run" if args.dry_run else "success",
            **ROLLBACK_POLICY,
        }, sort_keys=True))
    return completed.returncode


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        ReleaseContractError, OSError, ValueError, json.JSONDecodeError,
    ) as exc:
        print(f"rollback blocked: {exc}", file=sys.stderr)
        raise SystemExit(6)
