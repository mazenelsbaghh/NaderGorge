#!/usr/bin/env bash
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
readonly INVENTORY="$ROOT/deploy/production/inventory/production.yml"
readonly CLUSTER="$ROOT/deploy/production/scripts/clusterctl.py"
readonly GATE="$ROOT/deploy/production/scripts/prepare_release_migration_gate.py"
readonly PLANNER="$ROOT/.agents/skills/ssh-server/scripts/release_plan.py"
readonly OPS="$ROOT/.agents/skills/ssh-server/scripts/ops.sh"
readonly EVIDENCE_ROOT="$ROOT/artifacts/production"

step() {
  printf '\n\033[1;35m[%s] %s\033[0m\n' "$(date -u +%H:%M:%S)" "$*"
}

run() {
  step "$*"
  "$@"
}

run_live() {
  local child_pid
  local elapsed=0
  step "$*"
  "$@" &
  child_pid=$!
  while kill -0 "$child_pid" 2>/dev/null; do
    sleep 1
    elapsed=$((elapsed + 1))
    if (( elapsed % 15 == 0 )) && kill -0 "$child_pid" 2>/dev/null; then
      printf '[%s] still running (%ss): %s\n' \
        "$(date -u +%H:%M:%S)" "$elapsed" "$*"
    fi
  done
  wait "$child_pid"
}

usage() {
  cat <<'EOF'
Usage:
  deploy.sh plan [--base=REF]
  deploy.sh release-id
  deploy.sh build --release=ID|auto [--yes]
  deploy.sh gate --release=ID --manifest=PATH [--output=PATH] [--yes]
  deploy.sh release --release=ID --manifest=PATH --backup-evidence=PATH [--yes]
  deploy.sh fast-release --release=ID --manifest=PATH --backup-evidence=PATH \
    --reason=TEXT --yes
  deploy.sh small-release --component=frontend|backend|worker|all \
    --reason=TEXT [--base=REF] [--yes]

Behavior:
  - plan shows the affected backend/frontend/worker/database areas.
  - build uses node-3 and always creates all four immutable Production images.
  - release-id prints the exact immutable ID for the current source state.
  - small-release computes the ID and evidence paths automatically, then runs
    check, build, migration gate, and zero-downtime rolling release in order.
  - gate creates the encrypted-backup, isolated-restore, and N-1 evidence.
  - release always runs status plus migrate/deploy dry-runs first.
  - without --yes every mutating command remains a preview.
  - fast-release skips unrelated local checks only. It never skips health,
    DB migration coverage, backup/restore evidence, dry-run, rolling rollout,
    or automatic application rollback.
EOF
}

command_name="${1:-help}"
shift || true
release=""
manifest=""
backup_evidence=""
gate_output=""
base="${MASSAR_BASE_REF:-AUTO}"
reason=""
component=""
confirmed=false

for argument in "$@"; do
  case "$argument" in
    --release=*) release="${argument#*=}" ;;
    --manifest=*) manifest="${argument#*=}" ;;
    --backup-evidence=*) backup_evidence="${argument#*=}" ;;
    --output=*) gate_output="${argument#*=}" ;;
    --base=*) base="${argument#*=}" ;;
    --reason=*) reason="${argument#*=}" ;;
    --component=*) component="${argument#*=}" ;;
    --yes) confirmed=true ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown argument: $argument" >&2; usage >&2; exit 2 ;;
  esac
done

require_operator_environment() {
  export MASSAR_KNOWN_HOSTS_FILE="${MASSAR_KNOWN_HOSTS_FILE:-/Users/mazenelsbagh/.ssh/massar_prod_known_hosts}"
  export MASSAR_SSH_IDENTITY_FILE="${MASSAR_SSH_IDENTITY_FILE:-/Users/mazenelsbagh/.ssh/massar_prod_cluster_ed25519}"
  [[ -f "$MASSAR_KNOWN_HOSTS_FILE" ]] || {
    echo "Known-hosts file is missing: $MASSAR_KNOWN_HOSTS_FILE" >&2
    exit 2
  }
  [[ -f "$MASSAR_SSH_IDENTITY_FILE" ]] || {
    echo "SSH identity file is missing: $MASSAR_SSH_IDENTITY_FILE" >&2
    exit 2
  }
  local mode
  mode="$(stat -f '%Lp' "$MASSAR_SSH_IDENTITY_FILE" 2>/dev/null || stat -c '%a' "$MASSAR_SSH_IDENTITY_FILE")"
  [[ "$mode" == "600" ]] || {
    echo "SSH identity must be mode 0600." >&2
    exit 2
  }
}

require_release() {
  [[ -n "$release" ]] || { echo "--release is required" >&2; exit 2; }
}

exact_release_id() {
  python3 - "$ROOT" <<'PY'
import sys
from pathlib import Path

root = Path(sys.argv[1])
sys.path.insert(0, str(root / "deploy/production/scripts"))
from release_images import source_state

print(source_state(root)["releaseId"])
PY
}

normalize_release() {
  if [[ -z "$release" || "$release" == "auto" ]]; then
    release="$(exact_release_id)"
  fi
}

