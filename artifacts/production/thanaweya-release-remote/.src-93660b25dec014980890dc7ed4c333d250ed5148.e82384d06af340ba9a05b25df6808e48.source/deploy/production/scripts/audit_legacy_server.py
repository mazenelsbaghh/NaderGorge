#!/usr/bin/env python3
"""Read-only, PII-safe inventory for the explicitly approved legacy test host."""

from __future__ import annotations

import argparse
import datetime as dt
import json
from pathlib import Path

from ssh_transport import SshTarget, StrictSshTransport


REMOTE_AUDIT = r"""
set -euo pipefail
python3 - <<'PY'
import hashlib, json, os, pathlib, subprocess

def command(argv, timeout=30):
    result = subprocess.run(
        argv, text=True, capture_output=True, timeout=timeout, check=False
    )
    return result.returncode, result.stdout.strip()

def docker_json(argv):
    code, output = command(["docker", *argv])
    if code != 0:
        return []
    rows = []
    for line in output.splitlines():
        try:
            rows.append(json.loads(line))
        except json.JSONDecodeError:
            pass
    return rows

containers = docker_json(["ps", "-a", "--format", "{{json .}}"])
container_inventory = []
postgres_candidates = []
for row in containers:
    container_id = row.get("ID", "")
    code, raw = command(["docker", "inspect", container_id])
    if code != 0:
        continue
    detail = json.loads(raw)[0]
    image = str(detail.get("Config", {}).get("Image", ""))
    name = str(detail.get("Name", "")).lstrip("/")
    environment_keys = sorted(
        value.split("=", 1)[0]
        for value in detail.get("Config", {}).get("Env", [])
        if "=" in value
    )
    mounts = [{
        "type": mount.get("Type"),
        "name": mount.get("Name"),
        "sourceKind": "docker-volume"
            if mount.get("Type") == "volume"
            else "host-bind",
        "destination": mount.get("Destination"),
        "readWrite": bool(mount.get("RW")),
    } for mount in detail.get("Mounts", [])]
    container_inventory.append({
        "name": name,
        "image": image,
        "state": detail.get("State", {}).get("Status"),
        "environmentKeys": environment_keys,
        "mounts": mounts,
    })
    if "postgres" in image.lower() or "db" in name.lower():
        postgres_candidates.append(name)

databases = []
for container in sorted(set(postgres_candidates)):
    discover = r'''
user="${POSTGRES_USER:-postgres}"
psql -XAt -v ON_ERROR_STOP=1 -U "$user" -d postgres \
  -c "select datname from pg_database where datallowconn and not datistemplate order by datname;"
'''
    code, names = command(["docker", "exec", container, "sh", "-ec", discover])
    if code != 0:
        databases.append({"container": container, "accessible": False})
        continue
    for database in names.splitlines():
        if not database or database in {"postgres"}:
            continue
        audit = r'''
user="${POSTGRES_USER:-postgres}"
database="$1"
psql -XAt -v ON_ERROR_STOP=1 -U "$user" -d "$database" <<'SQL'
select 'server_version|' || current_setting('server_version');
select 'migration_count|' || case when to_regclass('public."__EFMigrationsHistory"') is null then '0' else (select count(*)::text from "__EFMigrationsHistory") end;
select 'latest_migration|' || case when to_regclass('public."__EFMigrationsHistory"') is null then '' else coalesce((select max("MigrationId") from "__EFMigrationsHistory"), '') end;
select 'migration|' || "MigrationId"
from "__EFMigrationsHistory" order by "MigrationId";
select 'table|' || quote_ident(c.relname) || '|' ||
  (xpath('/row/count/text()', query_to_xml(format('select count(*) as count from %I', c.relname), false, true, '')))[1]::text
from pg_class c
where c.relnamespace='public'::regnamespace and c.relkind in ('r','p')
order by c.relname;
select 'column|' || table_name || '|' || ordinal_position || '|' || column_name || '|' ||
       data_type || '|' || is_nullable || '|' || coalesce(column_default, '')
from information_schema.columns where table_schema='public'
order by table_name, ordinal_position;
select 'constraint|' || conrelid::regclass::text || '|' || conname || '|' || pg_get_constraintdef(oid)
from pg_constraint where connamespace='public'::regnamespace order by conrelid::regclass::text, conname;
select 'index|' || tablename || '|' || indexname || '|' || indexdef
from pg_indexes where schemaname='public' order by tablename,indexname;
SQL
'''
        code, output = command(
            ["docker", "exec", container, "sh", "-ec", audit, "sh", database],
            timeout=180,
        )
        if code != 0:
            databases.append({
                "container": container,
                "database": database,
                "accessible": False,
            })
            continue
        table_counts = {}
        migration_count = 0
        latest_migration = ""
        migration_ids = []
        schema_lines = []
        for line in output.splitlines():
            kind, _, rest = line.partition("|")
            if kind == "table":
                table, _, count = rest.partition("|")
                table_counts[table.strip('"')] = int(count or 0)
            elif kind == "migration_count":
                migration_count = int(rest or 0)
            elif kind == "latest_migration":
                latest_migration = rest
            elif kind == "migration":
                migration_ids.append(rest)
            elif kind in {"column", "constraint", "index"}:
                schema_lines.append(line)
        databases.append({
            "container": container,
            "database": database,
            "accessible": True,
            "migrationCount": migration_count,
            "latestMigration": latest_migration,
            "migrationIds": migration_ids,
            "tableCounts": table_counts,
            "schemaSha256": hashlib.sha256(
                "\n".join(schema_lines).encode()
            ).hexdigest(),
        })

volumes = []
for row in docker_json(["volume", "ls", "--format", "{{json .}}"]):
    name = row.get("Name")
    code, raw = command(["docker", "volume", "inspect", name])
    if code != 0:
        continue
    mountpoint = pathlib.Path(json.loads(raw)[0].get("Mountpoint", ""))
    file_count = 0
    total_bytes = 0
    symlink_count = 0
    manifest = hashlib.sha256()
    if mountpoint.is_dir():
        for path in sorted(mountpoint.rglob("*")):
            try:
                if path.is_symlink():
                    symlink_count += 1
                    continue
                if not path.is_file():
                    continue
                stat = path.stat()
                relative = str(path.relative_to(mountpoint))
                file_count += 1
                total_bytes += stat.st_size
                manifest.update(relative.encode(errors="surrogateescape"))
                manifest.update(b"\0")
                manifest.update(str(stat.st_size).encode())
                manifest.update(b"\0")
            except OSError:
                continue
    volumes.append({
        "name": name,
        "fileCount": file_count,
        "totalBytes": total_bytes,
        "symlinkCount": symlink_count,
        "pathSizeManifestSha256": manifest.hexdigest(),
    })

worktrees = []
for candidate in ("/var/www/nadergorge", "/var/www/nader-gorge", "/opt/massar/current"):
    root = pathlib.Path(candidate)
    if not root.is_dir():
        continue
    env_keys = []
    for env_file in (root / ".env", root / ".env.prod", root / ".env.production"):
        if not env_file.is_file():
            continue
        for line in env_file.read_text(errors="replace").splitlines():
            stripped = line.strip()
            if stripped and not stripped.startswith("#") and "=" in stripped:
                env_keys.append(stripped.split("=", 1)[0])
    code, head = command(["git", "-C", str(root), "rev-parse", "HEAD"])
    status_code, status = command([
        "git", "-C", str(root), "status", "--porcelain=v1", "--untracked-files=all",
    ])
    worktrees.append({
        "path": candidate,
        "gitCommit": head if code == 0 else None,
        "dirtyEntryCount": len(status.splitlines()) if status_code == 0 else None,
        "environmentKeys": sorted(set(env_keys)),
    })

print(json.dumps({
    "hostname": pathlib.Path("/etc/hostname").read_text().strip(),
    "containers": container_inventory,
    "databases": databases,
    "volumes": volumes,
    "worktrees": worktrees,
}, separators=(",", ":")))
PY
"""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", required=True)
    parser.add_argument("--user", default="root")
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    transport = StrictSshTransport(args.known_hosts, args.identity)
    result = transport.run(
        SshTarget("legacy-test", args.host, args.user),
        ("bash", "-lc", REMOTE_AUDIT),
        timeout_seconds=300,
    )
    payload = json.loads(result.stdout)
    payload.update({
        "schemaVersion": 1,
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "mode": "read-only",
        "status": "success",
    })
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temporary.chmod(0o640)
    temporary.replace(args.output)
    print(json.dumps({
        "status": "success",
        "databaseCount": len(payload["databases"]),
        "volumeCount": len(payload["volumes"]),
        "output": str(args.output),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
