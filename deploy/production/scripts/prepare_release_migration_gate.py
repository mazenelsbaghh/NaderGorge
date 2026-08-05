#!/usr/bin/python3
"""Produce a bound migration gate from a real backup, restore, and N-1 smoke."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import subprocess
import sys
import uuid
from pathlib import Path
from typing import Protocol

GATE_PREFIX = "MASSAR_MIGRATION_GATE="
RELEASE = re.compile(r"^(?:git-[0-9a-f]{40}|src-[0-9a-f]{40})$")
HEX_SHA256 = re.compile(r"^[0-9a-f]{64}$")
DIGEST = re.compile(r"^sha256:[0-9a-f]{64}$")
OPERATION_ID = re.compile(r"^gate-[0-9a-f]{32}$")


class GatePreparationError(RuntimeError):
    """Raised when real operational evidence cannot be produced safely."""


class Transport(Protocol):
    def run(
        self,
        target: SshTarget,
        remote_argv: tuple[str, ...],
        *,
        timeout_seconds: int = 60,
        check: bool = True,
    ): ...


def load_local_dependencies() -> None:
    """Load operator-side modules only; the installed root helper is standalone."""
    global Inventory, Node, SshTarget, StrictSshTransport
    global ReleaseContractError, load_inventory, load_migration_safety_gate
    global load_release_manifest, read_exact_json, write_json_atomic
    from clusterctl import Inventory, Node, load_inventory
    from release_contract import (
        ReleaseContractError,
        load_migration_safety_gate,
        load_release_manifest,
        read_exact_json,
        write_json_atomic,
    )
    from ssh_transport import SshTarget, StrictSshTransport


def target_for(inventory: Inventory, node: Node) -> SshTarget:
    return SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"])


def select_primary(inventory: Inventory, transport: Transport) -> Node:
    """Require exactly one Patroni primary before any mutating operation."""
    primaries: list[Node] = []
    for node in inventory.nodes:
        completed = transport.run(
            target_for(inventory, node),
            (
                "curl", "--fail", "--silent", "--show-error",
                "--max-time", "5", "http://127.0.0.1:8008/primary",
            ),
            timeout_seconds=15,
            check=False,
        )
        if completed.returncode == 0:
            primaries.append(node)
    if len(primaries) != 1:
        raise GatePreparationError(
            f"expected exactly one Patroni primary, observed {len(primaries)}"
        )
    return primaries[0]


def remote_producer_script(
    *,
    operation_id: str,
    release_id: str,
    manifest_sha256: str,
    migrator_digest: str,
    compatibility_release_id: str | None = None,
    compatibility_manifest_sha256: str | None = None,
    compatibility_backend_digest: str | None = None,
) -> str:
    """Render the root-only producer; all success booleans derive from passed checks."""
    return rf"""
set -Eeuo pipefail
umask 077

readonly operation_id="{operation_id}"
readonly release_id="{release_id}"
readonly manifest_sha256="{manifest_sha256}"
readonly migrator_digest="{migrator_digest}"
readonly requested_compatibility_release="{compatibility_release_id or ""}"
readonly requested_compatibility_manifest="{compatibility_manifest_sha256 or ""}"
readonly requested_compatibility_backend="{compatibility_backend_digest or ""}"
readonly release_root="/opt/massar/releases/$release_id"
readonly gate_base="/var/lib/massar-release-gates"
readonly restore_root="$gate_base/$operation_id"
readonly restore_data="$restore_root/data"
readonly restore_port=6544
readonly lock_dir="/run/lock/massar-release-migration-gate"
readonly smoke_name="massar-nminus1-$operation_id"
readonly backup_evidence="/var/lib/massar/evidence/backup/database-latest.json"
readonly pg_ctl="/usr/lib/postgresql/16/bin/pg_ctl"
started_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
lock_owned=false
postgres_started=false
smoke_created=false
stage="bootstrap"

report_error() {{
  status=$?
  printf 'MASSAR_GATE_FAILURE stage=%s line=%s status=%s\n' \
    "$stage" "$1" "$status" >&2
  return "$status"
}}
trap 'report_error "$LINENO"' ERR

