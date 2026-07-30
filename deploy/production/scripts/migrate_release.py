#!/usr/bin/env python3
"""Migrate an isolated empty database, then Production, under the migrator lock."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import re
import sys
from pathlib import Path

from clusterctl import load_inventory
from release_contract import (
    ReleaseContractError,
    load_migration_safety_gate,
    load_release_manifest,
)
from ssh_transport import SshTarget, StrictSshTransport


DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
RELEASE = re.compile(r"^(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$")


class MigrationError(RuntimeError):
    pass


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", required=True, type=Path)
    parser.add_argument("--identity", required=True, type=Path)
    parser.add_argument("--release", required=True)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--backup-evidence", required=True, type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    if not RELEASE.fullmatch(args.release):
        raise MigrationError("invalid immutable release ID")
    manifest = load_release_manifest(args.manifest, args.release)
    gate = load_migration_safety_gate(
        args.backup_evidence,
        manifest=manifest,
        now=dt.datetime.now(dt.timezone.utc),
    )
    digest = manifest.images["migrator"]
    if args.dry_run:
        print(json.dumps({
            "release": args.release,
            "cleanAuditDatabase": True,
            "productionMigration": True,
            "rollbackDatabaseAction": "prohibited",
            "retainedSchemaOnApplicationRollback": True,
            "schemaFailureDisposition": "reviewed-forward-fix-only",
            "status": "dry-run",
        }))
        return 0
    if not args.yes:
        raise MigrationError("migration requires --yes or --dry-run")
    inventory = load_inventory(args.inventory)
    node = inventory.nodes[0]
    transport = StrictSshTransport(args.known_hosts, args.identity)
    target = SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"])
    image = f"massar/migrator:{args.release}"
    manifest_sha256 = manifest.sha256
    script = f"""
set -Eeuo pipefail
stage="bootstrap"
report_failure() {{
  status=$?
  printf 'MASSAR_MIGRATION_FAILURE stage=%s line=%s status=%s\n' \
    "$stage" "$1" "$status" >&2
  exit "$status"
}}
trap 'report_failure "$LINENO"' ERR
stage="verify-release"
test "$(cat /etc/massar/cluster-id)" = "massar-production"
test "$(sudo docker image inspect {image} --format '{{{{.Id}}}}')" = "{digest}"
test "$(sha256sum /opt/massar/releases/{args.release}/manifest.json | awk '{{print $1}}')" = "{manifest_sha256}"
test "$(python3 -c 'import json; print(json.load(open("/opt/massar/current/manifest.json"))["releaseId"])')" = "{gate.current_release_id}"
stage="verify-database-identity"
system_identifier="$(sudo docker run --rm --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt -v ON_ERROR_STOP=1 -c "select system_identifier from pg_control_system();"' )"
test "$system_identifier" = "{gate.database_system_identifier}"
migration_hash() {{
  sudo docker run --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; exec psql -h 127.0.0.1 -p 6432 -U postgres -d massar_platform -XAt -v ON_ERROR_STOP=1 -c "$1"' \
    sh 'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";' |
    sha256sum | awk '{{print $1}}'
}}
stage="verify-pre-migration-hash"
test "$(migration_hash)" = "{gate.pre_migration_ids_sha256}"
stage="clean-audit-database"
audit_db="massar_audit_$(date -u +%Y%m%d%H%M%S)"
case "$audit_db" in massar_audit_[0-9]*) ;; *) exit 40 ;; esac
cleanup() {{
  sudo docker run --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; dropdb --if-exists --force -h 127.0.0.1 -p 6432 -U postgres "$1"' sh "$audit_db" >/dev/null 2>&1 || true
}}
trap cleanup EXIT
stage="create-clean-audit-database"
sudo docker run --rm --network host \
  -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgsuper)"; createdb -h 127.0.0.1 -p 6432 -U postgres -O massar_app "$1"' sh "$audit_db"
