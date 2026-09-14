#!/usr/bin/env bash
# Safe day-to-day Massar production helper.  It deliberately exposes only
# read-only commands and dry-run drills; real mutations stay in clusterctl.
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
readonly CLUSTER=(python3 "$ROOT/deploy/production/scripts/clusterctl.py" --inventory "$ROOT/deploy/production/inventory/production.yml")
readonly EVIDENCE_ROOT="$ROOT/artifacts/production"

require_environment() {
  : "${MASSAR_KNOWN_HOSTS_FILE:?Set MASSAR_KNOWN_HOSTS_FILE first}"
  : "${MASSAR_SSH_IDENTITY_FILE:?Set MASSAR_SSH_IDENTITY_FILE first}"
  [[ -f "$MASSAR_KNOWN_HOSTS_FILE" ]] || { echo "known-hosts file missing" >&2; exit 2; }
  [[ -f "$MASSAR_SSH_IDENTITY_FILE" ]] || { echo "SSH identity file missing" >&2; exit 2; }
  [[ "$(stat -f '%Lp' "$MASSAR_SSH_IDENTITY_FILE" 2>/dev/null || stat -c '%a' "$MASSAR_SSH_IDENTITY_FILE")" == "600" ]] || {
    echo "SSH identity must be mode 0600" >&2; exit 2;
  }
}

timestamp() { date -u +%Y%m%dT%H%M%SZ; }
evidence_dir() { printf '%s/%s-%s' "$EVIDENCE_ROOT" "$1" "$(timestamp)"; }

usage() {
  cat <<'EOF'
Usage: .agents/skills/ssh-server/scripts/massar.sh <command> [arguments]

Read-only commands:
  status                 Cluster health/quorum/release evidence for all nodes
  audit                  Host, data, disk, time, and service audit for all nodes
  backups                Backup schedule health for all nodes
  cloudflare             Tunnel/connector health for all nodes
  logs <node> <service> [minutes]
                         Safe redacted recent logs. node: node-1|node-2|node-3
                         service: backend|gateway|worker|student|admin|teacher|staff|landing

Safe previews only (no state change):
  failover-dry           Preview bounded PostgreSQL/Redis failover drill
  files-dry <node>       Preview Gluster file-failover drill for node-1 or node-2
  restore-dry            Preview isolated database restore on node-3

Mutating operations such as build, migrate, deploy, restore, and failover are
intentionally not available here. Use clusterctl directly with --dry-run then
--yes after reviewing the Production skill.
EOF
}

cluster_readonly() {
  local command="$1"
  "${CLUSTER[@]}" "$command" --node all --evidence-dir "$(evidence_dir "$command")"
}

safe_logs() {
  local node="$1" service="$2" minutes="${3:-15}"
  [[ "$node" =~ ^node-[123]$ ]] || { echo "invalid node" >&2; exit 2; }
  [[ "$service" =~ ^(backend|gateway|worker|student|admin|teacher|staff|landing)$ ]] || { echo "invalid service" >&2; exit 2; }
  [[ "$minutes" =~ ^[0-9]+$ ]] && (( minutes >= 1 && minutes <= 120 )) || { echo "minutes must be 1..120" >&2; exit 2; }
  python3 - "$ROOT" "$node" "$service" "$minutes" <<'PY'
import re, sys
from pathlib import Path
root, node_id, service, minutes = map(str, sys.argv[1:])
sys.path.insert(0, str(Path(root) / "deploy/production/scripts"))
from clusterctl import load_inventory
from ssh_transport import StrictSshTransport, SshTarget
inventory = load_inventory(Path(root) / "deploy/production/inventory/production.yml", require_operator_files=True)
node = next(item for item in inventory.nodes if item.id == node_id)
transport = StrictSshTransport(Path(inventory.cluster["known_hosts_file"]), Path(inventory.cluster["identity_file"]))
container = f"massar_production-{service}-1"
command = f"sudo -n /usr/bin/docker logs --since {minutes}m --tail 250 {container} 2>&1"
result = transport.run(SshTarget(node.id, node.public_address, inventory.cluster["ssh_user"]), ("bash", "-lc", command), timeout_seconds=30, check=False)
redacted = re.sub(r"(?i)(authorization|cookie|token|password|phone)[=:][^\s,;]+", r"\1=[REDACTED]", result.stdout + result.stderr)
print(redacted)
raise SystemExit(result.returncode)
PY
}

case "${1:-help}" in
  help|-h|--help) usage ;;
  *) require_environment ;;
esac

case "${1:-help}" in
  status) cluster_readonly status ;;
  audit) cluster_readonly audit ;;
  backups) cluster_readonly backup-schedules-status ;;
  cloudflare) cluster_readonly cloudflare-status ;;
  logs) [[ $# -ge 3 ]] || { usage >&2; exit 2; }; safe_logs "$2" "$3" "${4:-15}" ;;
  failover-dry) "${CLUSTER[@]}" failover-test --node all --dry-run --evidence-dir "$(evidence_dir failover-dry)" ;;
  files-dry) [[ "${2:-}" =~ ^node-[12]$ ]] || { echo "files-dry requires node-1 or node-2" >&2; exit 2; }; "${CLUSTER[@]}" file-failover-test --node "$2" --maximum-outage-seconds 30 --dry-run --evidence-dir "$(evidence_dir files-dry)" ;;
  restore-dry) "${CLUSTER[@]}" restore-test --node node-3 --dry-run --evidence-dir "$(evidence_dir restore-dry)" ;;
  help|-h|--help) ;;
  *) usage >&2; exit 2 ;;
esac