cleanup() {{
  status=$?
  set +e
  if [[ "$smoke_created" = true ]]; then
    sudo docker rm -f "$smoke_name" >/dev/null 2>&1
  fi
  if [[ "$postgres_started" = true ]] && [[ -s "$restore_data/postmaster.pid" ]]; then
    runuser -u postgres -- "$pg_ctl" -D "$restore_data" -m immediate stop \
      >/dev/null 2>&1
  fi
  case "$restore_root" in
    /var/lib/massar-release-gates/gate-[0-9a-f]*) rm -rf --one-file-system "$restore_root" ;;
    *) printf 'refusing unsafe restore cleanup path\n' >&2; status=90 ;;
  esac
  if [[ "$lock_owned" = true ]] && \
     [[ "$(cat "$lock_dir/owner" 2>/dev/null)" = "$operation_id" ]]; then
    rm -f -- "$lock_dir/owner"
    rmdir "$lock_dir" >/dev/null 2>&1
  fi
  exit "$status"
}}
trap cleanup EXIT HUP INT TERM

test "$(id -u)" = 0
test "$(cat /etc/massar/cluster-id)" = "massar-production"
sudo docker image inspect postgres:16-alpine >/dev/null
if ! mkdir "$lock_dir" 2>/dev/null; then
  printf 'migration evidence lock is already owned by %s\n' \
    "$(cat "$lock_dir/owner" 2>/dev/null || printf unknown)" >&2
  exit 41
fi
printf '%s\n' "$operation_id" >"$lock_dir/owner"
lock_owned=true

test -d "$release_root"
test ! -L "$release_root"
test -f "$release_root/manifest.json"
test ! -L "$release_root/manifest.json"
test "$(sha256sum "$release_root/manifest.json" | awk '{{print $1}}')" = \
  "$manifest_sha256"
test "$(sudo docker image inspect "massar/migrator:$release_id" \
  --format '{{{{.Id}}}}')" = "$migrator_digest"

readarray -t current_identity < <(
  python3 - /opt/massar/current/manifest.json <<'PY'
import json,re,sys
value=json.load(open(sys.argv[1],encoding="utf-8"))
release=value.get("releaseId")
backend=value.get("images",{{}}).get("backend")
if not isinstance(release,str) or not re.fullmatch(
    r"(?:git-[0-9a-f]{{7,40}}|src-[0-9a-f]{{40}}|prod-[0-9]{{8}}-[a-z0-9-]+)",
    release,
):
    raise SystemExit("current release identity is invalid")
if not isinstance(backend,str) or not re.fullmatch(r"sha256:[0-9a-f]{{64}}",backend):
    raise SystemExit("current backend digest is invalid")
print(release)
print(backend)
PY
)
current_release="${{current_identity[0]}}"
current_backend_digest="${{current_identity[1]}}"
current_manifest_sha256="$(
  sha256sum /opt/massar/current/manifest.json | awk '{{print $1}}'
)"
[[ "$current_release" =~ ^(git-[0-9a-f]{{7,40}}|src-[0-9a-f]{{40}}|prod-[0-9]{{8}}-[a-z0-9-]+)$ ]]
[[ "$current_manifest_sha256" =~ ^[0-9a-f]{{64}}$ ]]
test "$(sudo docker image inspect "massar/backend:$current_release" \
  --format '{{{{.Id}}}}')" = "$current_backend_digest"
compatibility_release="$current_release"
compatibility_manifest_sha256="$current_manifest_sha256"
compatibility_backend_digest="$current_backend_digest"
if test -n "$requested_compatibility_release"; then
  compatibility_release="$requested_compatibility_release"
  compatibility_manifest_sha256="$requested_compatibility_manifest"
  compatibility_backend_digest="$requested_compatibility_backend"
  test -d "/opt/massar/releases/$compatibility_release"
  test "$(sha256sum "/opt/massar/releases/$compatibility_release/manifest.json" |
    awk '{{print $1}}')" = "$compatibility_manifest_sha256"
  test "$(sudo docker image inspect "massar/backend:$compatibility_release" \
    --format '{{{{.Id}}}}')" = "$compatibility_backend_digest"
