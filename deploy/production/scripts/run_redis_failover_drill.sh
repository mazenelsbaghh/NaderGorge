#!/usr/bin/env bash
set -euo pipefail

readonly INVENTORY="${MASSAR_INVENTORY:?MASSAR_INVENTORY is required}"
readonly SSH_KEY="${MASSAR_SSH_KEY:?MASSAR_SSH_KEY is required}"
readonly KNOWN_HOSTS="${MASSAR_KNOWN_HOSTS:?MASSAR_KNOWN_HOSTS is required}"
readonly SSH_USER="${MASSAR_SSH_USER:-massar-ops}"
readonly SSH_OPTIONS=(
  -F /dev/null -i "$SSH_KEY"
  -o "UserKnownHostsFile=$KNOWN_HOSTS"
  -o StrictHostKeyChecking=yes
  -o BatchMode=yes
  -o IdentitiesOnly=yes
)

NODE_ROWS_FILE="$(mktemp "${TMPDIR:-/tmp}/massar-redis-nodes.XXXXXX")"
cleanup_node_rows() {
  rm -f "$NODE_ROWS_FILE"
}
trap cleanup_node_rows EXIT
python3 - "$INVENTORY" >"$NODE_ROWS_FILE" <<'PY'
import json,sys
value=json.load(open(sys.argv[1], encoding="utf-8"))
assert value["cluster"]["name"] == "massar-production"
assert [node["id"] for node in value["nodes"]] == ["node-1", "node-2", "node-3"]
for node in value["nodes"]:
    print(f'{node["id"]}|{node["public_address"]}|{node["overlay_address"]}')
PY
NODE_ROWS=()
while IFS= read -r row; do
  [[ -n "$row" ]] || { printf 'Invalid empty inventory node row\n' >&2; exit 9; }
  NODE_ROWS[${#NODE_ROWS[@]}]="$row"
done <"$NODE_ROWS_FILE"
rm -f "$NODE_ROWS_FILE"
trap - EXIT
[[ "${#NODE_ROWS[@]}" -eq 3 ]] || { printf 'Inventory did not yield three nodes\n' >&2; exit 9; }
for index in 0 1 2; do
  row="${NODE_ROWS[$index]}"
  IFS='|' read -r node_id public_address overlay_address <<<"$row"
  [[ "$node_id" == "node-$((index + 1))" ]] || { printf 'Inventory node order is invalid\n' >&2; exit 9; }
  case "$node_id" in
    node-1) NODE_1_PUBLIC="$public_address"; NODE_1_OVERLAY="$overlay_address" ;;
    node-2) NODE_2_PUBLIC="$public_address"; NODE_2_OVERLAY="$overlay_address" ;;
    node-3) NODE_3_PUBLIC="$public_address"; NODE_3_OVERLAY="$overlay_address" ;;
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
node_for_overlay() {
  case "$1" in
    "$NODE_1_OVERLAY") printf '%s\n' node-1 ;;
    "$NODE_2_OVERLAY") printf '%s\n' node-2 ;;
    "$NODE_3_OVERLAY") printf '%s\n' node-3 ;;
    *) return 1 ;;
  esac
}
readonly CONTROL_NODE="node-1"
readonly DRILL_LOCK="/srv/massar-shared/.cluster-health/failover-drill.lock"
operation_id="redis-$(date -u +%Y%m%dT%H%M%SZ)-$$"
lock_acquired=false
master_stopped=false

remote() {
  local node_id="$1"
  shift
  local remote_command=""
  local argument quoted
  for argument in "$@"; do
    printf -v quoted '%q' "$argument"
    if [[ -n "$remote_command" ]]; then
      remote_command+=" "
    fi
    remote_command+="$quoted"
  done
  [[ -n "$remote_command" ]] || { printf 'remote command is required\n' >&2; return 2; }
  ssh "${SSH_OPTIONS[@]}" "$SSH_USER@$(public_for_node "$node_id")" "$remote_command"
}

redis_command() {
  remote "$CONTROL_NODE" sudo docker run --rm --network host \
    --env-file /etc/massar/app.env redis:7-alpine \
    sh -ec 'REDISCLI_AUTH="$REDIS_PASSWORD" redis-cli --no-auth-warning "$@"' sh "$@"
}

