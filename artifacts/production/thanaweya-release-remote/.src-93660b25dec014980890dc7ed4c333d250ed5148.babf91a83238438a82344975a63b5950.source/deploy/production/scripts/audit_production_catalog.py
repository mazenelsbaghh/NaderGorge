#!/usr/bin/env python3
"""Read-only Production catalog/count audit for legacy reconciliation."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path

from clusterctl import load_inventory
from ssh_transport import SshTarget, StrictSshTransport


REMOTE_AUDIT = r"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
sudo docker run --rm -i --network host \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgapp)"; exec psql -h 127.0.0.1 -p 6432 -U massar_app -d massar_platform -XAt -v ON_ERROR_STOP=1' <<'SQL'
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
from pg_constraint where connamespace='public'::regnamespace
order by conrelid::regclass::text, conname;
select 'index|' || tablename || '|' || indexname || '|' || indexdef
from pg_indexes where schemaname='public' order by tablename,indexname;
SQL
"""


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()

    inventory = load_inventory(args.inventory)
    node = inventory.nodes[0]
    transport = StrictSshTransport(args.known_hosts, args.identity)
    result = transport.run(
        SshTarget(node.id, node.public_address, str(inventory.cluster["ssh_user"])),
        ("bash", "-lc", REMOTE_AUDIT),
        timeout_seconds=300,
    )
    migrations: list[str] = []
    table_counts: dict[str, int] = {}
    schema_lines: list[str] = []
    for line in result.stdout.splitlines():
        kind, _, rest = line.partition("|")
        if kind == "migration":
            migrations.append(rest)
        elif kind == "table":
            table, _, count = rest.partition("|")
            table_counts[table.strip('"')] = int(count or 0)
        elif kind in {"column", "constraint", "index"}:
            schema_lines.append(line)
    payload = {
        "schemaVersion": 1,
        "capturedAt": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "mode": "read-only",
        "status": "success",
        "migrationCount": len(migrations),
        "latestMigration": migrations[-1] if migrations else None,
        "migrationIds": migrations,
        "tableCounts": table_counts,
        "schemaSha256": hashlib.sha256("\n".join(schema_lines).encode()).hexdigest(),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.output.with_suffix(".tmp")
    temporary.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    temporary.chmod(0o640)
    temporary.replace(args.output)
    print(json.dumps({
        "status": "success",
        "migrationCount": len(migrations),
        "tableCount": len(table_counts),
        "output": str(args.output),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
