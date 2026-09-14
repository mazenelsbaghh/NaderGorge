#!/usr/bin/env bash
set -euo pipefail

readonly RESTORE_BASE="/var/lib/massar-restore-tests"
readonly EVIDENCE_DIR="/var/lib/massar/evidence/restore"
readonly BACKUP_EVIDENCE="/var/lib/massar/evidence/backup/database-latest.json"
umask 077
started_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
release_id="$(
  python3 -c 'import json; print(json.load(open("/opt/massar/current/manifest.json", encoding="utf-8"))["releaseId"])'
)"
[[ "$release_id" =~ ^(git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$ ]]
backup_evidence_sha256="$(sha256sum "$BACKUP_EVIDENCE" | awk '{print $1}')"
[[ "$backup_evidence_sha256" =~ ^[0-9a-f]{64}$ ]]
install -d -m 0700 -o postgres -g postgres "$RESTORE_BASE"
install -d -m 0750 -o root -g massar "$EVIDENCE_DIR"
restore_root="$(mktemp -d "$RESTORE_BASE/database.XXXXXX")"
case "$restore_root" in "$RESTORE_BASE"/database.*) ;; *) exit 40 ;; esac
chown postgres:postgres "$restore_root"
chmod 0700 "$restore_root"
cleanup() {
  if [[ -f "$restore_root/data/postmaster.pid" ]]; then
    runuser -u postgres -- /usr/lib/postgresql/16/bin/pg_ctl -D "$restore_root/data" -m immediate stop >/dev/null 2>&1 || true
  fi
  rm -rf --one-file-system "$restore_root"
}
trap cleanup EXIT
install -d -m 0700 -o postgres -g postgres "$restore_root/data"
probe_file=/srv/massar-shared/.cluster-health/pitr-target.json
test -s "$probe_file"
readarray -t probe_values < <(
  python3 - "$probe_file" <<'PY'
import datetime as dt,json,sys
value=json.load(open(sys.argv[1],encoding="utf-8"))
target=dt.datetime.fromisoformat(value["targetTime"].replace("Z","+00:00"))
now=dt.datetime.now(dt.timezone.utc)
age=(now-target).total_seconds()
if age < 0 or age > 300:
    raise SystemExit("PITR target is not within the last five minutes")
print(value["targetTime"])
print(value["probeId"])
PY
)
target_time="${probe_values[0]}"
probe_id="${probe_values[1]}"
[[ "$probe_id" =~ ^[0-9a-f]{32}$ ]]
backup_label="$(
  python3 - "$BACKUP_EVIDENCE" "$release_id" "$target_time" <<'PY'
import datetime as dt,json,re,sys
path,release,target=sys.argv[1:]
value=json.load(open(path,encoding="utf-8"))
required={
 "schemaVersion","status","producer","clusterId","releaseId","startedAt",
 "completedAt","capturedAt","backupLabel","backupType","stanza","repository",
 "encrypted","replicationFactor","repositoryInfoSha256","walArchiveAgeSeconds",
}
if set(value)!=required:
    raise SystemExit("database backup evidence does not match the exact contract")
if (
    value["schemaVersion"]!=1 or value["status"]!="success"
    or value["producer"]!="pgbackrest" or value["clusterId"]!="massar-production"
    or value["releaseId"]!=release or value["stanza"]!="massar"
    or value["repository"]!=1 or value["encrypted"] is not True
    or value["replicationFactor"]!=3
    or not re.fullmatch(r"[0-9a-f]{64}",str(value["repositoryInfoSha256"]))
    or not re.fullmatch(
      r"[0-9]{8}-[0-9]{6}F(?:_[0-9]{8}-[0-9]{6}[DI])?",
      str(value["backupLabel"]),
    )
):
    raise SystemExit("database backup evidence is not trusted")
completed=dt.datetime.fromisoformat(value["completedAt"].replace("Z","+00:00"))
recovery_target=dt.datetime.fromisoformat(target.replace("Z","+00:00"))
if completed.tzinfo is None or recovery_target.tzinfo is None or completed>recovery_target:
    raise SystemExit("selected database backup completed after the PITR target")
print(value["backupLabel"])
PY
)"
identity_backup_evidence="/var/lib/massar/evidence/backup/database-$backup_label.json"
test -f "$identity_backup_evidence"
test ! -L "$identity_backup_evidence"
test "$(sha256sum "$identity_backup_evidence" | awk '{print $1}')" = \
  "$backup_evidence_sha256"
backup_info="$(mktemp /tmp/massar-pgbackrest-restore.XXXXXX.json)"
runuser -u postgres -- pgbackrest \
  --stanza=massar --repo=1 --set="$backup_label" --output=json info >"$backup_info"
repository_info_sha256="$(sha256sum "$backup_info" | awk '{print $1}')"
rm -f -- "$backup_info"
[[ "$repository_info_sha256" =~ ^[0-9a-f]{64}$ ]]
runuser -u postgres -- pgbackrest \
  --stanza=massar \
  --repo=1 \
  --set="$backup_label" \
  --pg1-path="$restore_root/data" \
  --type=time \
  --target="$target_time" \
  --target-timeline=latest \
  --target-action=promote \
  restore
