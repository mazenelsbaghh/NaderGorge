#!/usr/bin/env python3
"""Bind live schema drift to the migrator already shipped in the current release."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
MIGRATIONS = ROOT / "backend/src/NaderGorge.Infrastructure/Migrations"
RELEASE = re.compile(
    r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$"
)


class RepairPlanError(RuntimeError):
    pass


def plan(
    comparison_path: Path,
    manifest_path: Path,
    reason: str = "reviewed database repair",
) -> dict[str, object]:
    if len(reason.strip()) < 12:
        raise RepairPlanError("repair reason must contain at least 12 characters")
    if any(path.is_symlink() or not path.is_file() for path in (comparison_path, manifest_path)):
        raise RepairPlanError("comparison and current manifest must be regular files")
    comparison = json.loads(comparison_path.read_text(encoding="utf-8"))
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    pending = comparison.get("pendingMigrations")
    expected = comparison.get("expectedMigrations")
    actual = comparison.get("actualMigrations")
    missing_tables = comparison.get("missingTables")
    extra_tables = comparison.get("extraTables")
    migration_set = manifest.get("migrationSet")
    release = manifest.get("releaseId")
    images = manifest.get("images")
    if not isinstance(release, str) or not RELEASE.fullmatch(release):
        raise RepairPlanError("current release identity is invalid")
    if not isinstance(images, dict) or "migrator" not in images:
        raise RepairPlanError("current release has no reviewed migrator image")
    if not all(
        isinstance(value, list)
        for value in (pending, expected, actual, missing_tables, extra_tables)
    ):
        raise RepairPlanError("schema comparison contract is invalid")
    if extra_tables:
        raise RepairPlanError("extra server tables require reviewed manual reconciliation")
    if not pending:
        raise RepairPlanError("database already matches the current repository migrations")
    if comparison.get("unexpectedMigrations"):
        raise RepairPlanError("server contains migrations unknown to this repository")
    if expected[: len(actual)] != actual or expected[len(actual) :] != pending:
        raise RepairPlanError("server migrations are not an exact repository prefix")
    if not isinstance(migration_set, list) or migration_set != expected:
        raise RepairPlanError(
            "pending migrations are not all present in the current Production "
            "migrator; use the immutable release path"
        )
    created_tables: set[str] = set()
    for migration in pending:
        path = MIGRATIONS / f"{migration}.cs"
        if path.is_symlink() or not path.is_file():
            raise RepairPlanError(f"pending migration source is missing: {migration}")
        created_tables.update(
            re.findall(
                r'\.CreateTable\(\s*name:\s*"([^"]+)"',
                path.read_text(encoding="utf-8"),
                flags=re.MULTILINE,
            )
        )
    uncovered = sorted(set(missing_tables) - created_tables)
    if uncovered:
        raise RepairPlanError(
            "missing tables are not created by the pending current-release "
            f"migrations: {', '.join(uncovered)}"
        )
    return {
        "schemaVersion": 1,
        "status": "eligible",
        "mode": "current-release-migrator-no-build",
        "releaseId": release,
        "reason": reason,
        "pendingMigrations": pending,
        "missingTables": missing_tables,
        "applicationImagesChanged": False,
        "databaseRollback": "prohibited-forward-fix-only",
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--comparison", required=True, type=Path)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--reason", required=True)
    args = parser.parse_args()
    payload = plan(args.comparison, args.manifest, args.reason)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(args.output.suffix + ".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temporary.chmod(0o640)
    temporary.replace(args.output)
    print(json.dumps({**payload, "output": str(args.output)}))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RepairPlanError, json.JSONDecodeError) as exc:
        print(f"DB-only repair blocked: {exc}", file=sys.stderr)
        raise SystemExit(2)
