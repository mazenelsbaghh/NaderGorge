#!/usr/bin/env python3
"""Generate migration/schema acceptance evidence from an allowlisted local clone."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

import audit_database


CONTAINER_NAME = re.compile(r"^massar-legacy-stage-166(?:-[a-z0-9-]+)?$")
MIGRATION_ATTRIBUTE = re.compile(r'\[Migration\("([^"]+)"\)\]')


def container_psql(container: str, database: str, query: str) -> str:
    completed = subprocess.run(
        [
            "docker", "exec", container,
            "psql", "-XAt", "-v", "ON_ERROR_STOP=1",
            "-U", "postgres", "-d", database, "-c", query,
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    if completed.returncode:
        raise RuntimeError(completed.stderr.strip() or "local clone audit failed")
    return completed.stdout.strip()


def repository_migrations(repository: Path) -> list[str]:
    root = repository.resolve() / "backend/src/NaderGorge.Infrastructure/Migrations"
    migrations = sorted({
        match.group(1)
        for path in root.glob("*.cs")
        for match in [MIGRATION_ATTRIBUTE.search(path.read_text(encoding="utf-8"))]
        if match
    })
    if not migrations:
        raise RuntimeError("repository migration set is empty")
    return migrations


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--container", default="massar-legacy-stage-166")
    parser.add_argument(
        "--database",
        choices=("massar_platform", "postgres"),
        default="massar_platform",
    )
    parser.add_argument("--repository", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if not CONTAINER_NAME.fullmatch(args.container):
        print("local migration evidence blocked: container is not allowlisted", file=sys.stderr)
        return 6
    try:
        container_id = subprocess.run(
            ["docker", "inspect", "--format", "{{.Id}}", args.container],
            text=True,
            capture_output=True,
            check=False,
        )
        if container_id.returncode or not re.fullmatch(
            r"[0-9a-f]{64}", container_id.stdout.strip()
        ):
            raise RuntimeError("allowlisted local clone is not available")
        audit_database.psql = lambda query: container_psql(
            args.container,
            args.database,
            query,
        )
        audit = audit_database.collect()
        expected = repository_migrations(args.repository)
        exact_match = audit["migrationIds"] == expected
        audit["migrationModelMatch"] = exact_match
        audit["criticalFindings"] = int(audit["criticalFindings"]) + int(not exact_match)
        payload = {
            "schemaVersion": 1,
            "evidenceScope": "local-disposable-staging-clone",
            "generatedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace(
                "+00:00", "Z"
            ),
            "containerIdentitySha256": hashlib.sha256(
                container_id.stdout.strip().encode()
            ).hexdigest(),
            "database": args.database,
            "repositoryMigrationCount": len(expected),
            **audit,
        }
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        args.output.chmod(0o640)
        print(json.dumps({
            "status": "success" if payload["criticalFindings"] == 0 else "failed",
            "migrationCount": len(payload["migrationIds"]),
            "output": str(args.output),
        }))
        return 0 if payload["criticalFindings"] == 0 else 6
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"local migration evidence blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