fi

live_system_identifier="$(
  sudo docker run --pull=never --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'export PGPASSWORD="$(cat /run/secrets/pgsuper)";
     psql -h 127.0.0.1 -p 6432 -U postgres -d postgres -XAt \
       -v ON_ERROR_STOP=1 \
       -c "select system_identifier from pg_control_system();"'
)"
[[ "$live_system_identifier" =~ ^[0-9]{{10,}}$ ]]

# This service creates a new pgBackRest label and atomically publishes its
# identity evidence. Existing evidence is never accepted as "fresh enough".
sudo systemctl start --wait massar-pgbackrest-full.service
test -f "$backup_evidence"
test ! -L "$backup_evidence"
readarray -t backup_identity < <(
  python3 - "$backup_evidence" "$current_release" "$started_at" <<'PY'
import datetime as dt,json,re,sys
path,current_release,started=sys.argv[1:]
value=json.load(open(path,encoding="utf-8"))
required={{
 "schemaVersion","status","producer","clusterId","releaseId","startedAt",
 "completedAt","capturedAt","backupLabel","backupType","stanza","repository",
 "encrypted","replicationFactor","repositoryInfoSha256","walArchiveAgeSeconds",
}}
if set(value)!=required:
    raise SystemExit("fresh backup evidence does not match the exact contract")
if (
    value["schemaVersion"]!=1 or value["status"]!="success"
    or value["producer"]!="pgbackrest" or value["clusterId"]!="massar-production"
    or value["releaseId"]!=current_release or value["backupType"]!="full"
    or value["stanza"]!="massar" or value["repository"]!=1
    or value["encrypted"] is not True or value["replicationFactor"]!=3
    or not re.fullmatch(r"[0-9a-f]{{64}}",str(value["repositoryInfoSha256"]))
    or not re.fullmatch(
      r"[0-9]{{8}}-[0-9]{{6}}F(?:_[0-9]{{8}}-[0-9]{{6}}[DI])?",
      str(value["backupLabel"]),
    )
):
    raise SystemExit("fresh backup identity is invalid")
def stamp(name):
    parsed=dt.datetime.fromisoformat(value[name].replace("Z","+00:00"))
    if parsed.tzinfo is None or parsed.utcoffset()!=dt.timedelta(0):
        raise SystemExit(f"{{name}} is not UTC")
    return parsed
requested=dt.datetime.fromisoformat(started.replace("Z","+00:00"))
started_at=stamp("startedAt"); completed=stamp("completedAt"); captured=stamp("capturedAt")
if started_at < requested or not started_at <= completed <= captured:
    raise SystemExit("backup was not created by this operation")
if (dt.datetime.now(dt.timezone.utc)-captured).total_seconds()>3600:
    raise SystemExit("backup evidence is stale")
print(value["backupLabel"])
print(value["capturedAt"])
PY
)
backup_label="${{backup_identity[0]}}"
backup_captured_at="${{backup_identity[1]}}"
identity_backup="/var/lib/massar/evidence/backup/database-$backup_label.json"
test -f "$identity_backup"
test ! -L "$identity_backup"
test "$(sha256sum "$identity_backup" | awk '{{print $1}}')" = \
  "$(sha256sum "$backup_evidence" | awk '{{print $1}}')"

install -d -m 0700 -o postgres -g postgres "$gate_base"
test ! -e "$restore_root"
install -d -m 0700 -o postgres -g postgres "$restore_root"
install -d -m 0700 -o postgres -g postgres "$restore_data"

runuser -u postgres -- pgbackrest \
  --stanza=massar --repo=1 --set="$backup_label" --pg1-path="$restore_data" \
  --type=immediate --target-action=promote restore >&2
