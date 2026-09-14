#!/usr/bin/env python3
"""Compare the reviewed EF schema contract with a read-only server catalog."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
MIGRATIONS = ROOT / "backend/src/NaderGorge.Infrastructure/Migrations"
SNAPSHOT = MIGRATIONS / "AppDbContextModelSnapshot.cs"
MIGRATION_FILE = re.compile(r"^(\d{14}_[A-Za-z0-9_]+)\.cs$")
TO_TABLE = re.compile(r'\.ToTable\("([^"]+)"')


class InventoryError(RuntimeError):
    pass


def expected_contract(snapshot: Path = SNAPSHOT) -> tuple[list[str], list[str]]:
    if not snapshot.is_file() or snapshot.is_symlink():
        raise InventoryError(f"EF snapshot is missing or unsafe: {snapshot}")
    tables = sorted(set(TO_TABLE.findall(snapshot.read_text(encoding="utf-8"))))
    if not tables:
        raise InventoryError("EF snapshot did not contain any public tables")
    migrations = sorted(
        match.group(1)
        for path in MIGRATIONS.iterdir()
        if path.is_file()
        and not path.is_symlink()
        and (match := MIGRATION_FILE.fullmatch(path.name))
    )
    if not migrations:
        raise InventoryError("repository does not contain numbered EF migrations")
    return tables, migrations


def compare(actual_path: Path, snapshot: Path = SNAPSHOT) -> dict[str, object]:
    if not actual_path.is_file() or actual_path.is_symlink():
        raise InventoryError(f"actual catalog is missing or unsafe: {actual_path}")
    actual = json.loads(actual_path.read_text(encoding="utf-8"))
    if actual.get("status") != "success" or not isinstance(actual.get("tableCounts"), dict):
        raise InventoryError("actual catalog does not match the successful audit contract")
    expected_tables, expected_migrations = expected_contract(snapshot)
    actual_tables = sorted(str(value) for value in actual["tableCounts"])
    actual_migrations = actual.get("migrationIds")
    if not isinstance(actual_migrations, list) or not all(
        isinstance(value, str) for value in actual_migrations
    ):
        raise InventoryError("actual catalog migrationIds are invalid")
    missing_tables = sorted(set(expected_tables) - set(actual_tables))
    extra_tables = sorted(
        set(actual_tables) - set(expected_tables) - {"__EFMigrationsHistory"}
    )
    pending_migrations = [
        value for value in expected_migrations if value not in actual_migrations
    ]
    unexpected_migrations = [
        value for value in actual_migrations if value not in expected_migrations
    ]
    status = (
        "match"
        if not missing_tables
        and not extra_tables
        and not pending_migrations
        and not unexpected_migrations
        else "drift"
    )
    return {
        "schemaVersion": 1,
        "mode": "read-only-comparison",
        "status": status,
        "actualCatalog": str(actual_path),
        "expectedSnapshot": str(snapshot),
        "expectedLatestMigration": expected_migrations[-1],
        "actualLatestMigration": actual.get("latestMigration"),
        "expectedTables": expected_tables,
        "actualTables": actual_tables,
        "missingTables": missing_tables,
        "extraTables": extra_tables,
        "expectedMigrations": expected_migrations,
        "actualMigrations": actual_migrations,
        "pendingMigrations": pending_migrations,
        "unexpectedMigrations": unexpected_migrations,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--actual", required=True, type=Path)
    parser.add_argument("--snapshot", type=Path, default=SNAPSHOT)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--require-match", action="store_true")
    args = parser.parse_args()
    payload = compare(args.actual, args.snapshot)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(args.output.suffix + ".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temporary.chmod(0o640)
    temporary.replace(args.output)
    print(
        json.dumps(
            {
                "status": payload["status"],
                "missingTables": payload["missingTables"],
                "pendingMigrations": payload["pendingMigrations"],
                "unexpectedMigrations": payload["unexpectedMigrations"],
                "output": str(args.output),
            }
        )
    )
    return 4 if args.require_match and payload["status"] != "match" else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (InventoryError, json.JSONDecodeError) as exc:
        print(f"schema inventory failed: {exc}", file=sys.stderr)
        raise SystemExit(2)