# The live cluster deliberately uses async archive-get with a shared spool for
# throughput. A restore drill must not reuse that live spool: stale "missing"
# notifications can make recovery stop before the requested WAL arrives.
# Override only the isolated copy to fetch WAL synchronously from the bucket.
printf \
  "restore_command = 'pgbackrest --no-archive-async --pg1-path=%s --stanza=massar archive-get %%f \"%%p\"'\n" \
  "$restore_root/data" >>"$restore_root/data/postgresql.auto.conf"
chown postgres:postgres "$restore_root/data/postgresql.auto.conf"
chmod 0600 "$restore_root/data/postgresql.auto.conf"
runuser -u postgres -- /usr/lib/postgresql/16/bin/pg_ctl \
  -D "$restore_root/data" \
  -o "-p 6543 -c listen_addresses=127.0.0.1 -c unix_socket_directories=$restore_root -c archive_mode=off" \
  -w start
# pg_ctl considers hot-standby readiness a successful start even while WAL is
# still being replayed. Do not inspect the restored data until PostgreSQL has
# reached the requested target and promoted the isolated instance.
recovery_complete=false
for _attempt in $(seq 1 300); do
  if [[ "$(
    runuser -u postgres -- psql \
      -h "$restore_root" -p 6543 -d postgres -XAt \
      -c 'select not pg_is_in_recovery();' 2>/dev/null || true
  )" = "t" ]]; then
    recovery_complete=true
    break
  fi
  sleep 1
done
test "$recovery_complete" = true || {
  printf 'restore verification failed: PITR did not reach the target within 300 seconds\n' >&2
  exit 55
}
migration="$(runuser -u postgres -- psql -h "$restore_root" -p 6543 -d massar_platform -XAt -c 'select max("MigrationId") from "__EFMigrationsHistory";')"
invalid="$(runuser -u postgres -- psql -h "$restore_root" -p 6543 -d massar_platform -XAt -c 'select count(*) from pg_index where not indisvalid;')"
roles="$(runuser -u postgres -- psql -h "$restore_root" -p 6543 -d massar_platform -XAt -c 'select count(*) from roles;')"
login_smoke="$(
  PGPASSWORD="$(tr -d '\n' </etc/massar/secrets/postgres-app-password)" \
    psql -h 127.0.0.1 -p 6543 -U massar_app -d massar_platform -XAt \
      -c 'select current_user;'
)"
restored_probe="$(runuser -u postgres -- psql -h "$restore_root" -p 6543 -d postgres -XAt -c "select probe_id from public.massar_pitr_restore_probe where probe_id = '$probe_id';")"
all_restored_probes="$(runuser -u postgres -- psql -h "$restore_root" -p 6543 -d postgres -XAt -c "select coalesce(string_agg(probe_id, ',' order by probe_id), '<empty>') from public.massar_pitr_restore_probe;")"
last_replay_timestamp="$(runuser -u postgres -- psql -h "$restore_root" -p 6543 -d postgres -XAt -c "select coalesce(pg_last_xact_replay_timestamp()::text, '<none>');")"
test -n "$migration" || { printf 'restore verification failed: migration history is empty\n' >&2; exit 51; }
test "$invalid" = "0" || { printf 'restore verification failed: invalid indexes=%s\n' "$invalid" >&2; exit 52; }
test "$roles" -gt 0 || { printf 'restore verification failed: roles=%s\n' "$roles" >&2; exit 53; }
test "$login_smoke" = "massar_app" || {
  printf 'restore verification failed: application login smoke failed\n' >&2
  exit 56
}
test "$restored_probe" = "$probe_id" || {
  printf 'restore verification failed: PITR probe mismatch expected=%s actual=%s target=%s lastReplay=%s\n' \
    "$probe_id" "$all_restored_probes" "$target_time" "$last_replay_timestamp" >&2
  exit 54
}
completed_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
cleanup
trap - EXIT
destroyed_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
captured_at="$destroyed_at"
temporary="$EVIDENCE_DIR/database-latest.json.tmp"
python3 - "$temporary" "$started_at" "$completed_at" "$destroyed_at" \
  "$captured_at" "$target_time" "$migration" "$backup_label" \
  "$repository_info_sha256" "$backup_evidence_sha256" "$release_id" <<'PY'
import json,sys
(
    path,started,completed,destroyed,captured,target,migration,backup_label,
    repository_info_sha256,backup_evidence_sha256,release_id,
)=sys.argv[1:]
with open(path,"w",encoding="utf-8") as handle:
    json.dump({
        "schemaVersion":1,
        "status":"success",
        "producer":"pgbackrest",
        "clusterId":"massar-production",
        "releaseId":release_id,
        "startedAt":started,
        "completedAt":completed,
        "destroyedAt":destroyed,
        "capturedAt":captured,
        "backupLabel":backup_label,
        "isolated":True,
        "productionTarget":False,
        "integrityOk":True,
        "migrationStateOk":True,
        "loginSmokeOk":True,
        "checksumVerified":True,
        "repositoryInfoSha256":repository_info_sha256,
        "backupEvidenceSha256":backup_evidence_sha256,
        "recoveryTarget":target,
        "latestMigration":migration,
    },handle,indent=2)
    handle.write("\n")
PY
chmod 0640 "$temporary"
identity_evidence="$EVIDENCE_DIR/database-$backup_label.json"
test ! -e "$identity_evidence"
ln -- "$temporary" "$identity_evidence"
mv "$temporary" "$EVIDENCE_DIR/database-latest.json"