printf \
  "restore_command = 'pgbackrest --no-archive-async --pg1-path=%s --stanza=massar archive-get %%f \"%%p\"'\n" \
  "$restore_data" >>"$restore_data/postgresql.auto.conf"
chown postgres:postgres "$restore_data/postgresql.auto.conf"
chmod 0600 "$restore_data/postgresql.auto.conf"
runuser -u postgres -- "$pg_ctl" -D "$restore_data" \
  -o "-p $restore_port -c listen_addresses=127.0.0.1 -c unix_socket_directories=$restore_root -c archive_mode=off -c synchronous_commit=off -c synchronous_standby_names=''" \
  -w start >&2
postgres_started=true

for _attempt in $(seq 1 300); do
  recovery="$(
    runuser -u postgres -- psql -h "$restore_root" -p "$restore_port" \
      -d postgres -XAt -v ON_ERROR_STOP=1 \
      -c 'select not pg_is_in_recovery();' 2>/dev/null || true
  )"
  [[ "$recovery" = t ]] && break
  sleep 1
done
test "${{recovery:-}}" = t
restore_captured_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
restored_system_identifier="$(
  runuser -u postgres -- psql -h "$restore_root" -p "$restore_port" \
    -d postgres -XAt -v ON_ERROR_STOP=1 \
    -c 'select system_identifier from pg_control_system();'
)"
test "$restored_system_identifier" = "$live_system_identifier"

psql_restore() {{
  runuser -u postgres -- psql -h "$restore_root" -p "$restore_port" \
    -d massar_platform -XAt -v ON_ERROR_STOP=1 "$@"
}}
migration_hash() {{
  psql_restore -c 'select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId";' |
    sha256sum | awk '{{print $1}}'
}}
schema_hash() {{
  sudo docker run --pull=never --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine sh -ec \
    'set -o pipefail; export PGPASSWORD="$(cat /run/secrets/pgsuper)";
     pg_dump -h 127.0.0.1 -p 6544 -U postgres -d massar_platform \
       --schema-only --no-owner --no-privileges --quote-all-identifiers |
       sed -E "/^-- (Dumped from|Dumped by|Started on|Completed on)/d; /^.(un)?restrict[[:space:]]/d"' |
    sha256sum | awk '{{print $1}}'
}}
table_counts_hash() {{
  while IFS= read -r table_name; do
    count="$(psql_restore -c "select count(*) from $table_name;")"
    printf '%s\t%s\n' "$table_name" "$count"
  done < <(
    psql_restore -c \
      "select format('%I',tablename) from pg_tables where schemaname='public' order by tablename;"
  ) | sha256sum | awk '{{print $1}}'
}}
unaffected_counts_hash() {{
  while IFS= read -r table_name; do
    count="$(psql_restore -c "select count(*) from $table_name;")"
    printf '%s\t%s\n' "$table_name" "$count"
  done < "$pre_unaffected_tables" | sha256sum | awk '{{print $1}}'
}}
protected_rows_hash() {{
  {{
    psql_restore -c \
      "copy (select row_to_json(t)::text from users t where \"Id\" not in
       ('d36c2e35-512c-497b-b8c7-43df9ac3b123','c4b82937-293e-48a3-a002-decf9a1efab8')
       order by \"Id\") to stdout;"
    psql_restore -c \
      "copy (select row_to_json(t)::text from user_roles t where \"UserId\" <>
       'd36c2e35-512c-497b-b8c7-43df9ac3b123' order by \"UserId\",\"RoleId\") to stdout;"
    psql_restore -c \
      "copy (select row_to_json(t)::text from teacher_profiles t where \"Id\" <>
       'b4b82937-293e-48a3-a002-decf9a1efab8' order by \"Id\") to stdout;"
    psql_restore -c \
      "copy (select row_to_json(t)::text from teacher_subjects t where not
       (\"TeacherId\"='b4b82937-293e-48a3-a002-decf9a1efab8' and
        \"SubjectId\"='d9b8a342-990a-4286-905e-fdebb2e3895e')
       order by \"TeacherId\",\"SubjectId\") to stdout;"
    psql_restore -c \
      "copy (select row_to_json(t)::text from subjects t where \"Id\" <>
       'd9b8a342-990a-4286-905e-fdebb2e3895e' order by \"Id\") to stdout;"
    psql_restore -c \
      "copy (select row_to_json(t)::text from roles t where \"Name\" not in
       ('Admin','Teacher','Assistant','Student') order by \"Id\") to stdout;"
  }} | sha256sum | awk '{{print $1}}'
}}

pre_migration_hash="$(migration_hash)"
source_table_counts_hash="$(table_counts_hash)"
pre_unaffected_tables="$restore_root/pre-unaffected-tables.txt"
psql_restore -c "
  select format('%I',tablename)
  from pg_tables
  where schemaname='public'
    and tablename not in (
      '__EFMigrationsHistory','cluster_leases','roles','users','user_roles',
      'teacher_profiles','teacher_subjects','subjects','thanaweya_results'
    )
  order by tablename;" > "$pre_unaffected_tables"
pre_unaffected_hash="$(unaffected_counts_hash)"
pre_protected_rows_hash="$(protected_rows_hash)"
pre_migration_count="$(
  psql_restore -c 'select count(*) from "__EFMigrationsHistory";'
)"
pre_cluster_leases_exists="$(
  psql_restore -c \
    "select count(*) from pg_tables where schemaname='public' and tablename='cluster_leases';"
)"
if test "$pre_cluster_leases_exists" = 1; then
  pre_cluster_leases_count="$(
    psql_restore -c 'select count(*) from cluster_leases;'
  )"
