#!/usr/bin/env bash
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
readonly INVENTORY="$ROOT/deploy/production/inventory/production.yml"
readonly AUDIT="$ROOT/deploy/production/scripts/audit_production_catalog.py"
readonly COMPARE="$ROOT/.agents/skills/ssh-server/scripts/schema_inventory.py"
readonly REPAIR_PLAN="$ROOT/.agents/skills/ssh-server/scripts/database_repair_plan.py"
readonly COLLECT="$ROOT/deploy/production/scripts/collect_current_release_manifest.py"
readonly GATE="$ROOT/deploy/production/scripts/prepare_release_migration_gate.py"
readonly MIGRATE="$ROOT/deploy/production/scripts/migrate_release.py"
readonly EVIDENCE_ROOT="$ROOT/artifacts/production/schema-inventory"

step() { printf '\n\033[1;33m[%s] %s\033[0m\n' "$(date -u +%H:%M:%S)" "$*"; }
run() { step "$*"; "$@"; }
run_live() {
  local child_pid elapsed=0
  step "$*"
  "$@" &
  child_pid=$!
  while kill -0 "$child_pid" 2>/dev/null; do
    sleep 1
    elapsed=$((elapsed + 1))
    if (( elapsed % 15 == 0 )) && kill -0 "$child_pid" 2>/dev/null; then
      printf '[%s] still running (%ss)\n' "$(date -u +%H:%M:%S)" "$elapsed"
    fi
  done
  wait "$child_pid"
}

usage() {
  cat <<'EOF'
Usage:
  database.sh inventory [--require-match]
  database.sh fast --reason=TEXT [--base=REF] [--yes]

inventory compares EF snapshot/migrations with the live read-only catalog.
fast repairs drift using the migrator image already shipped with the current
Production release. It never builds or restarts application images. New
migrations not present in that immutable release fail closed.
EOF
}

command_name="${1:-help}"
shift || true
if [[ "$command_name" == "help" || "$command_name" == "-h" || "$command_name" == "--help" ]]; then
  usage
  exit 0
fi
confirmed=false
require_match=false
reason=""
base="${MASSAR_BASE_REF:-AUTO}"
for argument in "$@"; do
  case "$argument" in
    --yes) confirmed=true ;;
    --require-match) require_match=true ;;
    --reason=*) reason="${argument#*=}" ;;
    --base=*) base="${argument#*=}" ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $argument" >&2; usage >&2; exit 2 ;;
  esac
done

export MASSAR_KNOWN_HOSTS_FILE="${MASSAR_KNOWN_HOSTS_FILE:-/Users/mazenelsbagh/.ssh/massar_prod_known_hosts}"
export MASSAR_SSH_IDENTITY_FILE="${MASSAR_SSH_IDENTITY_FILE:-/Users/mazenelsbagh/.ssh/massar_prod_cluster_ed25519}"
[[ -f "$MASSAR_KNOWN_HOSTS_FILE" && -f "$MASSAR_SSH_IDENTITY_FILE" ]] || {
  echo "Strict Production SSH files are missing." >&2
  exit 2
}
mode="$(stat -f '%Lp' "$MASSAR_SSH_IDENTITY_FILE" 2>/dev/null || stat -c '%a' "$MASSAR_SSH_IDENTITY_FILE")"
[[ "$mode" == 600 ]] || { echo "SSH identity must be mode 0600." >&2; exit 2; }

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
actual="$EVIDENCE_ROOT/$timestamp-actual.json"
comparison="$EVIDENCE_ROOT/$timestamp-comparison.json"

capture_inventory() {
  run_live python3 "$AUDIT" --inventory "$INVENTORY" \
    --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
    --identity "$MASSAR_SSH_IDENTITY_FILE" --output "$actual"
  compare_args=(--actual "$actual" --output "$comparison")
  [[ "$require_match" == true ]] && compare_args+=(--require-match)
  run python3 "$COMPARE" "${compare_args[@]}"
}

case "$command_name" in
  inventory)
    capture_inventory
    ;;
  fast)
    [[ ${#reason} -ge 12 ]] || { echo "--reason needs at least 12 characters." >&2; exit 2; }
    run python3 "$ROOT/deploy/production/scripts/clusterctl.py" \
      --inventory "$INVENTORY" status --node all \
      --evidence-dir "$ROOT/artifacts/production/status"
    capture_inventory
    current_manifest="$EVIDENCE_ROOT/$timestamp-current-manifest.json"
    collector_evidence="$EVIDENCE_ROOT/$timestamp-current-manifest-evidence.json"
    repair_plan="$EVIDENCE_ROOT/$timestamp-repair-plan.json"
    run_live python3 "$COLLECT" \
      --inventory "$INVENTORY" \
      --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
      --identity "$MASSAR_SSH_IDENTITY_FILE" \
      --manifest-output "$current_manifest" \
      --evidence-output "$collector_evidence"
    run python3 "$REPAIR_PLAN" --comparison "$comparison" \
      --manifest "$current_manifest" --output "$repair_plan" --reason "$reason"
    release="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["releaseId"])' "$repair_plan")"
    gate="$EVIDENCE_ROOT/$timestamp-migration-gate.json"
    gate_args=(
      --inventory "$INVENTORY"
      --known-hosts "$MASSAR_KNOWN_HOSTS_FILE"
      --identity "$MASSAR_SSH_IDENTITY_FILE"
      --release "$release" --manifest "$current_manifest" --output "$gate"
    )
    run_live python3 "$GATE" "${gate_args[@]}" --dry-run
    if [[ "$confirmed" == true ]]; then
      run_live python3 "$GATE" "${gate_args[@]}" --yes
      run_live python3 "$MIGRATE" \
        --inventory "$INVENTORY" \
        --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
        --identity "$MASSAR_SSH_IDENTITY_FILE" \
        --release "$release" --manifest "$current_manifest" \
        --backup-evidence "$gate" --dry-run
      run_live python3 "$MIGRATE" \
        --inventory "$INVENTORY" \
        --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
        --identity "$MASSAR_SSH_IDENTITY_FILE" \
        --release "$release" --manifest "$current_manifest" \
        --backup-evidence "$gate" --yes
      require_match=true
      timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
      actual="$EVIDENCE_ROOT/$timestamp-actual.json"
      comparison="$EVIDENCE_ROOT/$timestamp-comparison.json"
      capture_inventory
    else
      step "Preview complete. Re-run with --yes to repair only PostgreSQL."
    fi
    ;;
  help|-h|--help) usage ;;
  *) usage >&2; exit 2 ;;
esac
