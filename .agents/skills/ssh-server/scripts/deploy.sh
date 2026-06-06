#!/usr/bin/env bash
# =============================================================================
# deploy.sh — Nader Gorge Full Deployment Script
# =============================================================================
# Usage:
#   ./deploy.sh              → push current branch, deploy, run migrations
#   ./deploy.sh --no-migrate → push + deploy only, skip migrations
#   ./deploy.sh --migrate-only → only run migrations on server (no push/rebuild)
# =============================================================================

set -euo pipefail

# ─── Config ───────────────────────────────────────────────────────────────────
SERVER_HOST="72.62.27.189"
SERVER_USER="root"
SERVER_PASS="MazenElsbagh.12"
SERVER_APP_DIR="/var/www/nadergorge"
SERVER_GIT_DIR="/var/www/nadergorge.git"

SSH_OPTS="-o StrictHostKeyChecking=no -o ConnectTimeout=15"
SSH_CMD="sshpass -p '${SERVER_PASS}' ssh ${SSH_OPTS} ${SERVER_USER}@${SERVER_HOST}"

# ─── Colors ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
RESET='\033[0m'

log_step()  { echo -e "\n${BOLD}${BLUE}▶ $1${RESET}"; }
log_ok()    { echo -e "${GREEN}✅ $1${RESET}"; }
log_warn()  { echo -e "${YELLOW}⚠️  $1${RESET}"; }
log_error() { echo -e "${RED}❌ $1${RESET}"; }
log_info()  { echo -e "${CYAN}   $1${RESET}"; }

# ─── Flags ────────────────────────────────────────────────────────────────────
RUN_MIGRATIONS=true
DEPLOY=true

for arg in "$@"; do
  case "$arg" in
    --no-migrate)    RUN_MIGRATIONS=false ;;
    --migrate-only)  DEPLOY=false ;;
  esac
done

# ─── Helpers ──────────────────────────────────────────────────────────────────
remote() {
  sshpass -p "${SERVER_PASS}" ssh ${SSH_OPTS} "${SERVER_USER}@${SERVER_HOST}" "$@"
}

check_deps() {
  log_step "Checking local dependencies"
  if ! command -v sshpass &>/dev/null; then
    log_warn "sshpass not found — installing via Homebrew..."
    brew install sshpass
  fi
  log_ok "sshpass available"
}

# =============================================================================
# STEP 1: Get current branch
# =============================================================================
get_branch() {
  BRANCH=$(git branch --show-current)
  if [[ -z "$BRANCH" ]]; then
    log_error "Not on a git branch (detached HEAD?). Aborting."
    exit 1
  fi
  echo -e "\n${BOLD}╔══════════════════════════════════════════╗${RESET}"
  echo -e "${BOLD}║   🚀 Nader Gorge Deployment Pipeline     ║${RESET}"
  echo -e "${BOLD}╚══════════════════════════════════════════╝${RESET}"
  log_info "Branch:  ${YELLOW}${BRANCH}${RESET}"
  log_info "Server:  ${YELLOW}${SERVER_HOST}${RESET}"
  log_info "Migrate: ${YELLOW}${RUN_MIGRATIONS}${RESET}"
  echo ""
}

# =============================================================================
# STEP 2: Push to GitHub (origin)
# =============================================================================
push_to_github() {
  log_step "Pushing to GitHub (origin/${BRANCH})"
  if git push origin HEAD 2>&1; then
    log_ok "GitHub updated"
  else
    log_warn "GitHub push failed (may already be up to date)"
  fi
}

# =============================================================================
# STEP 3: Push to Production Server (prod remote)
# =============================================================================
push_to_prod() {
  log_step "Pushing to production server (prod/${BRANCH})"
  if sshpass -p "${SERVER_PASS}" git push prod HEAD 2>&1; then
    log_ok "Production git repo updated"
  else
    log_warn "Prod push had warnings (may already be up to date)"
  fi
}

