#!/usr/bin/env bash
set -euo pipefail

source /etc/massar/backup/files.env
export RESTIC_REPOSITORY AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY AWS_DEFAULT_REGION RESTIC_PASSWORD_FILE
umask 077

exec 9>/srv/massar-shared/.restic-backup.lock
if ! flock --nonblock 9; then
  printf 'file backup skipped: another cluster node owns the backup lock\n'
  exit 0
fi

latest_marker=/srv/massar-shared/.cluster-health/files-backup-last
if [[ -f "$latest_marker" ]] && find "$latest_marker" -mmin -50 -print -quit | grep -q .; then
  printf 'file backup skipped: a fresh cluster snapshot already exists\n'
  exit 0
fi

started_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
summary_file="$(mktemp /tmp/massar-restic-backup.XXXXXX.jsonl)"
cleanup() {
  rm -f -- "$summary_file"
}
trap cleanup EXIT
nice -n 10 ionice -c 2 -n 7 \
  restic backup \
    --json \
    --host massar-cluster \
    --tag hourly \
    --exclude /srv/massar-shared/.restic-backup.lock \
    /srv/massar-shared | tee "$summary_file"

completed_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
captured_at="$completed_at"
snapshot_id="$(
  python3 - "$summary_file" <<'PY'
import json,re,sys
summaries=[]
with open(sys.argv[1],encoding="utf-8") as stream:
    for line in stream:
        value=json.loads(line)
        if value.get("message_type")=="summary":
            summaries.append(value)
if len(summaries)!=1:
    raise SystemExit("Restic backup did not emit exactly one summary")
snapshot=summaries[0].get("snapshot_id")
if not isinstance(snapshot,str) or not re.fullmatch(r"[0-9a-f]{64}",snapshot):
    raise SystemExit("Restic backup summary did not contain a full snapshot_id")
print(snapshot)
PY
)"
backup_summary_sha256="$(sha256sum "$summary_file" | awk '{print $1}')"
[[ "$backup_summary_sha256" =~ ^[0-9a-f]{64}$ ]]
release_id="$(
  python3 -c 'import json; print(json.load(open("/opt/massar/current/manifest.json", encoding="utf-8"))["releaseId"])'
)"
[[ "$release_id" =~ ^(git-[0-9a-f]{7,40}|src-[0-9a-f]{40})$ ]]
install -d -m 0775 -o root -g massar /srv/massar-shared/.cluster-health
temporary=/srv/massar-shared/.cluster-health/file-backup-latest.json.tmp
python3 - "$temporary" "$started_at" "$completed_at" "$captured_at" \
  "$snapshot_id" "$backup_summary_sha256" "$release_id" <<'PY'
import json,sys
(
    path,started,completed,captured,snapshot_id,
    backup_summary_sha256,release_id,
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
        "capturedAt":captured,
        "snapshotId":snapshot_id,
        "hostname":"massar-cluster",
        "paths":["/srv/massar-shared"],
        "encrypted":True,
        "replicationFactor":3,
        "backupSummarySha256":backup_summary_sha256,
        "snapshotAgeSeconds":0,
    },handle,indent=2)
    handle.write("\n")
PY
chmod 0640 "$temporary"
identity_evidence="/srv/massar-shared/.cluster-health/file-backup-$snapshot_id.json"
test ! -e "$identity_evidence"
ln -- "$temporary" "$identity_evidence"
mv "$temporary" /srv/massar-shared/.cluster-health/file-backup-latest.json
touch "$latest_marker"
