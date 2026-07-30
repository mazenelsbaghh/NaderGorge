#!/usr/bin/env bash
set -euo pipefail

readonly ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
readonly PLANNER="$ROOT/.agents/skills/ssh-server/scripts/release_plan.py"
readonly DEFAULT_BASE="${MASSAR_BASE_REF:-AUTO}"

step() {
  printf '\n\033[1;36m[%s] %s\033[0m\n' "$(date -u +%H:%M:%S)" "$*"
}

run() {
  step "$*"
  "$@"
}

usage() {
  cat <<'EOF'
Usage: ops.sh <command> [--base=GIT_REF]

  plan          Explain affected components and Docker images
  check         Enforce EF migration coverage, then run focused checks
  build         Build only affected local Docker images with live output
  fast          Emergency local path: guard + focused checks + cached build
  db-guard      Fail if EF model changes have no new migration
  db-add NAME   Scaffold an EF migration using the already-installed dotnet-ef

Production is separate and explicit:
  deploy.sh plan|build|gate|release|fast-release --help
EOF
}

base="$DEFAULT_BASE"
arguments=()
for argument in "${@:2}"; do
  case "$argument" in
    --base=*) base="${argument#*=}" ;;
    *) arguments+=("$argument") ;;
  esac
done

focused_checks() {
  local components
  components="$(python3 "$PLANNER" components --base "$base")"
  database_guard "$components"
  if [[ " $components " == *" backend "* ]]; then
    run dotnet test \
      "$ROOT/backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj" \
      --no-restore
  fi
  if [[ " $components " == *" frontend "* ]]; then
    run bash -lc \
      "cd '$ROOT/frontend' && npm_config_offline=true npm run lint && npm_config_offline=true npm run typecheck"
  fi
  if [[ " $components " == *" worker "* ]]; then
    run bash -lc "cd '$ROOT/worker' && npm_config_offline=true npm test"
  fi
  if [[ " $components " == *" infrastructure "* ]]; then
    run docker compose -f "$ROOT/docker-compose.yml" config -q
  fi
}

database_guard() {
  local components="${1:-$(python3 "$PLANNER" components --base "$base")}"
  run python3 "$PLANNER" check-db --base "$base"
  if [[ " $components " != *" database "* ]]; then
    return
  fi
  command -v dotnet >/dev/null || {
    echo "dotnet is unavailable; database verification fails closed." >&2
    exit 2
  }
  dotnet ef --version >/dev/null 2>&1 || {
    echo "dotnet-ef is not installed; refusing to download it automatically." >&2
    exit 2
  }
  run dotnet build "$ROOT/backend/src/NaderGorge.API/NaderGorge.API.csproj" --no-restore
  run dotnet ef migrations has-pending-model-changes \
    --project "$ROOT/backend/src/NaderGorge.Infrastructure" \
    --startup-project "$ROOT/backend/src/NaderGorge.API" \
    --no-build
}

require_local_docker_bases() {
  local service
  local dockerfile
  local base_image
  for service in "$@"; do
    case "$service" in
      backend) dockerfile="$ROOT/backend/Dockerfile" ;;
      migrator) dockerfile="$ROOT/backend/Dockerfile.migrator" ;;
      worker) dockerfile="$ROOT/worker/Dockerfile" ;;
      landing) dockerfile="$ROOT/frontend/Dockerfile" ;;
      gateway) dockerfile="$ROOT/docker/nginx/Dockerfile" ;;
      *) echo "Unsupported Docker service: $service" >&2; exit 2 ;;
    esac
    while IFS= read -r base_image; do
      [[ "$base_image" == "scratch" ]] && continue
      docker image inspect "$base_image" >/dev/null 2>&1 || {
        echo "Missing local base image: $base_image" >&2
        echo "Offline build refused; no image will be downloaded automatically." >&2
        exit 2
      }
    done < <(awk 'toupper($1) == "FROM" {print $2}' "$dockerfile")
  done
}

build_affected() {
  local services=()
  local selected_services
  selected_services="$(python3 "$PLANNER" services --base "$base")"
  if [[ -z "$selected_services" ]]; then
    step "No Docker image is affected."
    return
  fi
  read -r -a services <<<"$selected_services"
  require_local_docker_bases "${services[@]}"
  run docker compose -f "$ROOT/docker-compose.yml" build \
    --pull=false --network=none --progress=plain "${services[@]}"
}

case "${1:-help}" in
  plan)
    run python3 "$PLANNER" plan --base "$base"
    ;;
  check)
    focused_checks
    ;;
  build)
    run python3 "$PLANNER" plan --base "$base"
    run python3 "$PLANNER" check-db --base "$base"
    build_affected
    ;;
  fast)
    step "FAST mode keeps the DB guard and focused verification; it only avoids unrelated checks."
    focused_checks
    build_affected
    ;;
  db-guard)
    # An explicit DB command must verify the compiled EF model even when the
    # selected Git base contains no new schema paths.
    database_guard "database"
    ;;
  db-add)
    name="${arguments[0]:-}"
    [[ "$name" =~ ^[A-Za-z][A-Za-z0-9_]{2,80}$ ]] || {
      echo "Migration name must be 3..80 letters, numbers, or underscores." >&2
      exit 2
    }
    command -v dotnet >/dev/null || {
      echo "dotnet is unavailable; no dependency will be downloaded automatically." >&2
      exit 2
    }
    dotnet ef --version >/dev/null 2>&1 || {
      echo "dotnet-ef is not already installed; refusing to download it automatically." >&2
      exit 2
    }
    run dotnet build "$ROOT/backend/src/NaderGorge.API/NaderGorge.API.csproj" --no-restore
    run dotnet ef migrations add "$name" \
      --project "$ROOT/backend/src/NaderGorge.Infrastructure" \
      --startup-project "$ROOT/backend/src/NaderGorge.API" \
      --output-dir Migrations \
      --no-build
    database_guard
    ;;
  help|-h|--help)
    usage
    ;;
  *)
    usage >&2
    exit 2
    ;;
esac