remote "$CONTROL_NODE" bash -lc \
  "set -euo pipefail; mkdir '$DRILL_LOCK'; printf '%s\n' '$operation_id' > '$DRILL_LOCK/owner'"
lock_acquired=true
cleanup() {
  status=$?
  if [[ "$master_stopped" == true && -n "${master_node:-}" ]]; then
    remote "$master_node" sudo systemctl start redis-server >/dev/null 2>&1 || true
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
  remote "$node_id" systemctl is-active --quiet redis-server redis-sentinel
done

sentinel_master=""
for sentinel_ip in 10.77.0.11 10.77.0.12 10.77.0.13; do
  observed="$(
    redis_command -h "$sentinel_ip" -p 26379 \
      SENTINEL get-master-addr-by-name massar-redis | head -n 1
  )"
  [[ -n "$observed" ]]
  if [[ -z "$sentinel_master" ]]; then
    sentinel_master="$observed"
  else
    [[ "$observed" == "$sentinel_master" ]] || {
      printf 'Sentinels disagree about the current master\n' >&2
      exit 9
    }
  fi
  redis_command -h "$sentinel_ip" -p 26379 \
    SENTINEL ckquorum massar-redis | grep -q '^OK'
done

master_ip="$(
  redis_command -h 10.77.0.11 -p 26379 SENTINEL get-master-addr-by-name massar-redis |
    head -n 1
)"
master_node="$(node_for_overlay "$master_ip" || true)"
[[ -n "$master_node" ]]
[[ "$master_ip" == "$sentinel_master" ]]
role_summary=""
masters=0
replicas=0
for redis_ip in 10.77.0.11 10.77.0.12 10.77.0.13; do
  role_summary="$(redis_command -h "$redis_ip" -p 6379 ROLE | head -n 1)"
  if [[ "$role_summary" == master ]]; then
    masters=$((masters + 1))
  elif [[ "$role_summary" == slave || "$role_summary" == replica ]]; then
    replicas=$((replicas + 1))
  fi
done
[[ "$masters" -eq 1 && "$replicas" -eq 2 ]] || {
  printf 'Redis pre-state is not exactly one master plus two replicas\n' >&2
  exit 9
}
redis_command -h "$master_ip" -p 6379 INFO replication |
  tr -d '\r' | grep -qx 'connected_slaves:2'
printf 'master-before=%s\n' "$master_node"
probe_key="massar:failover:166"
redis_command -h "$master_ip" -p 6379 SET "$probe_key" preserved EX 600 >/dev/null

remote "$master_node" sudo systemctl stop redis-server
master_stopped=true

start_epoch="$(date +%s)"
new_master=""
for _attempt in $(seq 1 30); do
  new_master="$(
    redis_command -h 10.77.0.11 -p 26379 SENTINEL get-master-addr-by-name massar-redis |
      head -n 1 || true
  )"
  [[ -n "$new_master" && "$new_master" != "$master_ip" ]] && break
  sleep 2
done
[[ -n "$new_master" && "$new_master" != "$master_ip" ]]
printf 'master-after=%s failover-seconds=%s\n' \
  "$(node_for_overlay "$new_master")" "$(( $(date +%s) - start_epoch ))"
[[ "$(redis_command -h "$new_master" -p 6379 GET "$probe_key")" == "preserved" ]]

remote "$master_node" sudo systemctl start redis-server
master_stopped=false
for _attempt in $(seq 1 30); do
  role="$(redis_command -h "$master_ip" -p 6379 ROLE | head -n 1 || true)"
  [[ "$role" == "slave" || "$role" == "replica" ]] && break
  sleep 2
done
[[ "$role" == "slave" || "$role" == "replica" ]]
redis_command -h "$new_master" -p 6379 INFO replication |
  tr -d '\r' | grep -qx 'connected_slaves:2'
redis_command -h "$new_master" -p 6379 DEL "$probe_key" >/dev/null
trap - EXIT HUP INT TERM
cleanup
