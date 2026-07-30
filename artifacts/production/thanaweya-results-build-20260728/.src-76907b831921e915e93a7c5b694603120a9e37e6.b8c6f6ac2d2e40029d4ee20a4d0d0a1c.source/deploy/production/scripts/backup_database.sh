#!/usr/bin/env bash
set -euo pipefail

backup_type="${1:-diff}"
if [[ "$backup_type" != "full" && "$backup_type" != "diff" ]]; then
  printf 'backup type must be full or diff\n' >&2
  exit 2
fi

if ! curl --fail --silent http://127.0.0.1:8008/primary >/dev/null; then
  printf 'database backup skipped: this node is not the Patroni primary\n'
  exit 0
fi

umask 077
started_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
before_info="$(mktemp /tmp/massar-pgbackrest-before.XXXXXX.json)"
after_info="$(mktemp /tmp/massar-pgbackrest-after.XXXXXX.json)"
cleanup() {
  rm -f -- "$before_info" "$after_info"
}
trap cleanup EXIT
runuser -u postgres -- pgbackrest \
  --stanza=massar --repo=1 --output=json info >"$before_info"
runuser -u postgres -- \
  nice -n 10 ionice -c 2 -n 7 \
  pgbackrest --stanza=massar --repo=1 --type="$backup_type" backup
runuser -u postgres -- pgbackrest \
  --stanza=massar --repo=1 --output=json info >"$after_info"

completed_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
captured_at="$completed_at"
backup_label="$(
  python3 - "$before_info" "$after_info" "$backup_type" <<'PY'
import json,re,sys
before_path,after_path,expected_type=sys.argv[1:]
pattern=re.compile(r"^[0-9]{8}-[0-9]{6}F(?:_[0-9]{8}-[0-9]{6}[DI])?$")
def backups(path):
    document=json.load(open(path,encoding="utf-8"))
    stanza=[item for item in document if item.get("name")=="massar"]
    if len(stanza)!=1:
        raise SystemExit("pgBackRest info did not contain exactly one massar stanza")
    return {
        item["label"]:item
        for item in stanza[0].get("backup",[])
        if isinstance(item,dict) and isinstance(item.get("label"),str)
    }
before=backups(before_path); after=backups(after_path)
created=[item for label,item in after.items() if label not in before]
if len(created)!=1:
    raise SystemExit("backup did not create exactly one new pgBackRest label")
item=created[0]; label=item["label"]
if not pattern.fullmatch(label) or item.get("type")!=expected_type:
    raise SystemExit("new pgBackRest label/type is invalid")
if not isinstance(item.get("timestamp"),dict) or not isinstance(item["timestamp"].get("stop"),int):
    raise SystemExit("new pgBackRest backup is not complete")
print(label)
PY
)"
repository_info_sha256="$(sha256sum "$after_info" | awk '{print $1}')"
[[ "$repository_info_sha256" =~ ^[0-9a-f]{64}$ ]]
release_id="$(
  python3 -c 'import json; print(json.load(open("/opt/massar/current/manifest.json", encoding="utf-8"))["releaseId"])'
)"
[[ "$release_id" =~ ^(git-[0-9a-f]{7,40}|src-[0-9a-f]{40}|prod-[0-9]{8}-[a-z0-9-]+)$ ]]
evidence_dir=/var/lib/massar/evidence/backup
install -d -m 0750 -o root -g massar "$evidence_dir"
temporary="$evidence_dir/database-latest.json.tmp"
python3 - "$temporary" "$started_at" "$completed_at" "$captured_at" \
  "$backup_type" "$backup_label" "$repository_info_sha256" "$release_id" <<'PY'
import json,sys
(
    path,started,completed,captured,backup_type,backup_label,
    repository_info_sha256,release_id,
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
        "capturedAt":captured,
        "backupLabel":backup_label,
        "backupType":backup_type,
        "stanza":"massar",
        "repository":1,
        "encrypted":True,
        "replicationFactor":3,
        "repositoryInfoSha256":repository_info_sha256,
        "walArchiveAgeSeconds":0,
    },handle,indent=2)
    handle.write("\n")
PY
chmod 0640 "$temporary"
identity_evidence="$evidence_dir/database-$backup_label.json"
test ! -e "$identity_evidence"
ln -- "$temporary" "$identity_evidence"
mv "$temporary" "$evidence_dir/database-latest.json"