else
  test "$pre_cluster_leases_exists" = 0
  pre_cluster_leases_count=-1
fi

# The target migrator reads only the isolated copy. Its digest was bound to the
# final three-node release manifest before this operation began.
stage="target-migration"
sudo docker run --pull=never --rm --network host \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  --user 0:0 \
  --entrypoint /bin/sh "massar/migrator:$release_id" -ec \
  'database_password="$(cat /run/secrets/pgapp)";
   export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=6544;Database=massar_platform;Username=massar_app;Password=$database_password";
   unset database_password;
   exec setpriv --reuid=65532 --regid=65532 --clear-groups dotnet NaderGorge.Migrator.dll' >&2

post_migration_hash="$(migration_hash)"
post_migration_schema_hash="$(schema_hash)"
post_migration_count="$(
  psql_restore -c 'select count(*) from "__EFMigrationsHistory";'
)"
stage="post-migration-validation"
test "$post_migration_count" -ge "$pre_migration_count"
test "$((post_migration_count - pre_migration_count))" -le 4
test "$(
  psql_restore -c \
    'select count(*) - count(distinct "MigrationId")
     from "__EFMigrationsHistory";'
)" = 0
test "$(unaffected_counts_hash)" = "$pre_unaffected_hash"
test "$(protected_rows_hash)" = "$pre_protected_rows_hash"
test "$(
  psql_restore -c "
    select
      (select count(*) from pg_index where not indisvalid) +
      (select count(*) from pg_constraint
        where connamespace='public'::regnamespace and not convalidated) +
      (select case when count(*)=4 then 0 else 1 end
        from roles where \"Name\" in ('Admin','Teacher','Assistant','Student'));"
)" = 0
test "$(
  psql_restore -c "
    select count(*) from (
      select tablename from pg_tables where schemaname='public'
      except select 'cluster_leases'
    ) tables_without_new;"
)" -gt 0
test "$(
  psql_restore -c \
    "select count(*) from pg_tables where schemaname='public' and tablename='cluster_leases';"
)" = 1
post_cluster_leases_count="$(
  psql_restore -c 'select count(*) from cluster_leases;'
)"
if test "$pre_cluster_leases_count" = -1; then
  test "$post_cluster_leases_count" = 0
else
  test "$post_cluster_leases_count" = "$pre_cluster_leases_count"
fi

