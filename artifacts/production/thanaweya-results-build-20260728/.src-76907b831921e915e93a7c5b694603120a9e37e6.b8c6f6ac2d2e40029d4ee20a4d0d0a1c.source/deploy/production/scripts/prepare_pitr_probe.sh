#!/usr/bin/env bash
set -euo pipefail

if ! curl --fail --silent http://127.0.0.1:8008/primary >/dev/null; then
  printf 'PITR probe skipped: this node is not the Patroni primary\n'
  exit 0
fi

probe_id="$(python3 -c 'import secrets; print(secrets.token_hex(16))')"
runuser -u postgres -- psql -X -v ON_ERROR_STOP=1 -d postgres \
  -c "create table if not exists public.massar_pitr_restore_probe (probe_id text primary key, created_at timestamptz not null default clock_timestamp());" \
  >/dev/null
runuser -u postgres -- psql -X -v ON_ERROR_STOP=1 -d postgres \
  -c "truncate public.massar_pitr_restore_probe;" >/dev/null
runuser -u postgres -- psql -X -v ON_ERROR_STOP=1 -d postgres \
  -c "insert into public.massar_pitr_restore_probe(probe_id) values ('$probe_id');" \
  >/dev/null
sleep 1
runuser -u postgres -- psql -X -v ON_ERROR_STOP=1 -d postgres \
  -c "insert into public.massar_pitr_restore_probe(probe_id) values ('${probe_id}-included');" \
  >/dev/null
sleep 1
target_time="$(
  runuser -u postgres -- psql -XAt -d postgres \
    -c "select clock_timestamp();"
)"
sleep 1
runuser -u postgres -- psql -X -v ON_ERROR_STOP=1 -d postgres \
  -c "insert into public.massar_pitr_restore_probe(probe_id) values ('${probe_id}-after-target');" \
  >/dev/null
runuser -u postgres -- psql -XAt -d postgres -c "select pg_switch_wal();" >/dev/null
runuser -u postgres -- pgbackrest --stanza=massar check

install -d -m 0775 -o root -g massar /srv/massar-shared/.cluster-health
temporary=/srv/massar-shared/.cluster-health/pitr-target.json.tmp
python3 - "$temporary" "$target_time" "$probe_id" <<'PY'
import json,sys
path,target,probe=sys.argv[1:]
with open(path,"w",encoding="utf-8") as handle:
    json.dump({
        "schemaVersion":1,
        "status":"ready",
        "targetTime":target,
        "probeId":probe,
    },handle,indent=2)
    handle.write("\n")
PY
chmod 0640 "$temporary"
mv "$temporary" /srv/massar-shared/.cluster-health/pitr-target.json
