#!/usr/bin/env bash
set -euo pipefail

readonly INVENTORY="${MASSAR_INVENTORY:?MASSAR_INVENTORY is required}"
readonly SSH_KEY="${MASSAR_SSH_KEY:?MASSAR_SSH_KEY is required}"
readonly KNOWN_HOSTS="${MASSAR_KNOWN_HOSTS:?MASSAR_KNOWN_HOSTS is required}"
readonly SSH_USER="${MASSAR_SSH_USER:-massar-ops}"
readonly SSH_OPTIONS=(
  -F /dev/null
  -i "$SSH_KEY"
  -o "UserKnownHostsFile=$KNOWN_HOSTS"
  -o StrictHostKeyChecking=yes
  -o BatchMode=yes
  -o IdentitiesOnly=yes
)

NODE_ROWS=()
while IFS= read -r node_row; do
  [[ -n "$node_row" ]] || { printf 'Invalid empty inventory node row\n' >&2; exit 9; }
  NODE_ROWS[${#NODE_ROWS[@]}]="$node_row"
done < <(
  python3 - "$INVENTORY" <<'PY'
import json,sys
value=json.load(open(sys.argv[1], encoding="utf-8"))
assert value["cluster"]["name"] == "massar-production"
assert [node["id"] for node in value["nodes"]] == ["node-1", "node-2", "node-3"]
for node in value["nodes"]:
    print(f'{node["id"]}|{node["public_address"]}|{node["overlay_address"]}')
PY
)
[[ "${#NODE_ROWS[@]}" -eq 3 ]]

for row in "${NODE_ROWS[@]}"; do
  IFS='|' read -r node_id public_address overlay_address <<<"$row"
  case "$node_id" in
    node-1) NODE_1_PUBLIC="$public_address"; NODE_1_OVERLAY="$overlay_address" ;;
    node-2) NODE_2_PUBLIC="$public_address"; NODE_2_OVERLAY="$overlay_address" ;;
    node-3) NODE_3_PUBLIC="$public_address"; NODE_3_OVERLAY="$overlay_address" ;;
    *) printf 'Unexpected inventory node: %s\n' "$node_id" >&2; exit 9 ;;
  esac
done
public_for_node() {
  case "$1" in
    node-1) printf '%s\n' "$NODE_1_PUBLIC" ;;
    node-2) printf '%s\n' "$NODE_2_PUBLIC" ;;
    node-3) printf '%s\n' "$NODE_3_PUBLIC" ;;
    *) return 1 ;;
  esac
}
overlay_for_node() {
  case "$1" in
    node-1) printf '%s\n' "$NODE_1_OVERLAY" ;;
    node-2) printf '%s\n' "$NODE_2_OVERLAY" ;;
    node-3) printf '%s\n' "$NODE_3_OVERLAY" ;;
    *) return 1 ;;
  esac
}
readonly CONTROL_NODE="node-1"
readonly DRILL_LOCK="/srv/massar-shared/.cluster-health/failover-drill.lock"
operation_id="postgres-$(date -u +%Y%m%dT%H%M%SZ)-$$"
lock_acquired=false
leader_stopped=false

remote() {
  local node_id="$1"
  shift
  local remote_command=""
  local argument quoted
  for argument in "$@"; do
    printf -v quoted '%q' "$argument"
    [[ -z "$remote_command" ]] || remote_command+=" "
    remote_command+="$quoted"
  done
  [[ -n "$remote_command" ]] || { printf 'remote command is required\n' >&2; return 2; }
  ssh "${SSH_OPTIONS[@]}" "$SSH_USER@$(public_for_node "$node_id")" "$remote_command"
}

database_query() {
  local sql="$1"
  local encoded
  encoded="$(printf '%s' "$sql" | base64 | tr -d '\n')"
  remote "$CONTROL_NODE" sudo docker run --rm --network host \
    -v /etc/massar/secrets/postgres-superuser-password:/run/secrets/pgsuper:ro \
    postgres:16-alpine \
    sh -ec 'query="$(printf %s "$1" | base64 -d)"; export PGPASSWORD="$(cat /run/secrets/pgsuper)"; psql -h 127.0.0.1 -p 6432 -U postgres -d massar_platform -v ON_ERROR_STOP=1 -XAt -c "$query"' sh "$encoded"
}

remote "$CONTROL_NODE" bash -lc \
  "set -euo pipefail; mkdir '$DRILL_LOCK'; printf '%s\n' '$operation_id' > '$DRILL_LOCK/owner'"