require_release_evidence() {
  require_release
  [[ -f "$manifest" ]] || { echo "--manifest must be an existing file" >&2; exit 2; }
  [[ -f "$backup_evidence" ]] || {
    echo "--backup-evidence must be an existing file" >&2
    exit 2
  }
}

cluster() {
  run_live python3 "$CLUSTER" --inventory "$INVENTORY" "$@"
}

case "$command_name" in
  plan)
    run python3 "$PLANNER" plan --base "$base"
    ;;
  release-id)
    exact_release_id
    ;;
  build)
    require_operator_environment
    normalize_release
    require_release
    cluster build --node all --release "$release" --remote-builder \
      --dry-run --evidence-dir "$EVIDENCE_ROOT/build"
    if [[ "$confirmed" == "true" ]]; then
      cluster build --node all --release "$release" --remote-builder \
        --yes --evidence-dir "$EVIDENCE_ROOT/build"
    else
      step "Preview complete. Re-run with --yes to build and distribute."
    fi
    ;;
  small-release)
    require_operator_environment
    case "$component" in
      frontend|backend|worker|all) ;;
      *)
        echo "--component must be frontend, backend, worker, or all" >&2
        exit 2
        ;;
    esac
    [[ ${#reason} -ge 12 ]] || {
      echo "small-release requires --reason with at least 12 characters" >&2
      exit 2
    }
    release="$(exact_release_id)"
    manifest="$EVIDENCE_ROOT/build/$release/manifest.json"
    backup_evidence="$EVIDENCE_ROOT/migration-gates/$release.json"
    step "SMALL RELEASE PLAN"
    printf '  Component intent: %s\n  Release:          %s\n  Manifest:         %s\n  Gate evidence:    %s\n  Reason:           %s\n' \
      "$component" "$release" "$manifest" "$backup_evidence" "$reason"
    step "Production keeps one four-image immutable manifest; remote cache avoids unchanged rebuild work."
    run python3 "$PLANNER" validate-scope --base "$base" --scope "$component"
    run bash "$OPS" check --base="$base"
    if [[ "$confirmed" != "true" ]]; then
      "$0" build --release="$release"
      step "Preview complete. Review the plan, then re-run with --yes."
      exit 0
    fi
    "$0" build --release="$release" --yes
    [[ -f "$manifest" ]] || {
      echo "Build completed without the expected manifest: $manifest" >&2
      exit 2
    }
    "$0" gate --release="$release" --manifest="$manifest" \
      --output="$backup_evidence" --yes
    [[ -f "$backup_evidence" ]] || {
      echo "Migration gate completed without evidence: $backup_evidence" >&2
      exit 2
    }
    "$0" release --release="$release" --manifest="$manifest" \
      --backup-evidence="$backup_evidence" --base="$base" --yes
    ;;
  gate)
    require_operator_environment
    require_release
    [[ -f "$manifest" ]] || { echo "--manifest must be an existing file" >&2; exit 2; }
    gate_output="${gate_output:-$EVIDENCE_ROOT/migration-gates/$release.json}"
    run_live python3 "$GATE" \
      --inventory "$INVENTORY" \
      --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
      --identity "$MASSAR_SSH_IDENTITY_FILE" \
      --release "$release" \
      --manifest "$manifest" \
      --output "$gate_output" \
      --dry-run
    if [[ "$confirmed" == "true" ]]; then
      run_live python3 "$GATE" \
        --inventory "$INVENTORY" \
        --known-hosts "$MASSAR_KNOWN_HOSTS_FILE" \
        --identity "$MASSAR_SSH_IDENTITY_FILE" \
        --release "$release" \
        --manifest "$manifest" \
        --output "$gate_output" \
        --yes
    else
      step "Preview complete. Re-run with --yes to create migration evidence."
    fi
    ;;
  release|fast-release)
    require_operator_environment
    require_release_evidence
    run bash "$OPS" check --base="$base"
    if [[ "$command_name" == "fast-release" ]]; then
      [[ "$confirmed" == "true" ]] || {
        echo "fast-release requires --yes" >&2
        exit 2
      }
      [[ ${#reason} -ge 12 ]] || {
        echo "fast-release requires --reason with at least 12 characters" >&2
        exit 2
      }
      step "FAST RELEASE: $reason"
      step "Safety gates remain enabled; only unrelated local checks are omitted."
    fi
    cluster status --node all --evidence-dir "$EVIDENCE_ROOT/status"
    cluster migrate --node all --release "$release" \
      --manifest "$manifest" --backup-evidence "$backup_evidence" --dry-run
    cluster deploy --node all --release "$release" \
      --manifest "$manifest" --backup-evidence "$backup_evidence" --dry-run
    if [[ "$confirmed" == "true" ]]; then
      cluster migrate --node all --release "$release" \
        --manifest "$manifest" --backup-evidence "$backup_evidence" --yes
      cluster deploy --node all --release "$release" \
        --manifest "$manifest" --backup-evidence "$backup_evidence" --yes
      cluster status --node all --evidence-dir "$EVIDENCE_ROOT/status"
    else
      step "Preview complete. Re-run with --yes for the rolling release."
    fi
    ;;
  help|-h|--help)
    usage
    ;;
  *)
    usage >&2
    exit 2
    ;;
esac
