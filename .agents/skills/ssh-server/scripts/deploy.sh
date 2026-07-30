#!/usr/bin/env bash
set -euo pipefail

repo_root="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
inventory="$repo_root/deploy/production/inventory/production.yml"
clusterctl="$repo_root/deploy/production/scripts/clusterctl.py"

release=""
dry_run=false
for arg in "$@"; do
  case "$arg" in
    --dry-run) dry_run=true ;;
    --release=*) release="${arg#*=}" ;;
    *)
      echo "usage: deploy.sh --release=git-SHA [--dry-run]" >&2
      exit 2
      ;;
  esac
done

if [[ -z "$release" ]]; then
  echo "--release is required; mutable or implicit releases are forbidden" >&2
  exit 2
fi

args=(--inventory "$inventory" deploy --node all --release "$release")
if [[ "$dry_run" == "true" ]]; then
  args+=(--dry-run)
else
  args+=(--yes)
fi
exec python3 "$clusterctl" "${args[@]}"
