#!/usr/bin/env python3
"""Generate a PII-safe URL/path inventory from the local legacy staging clone."""

from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path

import validate_legacy_staging as validation


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--backup-dir", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    backup = args.backup_dir.expanduser().resolve()
    roots = (
        backup / "staging-files-assets",
        backup / "staging-files-protected",
        backup / "staging-files-app-data",
    )
    if any(root.is_symlink() or not root.is_dir() for root in roots):
        raise RuntimeError("all three local staging file roots are required")
    inventory = validation.file_reference_audit(*roots)
    payload = {
        "schemaVersion": 1,
        "evidenceScope": "local-legacy-staging-clone",
        "generatedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace(
            "+00:00", "Z"
        ),
        **inventory,
        "status": (
            "success"
            if inventory["missingUnblockedReferences"] == 0
            else "failed"
        ),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    args.output.chmod(0o640)
    print(json.dumps({
        "status": payload["status"],
        "discoveredColumnCount": payload["discoveredColumnCount"],
        "missingUnblockedReferences": payload["missingUnblockedReferences"],
        "output": str(args.output),
    }))
    return 0 if payload["status"] == "success" else 6


if __name__ == "__main__":
    raise SystemExit(main())
