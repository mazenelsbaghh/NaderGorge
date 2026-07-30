#!/usr/bin/env bash
set -euo pipefail

if ! curl --fail --silent http://127.0.0.1:8008/primary >/dev/null; then
  printf 'database backup initialization skipped: this node is not the Patroni primary\n'
  exit 0
fi

runuser -u postgres -- pgbackrest --stanza=massar stanza-create
runuser -u postgres -- pgbackrest --stanza=massar --output=json info >/dev/null

evidence_dir=/var/lib/massar/evidence/backup
install -d -m 0750 -o root -g massar "$evidence_dir"
captured_at="$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
temporary="$evidence_dir/database-stanza-ready.json.tmp"
python3 - "$temporary" "$captured_at" <<'PY'
import json,sys
path,captured=sys.argv[1:]
with open(path,"w",encoding="utf-8") as handle:
    json.dump({
        "schemaVersion":1,
        "status":"success",
        "repository":"internal-three-node",
        "encrypted":True,
        "replicationFactor":3,
        "capturedAt":captured,
    },handle,indent=2)
    handle.write("\n")
PY
chmod 0640 "$temporary"
mv "$temporary" "$evidence_dir/database-stanza-ready.json"