# =============================================================================
# STEP 4: Checkout latest code on server
# =============================================================================
checkout_on_server() {
  log_step "Checking out ${BRANCH} on server"
  remote "
    git --git-dir=${SERVER_GIT_DIR} --work-tree=${SERVER_APP_DIR} checkout ${BRANCH} -f
    echo 'Checked out: '
    git -C ${SERVER_GIT_DIR} log --oneline -3
  "
  log_ok "Code updated on server"
}

# =============================================================================
# STEP 5: Run EF Core Migrations
# =============================================================================
run_migrations() {
  log_step "Running EF Core database migrations"
  log_info "Starting migrator container..."

  MIGRATION_RESULT=$(remote "
    cd ${SERVER_APP_DIR}
    # Run migrator and capture output
    docker compose --profile migration run --rm migrator 2>&1
  ")

  echo "$MIGRATION_RESULT"

  if echo "$MIGRATION_RESULT" | grep -qi "error\|fail\|exception"; then
    log_error "Migration may have failed — check output above"
    read -p "Continue with deployment anyway? (y/N): " confirm
    [[ "$confirm" =~ ^[Yy]$ ]] || { log_error "Aborting deployment."; exit 1; }
  else
    log_ok "Migrations applied successfully"
  fi
}

# =============================================================================
# STEP 6: Rebuild Docker containers
# =============================================================================
rebuild_containers() {
  log_step "Rebuilding and restarting Docker containers"
  log_info "This may take a few minutes..."

  remote "
    cd ${SERVER_APP_DIR}
    docker compose up -d --build --force-recreate --remove-orphans 2>&1
  " &
  REMOTE_PID=$!

  # Show spinner while building
  SPINNER='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
  i=0
  while kill -0 $REMOTE_PID 2>/dev/null; do
    printf "\r   ${CYAN}%s Building...${RESET}" "${SPINNER:$((i % ${#SPINNER})):1}"
    sleep 0.2
    ((i++))
  done
  printf "\r   \n"

  wait $REMOTE_PID || true
  log_ok "Docker containers rebuilt"
}

# =============================================================================
# STEP 7: Health check
# =============================================================================
health_check() {
  log_step "Verifying container health"
  log_info "Waiting 10s for containers to settle..."
  sleep 10

  CONTAINER_STATUS=$(remote "docker compose -f ${SERVER_APP_DIR}/docker-compose.yml ps --format 'table {{.Name}}\t{{.Status}}'")
  echo "$CONTAINER_STATUS"

  UNHEALTHY=$(echo "$CONTAINER_STATUS" | grep -i "unhealthy\|exit\|dead" || true)
  if [[ -n "$UNHEALTHY" ]]; then
    log_error "Some containers are unhealthy:"
    echo "$UNHEALTHY"
    log_info "Run this to see logs: sshpass -p '${SERVER_PASS}' ssh ${SSH_OPTS} root@${SERVER_HOST} 'docker compose -f ${SERVER_APP_DIR}/docker-compose.yml logs --tail=50'"
  else
    log_ok "All containers are healthy! 🎉"
  fi
}

# =============================================================================
# MAIN
# =============================================================================
main() {
  # Ensure we're in the repo root
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  REPO_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"
  cd "${REPO_ROOT}"

  check_deps
  get_branch

  if $DEPLOY; then
    push_to_github
    push_to_prod
    checkout_on_server

    if $RUN_MIGRATIONS; then
      run_migrations
    else
      log_warn "Skipping migrations (--no-migrate flag)"
    fi

    rebuild_containers
    health_check
  elif $RUN_MIGRATIONS; then
    # --migrate-only mode
    log_step "Running migrations only (no push/rebuild)"
    run_migrations
    log_ok "Done — migrations applied"
  fi

  echo ""
  echo -e "${BOLD}${GREEN}╔══════════════════════════════════════════╗${RESET}"
  echo -e "${BOLD}${GREEN}║       ✅ Deployment Complete!            ║${RESET}"
  echo -e "${BOLD}${GREEN}╚══════════════════════════════════════════╝${RESET}"
  echo ""
}

main "$@"