lock_acquired=true
cleanup() {
  status=$?
  if [[ "$leader_stopped" == true && -n "${leader:-}" ]]; then
    remote "$leader" sudo systemctl start patroni >/dev/null 2>&1 || true
  fi
  if [[ "$lock_acquired" == true ]]; then
    remote "$CONTROL_NODE" bash -lc \
      "test \"\$(cat '$DRILL_LOCK/owner' 2>/dev/null)\" = '$operation_id' && rm -f '$DRILL_LOCK/owner' && rmdir '$DRILL_LOCK'" \
      >/dev/null 2>&1 || true
  fi
  exit "$status"
}
trap cleanup EXIT HUP INT TERM

for node_id in node-1 node-2 node-3; do
  remote "$node_id" systemctl is-active --quiet etcd patroni
done

leader=""
running_members=0
replica_members=0
for candidate in node-1 node-2 node-3; do
  member="$(
    remote "$CONTROL_NODE" curl --fail --silent \
      "http://$(overlay_for_node "$candidate"):8008/patroni" |
      python3 -c 'import json,sys; value=json.load(sys.stdin); print("{}|{}".format(value.get("state",""),value.get("role","")))'
  )"
  [[ "$member" == running\|* ]] && running_members=$((running_members + 1))
  [[ "$member" == "running|replica" ]] && replica_members=$((replica_members + 1))
  if remote "$CONTROL_NODE" curl --fail --silent \
    "http://$(overlay_for_node "$candidate"):8008/primary" >/dev/null; then
    [[ -z "$leader" ]] || {
      printf 'more than one Patroni primary observed\n' >&2
      exit 9
    }
    leader="$candidate"
  fi
done
[[ -n "$leader" ]]
[[ "$running_members" -eq 3 && "$replica_members" -eq 2 ]] || {
  printf 'Patroni pre-state is not one running leader plus two running replicas\n' >&2
  exit 9
}
replication_prestate="$(
  database_query \
    "select count(*)::text || '|' || (count(*) filter (where state='streaming' and coalesce(pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn),0) <= 1048576))::text from pg_stat_replication;"
)"
[[ "$replication_prestate" == "2|2" ]] || {
  printf 'PostgreSQL pre-state does not have two streaming replicas within 1 MiB\n' >&2
  exit 9
}
printf 'leader-before=%s\n' "$leader"

database_query \
  "CREATE TABLE IF NOT EXISTS cluster_failover_probe (id uuid PRIMARY KEY, payload text NOT NULL, created_at timestamptz NOT NULL DEFAULT now()); INSERT INTO cluster_failover_probe (id,payload) VALUES ('16600000-0000-0000-0000-000000000001','acknowledged-before-failover') ON CONFLICT (id) DO UPDATE SET payload=EXCLUDED.payload;" \
  >/dev/null

start_epoch="$(date +%s)"
remote "$leader" sudo systemctl stop patroni
leader_stopped=true

new_writer=""
for _attempt in $(seq 1 30); do
  new_writer="$(database_query "select inet_server_addr()::text where not pg_is_in_recovery();" 2>/dev/null || true)"
  [[ -n "$new_writer" ]] && break
  sleep 2
done
elapsed="$(( $(date +%s) - start_epoch ))"
[[ -n "$new_writer" ]] || { printf 'no safe writer elected within 60 seconds\n' >&2; exit 8; }
printf 'new-writer=%s failover-seconds=%s\n' "$new_writer" "$elapsed"

probe="$(database_query "select payload from cluster_failover_probe where id='16600000-0000-0000-0000-000000000001';")"
[[ "$probe" == "acknowledged-before-failover" ]]
printf 'acknowledged-probe=preserved\n'

remote "$leader" sudo systemctl start patroni
leader_stopped=false
old_state=""
for _attempt in $(seq 1 30); do
  old_state="$(
    remote "$CONTROL_NODE" curl --fail --silent "http://$(overlay_for_node "$leader"):8008/patroni" 2>/dev/null |
      python3 -c 'import json,sys; row=json.load(sys.stdin); print("{}:{}".format(row.get("state","missing"), row.get("role","missing")))' \
      2>/dev/null || true
  )"
  [[ "$old_state" == "running:replica" ]] && break
  sleep 2
done
printf 'former-leader-state=%s\n' "$old_state"
[[ "$old_state" == "running:replica" ]]
database_query "DROP TABLE cluster_failover_probe;" >/dev/null
trap - EXIT HUP INT TERM
cleanup