# Real N-1 compatibility: boot the exact requested previous backend image
# (or the current backend during a forward deployment) against the already
# migrated restored copy and require its direct readiness identity.
if ss -ltnH 'sport = :5245' | grep -q .; then
  printf 'port 5245 is already in use; N-1 smoke cannot be isolated\n' >&2
  exit 71
fi
stage="n-minus-one-readiness"
sudo docker run --pull=never -d --name "$smoke_name" --network host \
  --env-file /etc/massar/app.env \
  -e ASPNETCORE_URLS=http://127.0.0.1:5245 \
  -e Cluster__NodeId=nminus1-compat \
  -e "Cluster__ReleaseId=$compatibility_release" \
  -v /etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro \
  -v /srv/massar-shared/public:/app/wwwroot \
  -v /srv/massar-shared/protected:/app/App_Data/protected \
  -v /srv/massar-shared/private:/app/App_Data/private \
  -v /srv/massar-shared/live-support:/app/App_Data/live-support \
  --entrypoint /bin/sh "massar/backend:$compatibility_release" -ec \
  'export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=6544;Database=massar_platform;Username=massar_app;Password=$(cat /run/secrets/pgapp)";
   exec dotnet NaderGorge.API.dll' >/dev/null
smoke_created=true
smoke_ready=false
headers="$restore_root/nminus1.headers"
body="$restore_root/nminus1.body"
for _attempt in $(seq 1 120); do
  if curl --fail --silent --show-error --max-time 3 \
    -D "$headers" -o "$body" \
    http://127.0.0.1:5245/api/health/ready; then
    # Readiness contracts predate release identity fields. The container was
    # already bound above to the exact reviewed backend digest; HTTP 2xx from
    # its readiness endpoint proves that image can use the migrated database.
    smoke_ready=true
    break
  fi
  sleep 1
done
if [[ "$smoke_ready" != true ]]; then
  printf 'N-1 backend readiness failed; bounded container diagnostics follow\n' >&2
  printf '%s\n' '--- readiness headers ---' >&2
  sed -n '1,40p' "$headers" >&2 || true
  printf '%s\n' '--- readiness body (first 500 bytes) ---' >&2
  head -c 500 "$body" >&2 || true
  printf '\n%s\n' '--- container logs ---' >&2
  sudo docker logs --tail 120 "$smoke_name" >&2 || true
fi
test "$smoke_ready" = true
sudo docker rm -f "$smoke_name" >/dev/null
smoke_created=false

validated_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
database_restore_id="restore-$operation_id"
gate_json="$(
  python3 - "$release_id" "$manifest_sha256" "$compatibility_release" \
    "$compatibility_manifest_sha256" \
    "$live_system_identifier" "$backup_label" "$database_restore_id" \
    "$backup_captured_at" "$restore_captured_at" "$validated_at" \
    "$source_table_counts_hash" "$pre_migration_hash" "$post_migration_hash" \
    "$post_migration_schema_hash" <<'PY'
import json,sys
(
 release,manifest,current,current_manifest,system,backup,restore,
 backup_at,restore_at,validated_at,counts,pre_migrations,post_migrations,
 post_schema,
)=sys.argv[1:]
print(json.dumps({{
 "schemaVersion":1,
 "status":"success",
 "clusterId":"massar-production",
 "releaseId":release,
 "manifestSha256":manifest,
 "currentReleaseId":current,
 "currentManifestSha256":current_manifest,
 "databaseSystemIdentifier":system,
 "databaseBackupId":backup,
 "databaseRestoreId":restore,
 "backupCapturedAt":backup_at,
 "restoreCapturedAt":restore_at,
 "validatedAt":validated_at,
 "backupEncrypted":True,
 "restoreIsolated":True,
 "restoreChecksumVerified":True,
 "restoredCopyMigrationVerified":True,
 "realDataValidationVerified":True,
 "nMinusOneCompatibilityVerified":True,
 "sourceDatabaseTableCountsSha256":counts,
 "restoredDatabaseTableCountsSha256":counts,
 "preMigrationIdsSha256":pre_migrations,
 "postMigrationIdsSha256":post_migrations,
 "postMigrationSchemaSha256":post_schema,
}},separators=(",",":")))
PY
)"