stage="migrate-clean-audit-database"
sudo docker run --rm --network host \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  --user 0:0 \
  --entrypoint /bin/sh {image} -ec \
  'database_password="$(cat /run/secrets/pgapp)";
   export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=6432;Database=$1;Username=massar_app;Password=$database_password";
   unset database_password;
   exec setpriv --reuid=65532 --regid=65532 --clear-groups dotnet NaderGorge.Migrator.dll' sh "$audit_db"
stage="validate-clean-audit-database"
audit_sql="$(cat <<'SQL'
select concat_ws('|',
  (select count(*) from pg_index where not indisvalid),
  (select count(*) from pg_constraint
    where connamespace='public'::regnamespace and not convalidated),
  (select count(*) from cluster_leases),
  (select case when count(*) = 4 then 0 else 1 end
    from roles where "Name" in ('Admin','Teacher','Assistant','Student')));
SQL
)"
audit_result="$(sudo docker run --rm --network host \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgapp)";
   exec psql -h 127.0.0.1 -p 6432 -U massar_app -d "$1" \
     -XAt -v ON_ERROR_STOP=1 -c "$2"' \
  sh "$audit_db" "$audit_sql")"
if test "$audit_result" != "0|0|0|0"; then
  if ! printf '%s' "$audit_result" |
    grep -Eq '^[0-9]+[|][0-9]+[|][0-9]+[|][0-9]+$'; then
    audit_result="invalid-output"
  fi
  printf 'MASSAR_CLEAN_AUDIT_RESULT values=%s\n' "$audit_result" >&2
  exit 41
fi
stage="cleanup-clean-audit-database"
cleanup
trap - EXIT
stage="production-migration"
sudo docker run --rm --network host \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  --user 0:0 \
  --entrypoint /bin/sh {image} -ec \
  'database_password="$(cat /run/secrets/pgapp)";
   export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=6432;Database=massar_platform;Username=massar_app;Password=$database_password";
   unset database_password;
   exec setpriv --reuid=65532 --regid=65532 --clear-groups dotnet NaderGorge.Migrator.dll'
stage="verify-post-migration-hash"
test "$(migration_hash)" = "{gate.post_migration_ids_sha256}"
stage="verify-production-indexes"
sudo docker run --rm --network host \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  postgres:16-alpine sh -ec \
  'export PGPASSWORD="$(cat /run/secrets/pgapp)";
   psql -h 127.0.0.1 -p 6432 -U massar_app -d massar_platform \
     -XAt -v ON_ERROR_STOP=1 \
     -c "select count(*) from pg_index where not indisvalid;"' | grep -qx 0
stage="success"
"""
    completed = transport.run(target, ("bash", "-lc", script), timeout_seconds=600, check=False)
    if completed.returncode:
        marker = re.search(
            r"MASSAR_MIGRATION_FAILURE stage=[a-z0-9-]+ line=[0-9]+ status=[0-9]+",
            f"{completed.stderr}\n{completed.stdout}",
        )
        audit_detail = re.search(
            r"MASSAR_CLEAN_AUDIT_RESULT values=(?:[0-9]+\|[0-9]+\|[0-9]+\|[0-9]+|invalid-output)",
            f"{completed.stderr}\n{completed.stdout}",
        )
        detail = marker.group(0) if marker else "remote stage marker unavailable"
        if audit_detail:
            detail = f"{detail}; {audit_detail.group(0)}"
        raise MigrationError(
            "clean audit or production migration gate failed; automatic database "
            "Down/restore is prohibited and a reviewed forward-only corrective "
            f"migration is required: {detail}"
        )
    print(json.dumps({
        "release": args.release,
        "status": "success",
        "node": node.id,
        "retainedSchemaOnApplicationRollback": True,
        "schemaFailureDisposition": "reviewed-forward-fix-only",
    }))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (
        MigrationError, ReleaseContractError, OSError, ValueError,
        json.JSONDecodeError,
    ) as exc:
        print(f"migration blocked: {exc}", file=sys.stderr)
        raise SystemExit(6)
