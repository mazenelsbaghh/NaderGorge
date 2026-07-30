#!/usr/bin/env bash
set -euo pipefail

source /etc/massar/backup/files.env
export RESTIC_REPOSITORY AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY AWS_DEFAULT_REGION RESTIC_PASSWORD_FILE
readonly BACKUP_EVIDENCE="/srv/massar-shared/.cluster-health/file-backup-latest.json"
umask 077

exec 9>/srv/massar-shared/.restic-restore-test.lock
if ! flock --nonblock 9; then
  printf 'file restore test skipped: another cluster node owns the restore lock\n'
  exit 0
fi

latest_marker=/srv/massar-shared/.cluster-health/files-restore-test-last
if [[ -f "$latest_marker" ]] && find "$latest_marker" -mtime -20 -print -quit | grep -q .; then
  printf 'file restore test skipped: a recent cluster restore test already exists\n'
  exit 0
fi

started_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
release_id="$(
  python3 -c 'import json; print(json.load(open("/opt/massar/current/manifest.json", encoding="utf-8"))["releaseId"])'
)"
[[ "$release_id" =~ ^(git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$ ]]
backup_evidence_sha256="$(sha256sum "$BACKUP_EVIDENCE" | awk '{print $1}')"
[[ "$backup_evidence_sha256" =~ ^[0-9a-f]{64}$ ]]
snapshot_id="$(
  python3 - "$BACKUP_EVIDENCE" "$release_id" "$started_at" <<'PY'
import datetime as dt,json,re,sys
path,release,restore_started=sys.argv[1:]
value=json.load(open(path,encoding="utf-8"))
required={
 "schemaVersion","status","producer","clusterId","releaseId","startedAt",
 "completedAt","capturedAt","snapshotId","hostname","paths","encrypted",
 "replicationFactor","backupSummarySha256","snapshotAgeSeconds",
}
if set(value)!=required:
    raise SystemExit("file backup evidence does not match the exact contract")
if (
    value["schemaVersion"]!=1 or value["status"]!="success"
    or value["producer"]!="restic" or value["clusterId"]!="massar-production"
    or value["releaseId"]!=release or value["hostname"]!="massar-cluster"
    or value["paths"]!=["/srv/massar-shared"] or value["encrypted"] is not True
    or value["replicationFactor"]!=3
    or not re.fullmatch(r"[0-9a-f]{64}",str(value["snapshotId"]))
    or not re.fullmatch(r"[0-9a-f]{64}",str(value["backupSummarySha256"]))
):
    raise SystemExit("file backup evidence is not trusted")
completed=dt.datetime.fromisoformat(value["completedAt"].replace("Z","+00:00"))
restore=dt.datetime.fromisoformat(restore_started.replace("Z","+00:00"))
if completed.tzinfo is None or restore.tzinfo is None or completed>restore:
    raise SystemExit("selected file snapshot completed after restore started")
print(value["snapshotId"])
PY
)"
identity_backup_evidence="/srv/massar-shared/.cluster-health/file-backup-$snapshot_id.json"
test -f "$identity_backup_evidence"
test ! -L "$identity_backup_evidence"
test "$(sha256sum "$identity_backup_evidence" | awk '{print $1}')" = \
  "$backup_evidence_sha256"
snapshot_info="$(mktemp /tmp/massar-restic-snapshot.XXXXXX.json)"
restic snapshots --json "$snapshot_id" >"$snapshot_info"
python3 - "$snapshot_info" "$snapshot_id" <<'PY'
import json,sys
value=json.load(open(sys.argv[1],encoding="utf-8"))
if (
    not isinstance(value,list) or len(value)!=1
    or value[0].get("id")!=sys.argv[2]
):
    raise SystemExit("Restic repository did not return the exact snapshot ID")
PY
snapshot_metadata_sha256="$(sha256sum "$snapshot_info" | awk '{print $1}')"
rm -f -- "$snapshot_info"
[[ "$snapshot_metadata_sha256" =~ ^[0-9a-f]{64}$ ]]

restore_root="$(mktemp -d /var/lib/massar-restore-tests/files.XXXXXX)"
cleanup() {
  rm -rf -- "$restore_root"
}
trap cleanup EXIT

restic check --read-data-subset=5%
restic restore "$snapshot_id" \
  --include /srv/massar-shared/.backup-restore-sentinel \
  --target "$restore_root"

restored="$restore_root/srv/massar-shared/.backup-restore-sentinel"
test -f "$restored"
expected="$(cut -d' ' -f1 /srv/massar-shared/.backup-restore-sentinel.sha256)"
actual="$(sha256sum "$restored" | cut -d' ' -f1)"
test "$actual" = "$expected"
evidence_dir=/var/lib/massar/evidence/restore
install -d -m 0750 -o root -g massar "$evidence_dir"
captured_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
completed_at="$captured_at"
cleanup
trap - EXIT
destroyed_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
captured_at="$destroyed_at"
temporary="$evidence_dir/files-latest.json.tmp"
python3 - "$temporary" "$started_at" "$completed_at" "$destroyed_at" \
  "$captured_at" "$actual" "$snapshot_id" "$snapshot_metadata_sha256" \
  "$backup_evidence_sha256" "$release_id" <<'PY'
import json,sys
(
    path,started,completed,destroyed,captured,checksum,snapshot_id,
    snapshot_metadata_sha256,backup_evidence_sha256,release_id,
)=sys.argv[1:]
with open(path,"w",encoding="utf-8") as handle:
    json.dump({
        "schemaVersion":1,
        "status":"success",
        "producer":"restic",
        "clusterId":"massar-production",
        "releaseId":release_id,
        "startedAt":started,
        "completedAt":completed,
        "destroyedAt":destroyed,
        "capturedAt":captured,
        "snapshotId":snapshot_id,
        "isolated":True,
        "productionTarget":False,
        "repositoryCheckOk":True,
        "checksumVerified":True,
        "fileSampleOk":True,
        "snapshotMetadataSha256":snapshot_metadata_sha256,
        "backupEvidenceSha256":backup_evidence_sha256,
        "checksum":checksum,
    },handle,indent=2)
    handle.write("\n")
PY
chmod 0640 "$temporary"
identity_evidence="$evidence_dir/files-$snapshot_id.json"
test ! -e "$identity_evidence"
ln -- "$temporary" "$identity_evidence"
mv "$temporary" "$evidence_dir/files-latest.json"
touch "$latest_marker"
printf 'isolated file restore verified\n'