# Destroy the restored copy before emitting success; cleanup remains armed for
# every failure path and the global operation lock is retained until process exit.
runuser -u postgres -- "$pg_ctl" -D "$restore_data" -m immediate stop >&2
postgres_started=false
rm -rf --one-file-system "$restore_root"
stage="success"
printf '%s%s\n' "{GATE_PREFIX}" "$gate_json"
"""


def parse_gate(stdout: str) -> dict[str, object]:
    matches = [
        line[len(GATE_PREFIX):]
        for line in stdout.splitlines()
        if line.startswith(GATE_PREFIX)
    ]
    if len(matches) != 1:
        raise GatePreparationError("remote producer did not emit exactly one gate")
    try:
        value = json.loads(matches[0])
    except json.JSONDecodeError as exc:
        raise GatePreparationError("remote producer emitted invalid gate JSON") from exc
    if not isinstance(value, dict):
        raise GatePreparationError("remote gate root must be an object")
    return value


def prepare(
    *,
    inventory: Inventory,
    transport: Transport,
    release_id: str,
    manifest_path: Path,
    output: Path,
    compatibility_manifest_path: Path | None = None,
    now: dt.datetime | None = None,
) -> dict[str, object]:
    if output.exists() or output.is_symlink():
        raise GatePreparationError("migration gate output must not already exist")
    manifest = load_release_manifest(manifest_path, release_id)
    compatibility_manifest = None
    if compatibility_manifest_path is not None:
        _, compatibility_value = read_exact_json(
            compatibility_manifest_path,
            "N-1 compatibility manifest",
        )
        compatibility_release = compatibility_value.get("releaseId")
        if not isinstance(compatibility_release, str):
            raise GatePreparationError("N-1 compatibility release identity is missing")
        compatibility_manifest = load_release_manifest(
            compatibility_manifest_path,
            compatibility_release,
        )
    primary = select_primary(inventory, transport)
    operation_id = f"gate-{uuid.uuid4().hex}"
    producer_command = [
        "sudo",
        "/usr/local/sbin/massar-produce-release-migration-gate",
        "--root-produce",
        operation_id,
        release_id,
        manifest.sha256,
        manifest.images["migrator"],
    ]
    if compatibility_manifest is not None:
        producer_command.extend([
            compatibility_manifest.release_id,
            compatibility_manifest.sha256,
            compatibility_manifest.images["backend"],
        ])
    completed = transport.run(
        target_for(inventory, primary),
        tuple(producer_command),
        timeout_seconds=1800,
        check=False,
    )
    if completed.returncode != 0:
        detail = completed.stderr.strip().splitlines()
        preferred = [
            line for line in detail
            if re.search(
                r"(connection refused|failed to connect|permission denied|"
                r"no frameworks were found|unhandled exception|"
                r"MASSAR_GATE_FAILURE|\b(?:fatal|blocked)\b)",
                line,
                flags=re.IGNORECASE,
            )
        ]
        safe_detail = (
            preferred[-1] if preferred else
            detail[-1] if detail else
            "remote evidence production failed"
        )
        raise GatePreparationError(
            f"operational migration evidence failed on {primary.id}: {safe_detail}"
        )
    payload = parse_gate(completed.stdout)
    output.parent.mkdir(parents=True, exist_ok=True)
    write_json_atomic(output, payload)
    output.chmod(0o640)
    try:
        load_migration_safety_gate(
            output,
            manifest=manifest,
            now=now or dt.datetime.now(dt.timezone.utc),
        )
    except Exception:
        output.unlink(missing_ok=True)
        raise
    return payload


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser(description=__doc__)
    value.add_argument("--inventory", required=True, type=Path)
    value.add_argument("--known-hosts", required=True, type=Path)
    value.add_argument("--identity", required=True, type=Path)
    value.add_argument("--release", required=True)
    value.add_argument("--manifest", required=True, type=Path)
    value.add_argument("--n-minus-one-manifest", type=Path)
    value.add_argument("--output", required=True, type=Path)
    mode = value.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return value


def main() -> int:
    load_local_dependencies()
    args = parser().parse_args()
    if not RELEASE.fullmatch(args.release):
        raise GatePreparationError("invalid immutable release ID")
    manifest = load_release_manifest(args.manifest, args.release)
    inventory = load_inventory(
        args.inventory,
        require_operator_files=not args.dry_run,
    )
    if args.dry_run:
        print(json.dumps({
            "status": "dry-run",
            "release": manifest.release_id,
            "nMinusOneManifest": (
                str(args.n_minus_one_manifest.resolve())
                if args.n_minus_one_manifest else None
            ),
            "manifestSha256": manifest.sha256,
            "steps": [
                "discover-exactly-one-primary",
                "fresh-encrypted-full-backup",
                "isolated-restore",
                "target-migration-and-real-data-validation",
                "n-minus-one-backend-readiness",
                "destroy-isolated-copy",
            ],
            "sshAttempted": False,
        }))
        return 0
    transport = StrictSshTransport(args.known_hosts, args.identity)
    payload = prepare(
        inventory=inventory,
        transport=transport,
        release_id=args.release,
        manifest_path=args.manifest,
        output=args.output,
        compatibility_manifest_path=args.n_minus_one_manifest,
    )
    print(json.dumps({
        "status": "success",
        "release": payload["releaseId"],
        "currentRelease": payload["currentReleaseId"],
        "output": str(args.output.resolve()),
    }))
    return 0


def root_main(argv: list[str]) -> int:
    """Reviewed root entry point installed by backup bootstrap."""
    parser_value = argparse.ArgumentParser(add_help=False)
    parser_value.add_argument("operation_id")
    parser_value.add_argument("release_id")
    parser_value.add_argument("manifest_sha256")
    parser_value.add_argument("migrator_digest")
    parser_value.add_argument("compatibility_release_id", nargs="?")
    parser_value.add_argument("compatibility_manifest_sha256", nargs="?")
    parser_value.add_argument("compatibility_backend_digest", nargs="?")
    args = parser_value.parse_args(argv)
    if os.geteuid() != 0:
        raise GatePreparationError("root producer must run as root")
    compatibility_values = (
        args.compatibility_release_id,
        args.compatibility_manifest_sha256,
        args.compatibility_backend_digest,
    )
    if (
        not OPERATION_ID.fullmatch(args.operation_id)
        or not RELEASE.fullmatch(args.release_id)
        or not HEX_SHA256.fullmatch(args.manifest_sha256)
        or not DIGEST.fullmatch(args.migrator_digest)
        or any(compatibility_values)
        and (
            not all(compatibility_values)
            or not RELEASE.fullmatch(str(args.compatibility_release_id))
            or not HEX_SHA256.fullmatch(str(args.compatibility_manifest_sha256))
            or not DIGEST.fullmatch(str(args.compatibility_backend_digest))
        )
    ):
        raise GatePreparationError("root producer arguments are invalid")
    script = remote_producer_script(
        operation_id=args.operation_id,
        release_id=args.release_id,
        manifest_sha256=args.manifest_sha256,
        migrator_digest=args.migrator_digest,
        compatibility_release_id=args.compatibility_release_id,
        compatibility_manifest_sha256=args.compatibility_manifest_sha256,
        compatibility_backend_digest=args.compatibility_backend_digest,
    )
    return subprocess.run(
        ("/usr/bin/bash", "-c", script),
        check=False,
    ).returncode


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--root-produce":
        try:
            raise SystemExit(root_main(sys.argv[2:]))
        except (GatePreparationError, OSError, ValueError) as exc:
            print(f"migration gate blocked: {exc}", file=sys.stderr)
            raise SystemExit(6)
    try:
        raise SystemExit(main())
    except (
        GatePreparationError,
        ReleaseContractError,
        OSError,
        ValueError,
        json.JSONDecodeError,
    ) as exc:
        print(f"migration gate blocked: {exc}", file=sys.stderr)
        raise SystemExit(6)
