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

SSH_OPTS="-o StrictHostKeyChecking=no -o ConnectTimeout=15 -o PreferredAuthentications=password -o ServerAliveInterval=30 -o ServerAliveCountMax=5"
export SSHPASS="${SERVER_PASS}"
SSH_CMD="sshpass -e ssh ${SSH_OPTS} ${SERVER_USER}@${SERVER_HOST}"

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
FORCE_FULL=false
REBUILD_BACKEND=false
REBUILD_ALL=false

for arg in "$@"; do
  case "$arg" in
    --no-migrate)    RUN_MIGRATIONS=false ;;
    --migrate-only)  DEPLOY=false ;;
    --force|--full)  FORCE_FULL=true ;;
  esac
done

# ─── Helpers ──────────────────────────────────────────────────────────────────
remote() {
  local max_attempts=3
  local attempt=1
  local exit_code=0
  
  while [ $attempt -le $max_attempts ]; do
    # Run the SSH command
    # Use set +e to avoid exiting the script if a command fails in the loop
    set +e
    sshpass -e ssh ${SSH_OPTS} "${SERVER_USER}@${SERVER_HOST}" "$@"
    exit_code=$?
    set -e
    
    if [ $exit_code -eq 0 ]; then
      return 0
    elif [ $exit_code -eq 255 ]; then
      log_warn "SSH connection failed (attempt $attempt/$max_attempts). Retrying in 2 seconds..."
      sleep 2
      attempt=$((attempt + 1))
    else
      return $exit_code
    fi
  done
  
  return $exit_code
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
# STEP 1.5: Preview rebuild plan and files to upload
# =============================================================================
get_rebuild_plan() {
  log_step "Fetching last deployed status from server..."
  
  # Fetch last deployed commit from server
  LAST_COMMIT=$(remote "cat ${SERVER_APP_DIR}/.last_deployed_commit 2>/dev/null || true" | tr -d '\r\n')
  
  if [ -z "$LAST_COMMIT" ]; then
    log_warn "No last deployed commit found on server. This will be a FULL REBUILD."
    echo -e "\n${BOLD}${YELLOW}📋 DEPLOYMENT PLAN:${RESET}"
    echo -e "   - Rebuild Mode: ${BOLD}${RED}FULL REBUILD${RESET} (All containers)"
    echo -e "   - Changes: First smart deployment or fresh baseline."
    echo ""
    confirm_plan
    return 0
  fi
  
  # Check if last commit is valid in local git history
  if ! git cat-file -e "$LAST_COMMIT" 2>/dev/null; then
    log_warn "Last deployed commit ($LAST_COMMIT) not found in local git history."
    log_info "This could happen if history was rewritten or branches diverged. Rebuilding all containers."
    echo -e "\n${BOLD}${YELLOW}📋 DEPLOYMENT PLAN:${RESET}"
    echo -e "   - Rebuild Mode: ${BOLD}${RED}FULL REBUILD${RESET} (All containers)"
    echo -e "   - Changes: Cannot diff history (rebuild baseline)."
    echo ""
    confirm_plan
    return 0
  fi
  
  # Get local head commit
  CURRENT_COMMIT=$(git rev-parse HEAD)
  
  # Get diff list
  CHANGED_FILES=$(git diff --name-only "$LAST_COMMIT" "$CURRENT_COMMIT")
  FILES_COUNT=$(echo "$CHANGED_FILES" | grep -c -v "^$" || true)
  
  echo -e "\n${BOLD}${YELLOW}📋 DEPLOYMENT PLAN:${RESET}"
  log_info "Last Deployed Commit: ${CYAN}$LAST_COMMIT${RESET}"
  log_info "Current Local Commit: ${CYAN}$CURRENT_COMMIT${RESET}"
  
  if [ "$FILES_COUNT" -eq 0 ]; then
    log_warn "No new commits/changes to upload since last deployment."
    echo -e "   - Rebuild Mode: ${BOLD}${GREEN}NO CHANGES${RESET} (Skip rebuild)"
  else
    log_info "Files to be deployed (${BOLD}${YELLOW}$FILES_COUNT files${RESET}):"
    # Show first 15 files
    echo "$CHANGED_FILES" | head -n 15 | sed 's/^/     • /'
    if [ "$FILES_COUNT" -gt 15 ]; then
      echo "     • ... and $((FILES_COUNT - 15)) more files."
    fi
    
    # Determine what will rebuild (REBUILD_BACKEND and REBUILD_ALL are global)
    REBUILD_BACKEND=false
    REBUILD_WORKER=false
    REBUILD_FRONTEND=false
    REBUILD_NGINX=false
    REBUILD_ALL=false
    
    while IFS= read -r file; do
      if [ -z "$file" ]; then continue; fi
      if [[ "$file" =~ ^backend/ ]]; then
        REBUILD_BACKEND=true
      elif [[ "$file" =~ ^worker/ ]]; then
        REBUILD_WORKER=true
      elif [[ "$file" =~ ^frontend/ ]]; then
        REBUILD_FRONTEND=true
      elif [[ "$file" =~ ^docker/nginx/ ]]; then
        REBUILD_NGINX=true
      elif [[ "$file" = "docker-compose.yml" || "$file" = ".env" || "$file" =~ ^docker-compose ]]; then
        echo "   ⚠️ Docker configuration change detected: $file. Forcing full rebuild."
        REBUILD_ALL=true
        break
      else
        # Any other files (like scripts, docs, readme, etc.) do NOT trigger any rebuild
        log_info "Skipping container rebuild for system/doc file: $file"
      fi
    done <<< "$CHANGED_FILES"
    
    if [ "$REBUILD_ALL" = true ]; then
      echo -e "\n   - Rebuild Mode: ${BOLD}${RED}FULL REBUILD${RESET} (Root/Infrastructure changes detected)"
      echo "     Services to rebuild: db, redis, backend, worker, frontend, nginx"
    else
      echo -e "\n   - Rebuild Mode: ${BOLD}${CYAN}SELECTIVE REBUILD${RESET} (Only modified services)"
      echo "     Services to rebuild and restart:"
      [ "$REBUILD_BACKEND" = true ] && echo "       • backend"
      [ "$REBUILD_WORKER" = true ] && echo "       • worker"
      [ "$REBUILD_FRONTEND" = true ] && echo "       • frontend (landing, student, admin, teacher, assistant)"
      [ "$REBUILD_NGINX" = true ] && echo "       • nginx"
      if [ "$REBUILD_BACKEND" = false ] && [ "$REBUILD_WORKER" = false ] && [ "$REBUILD_FRONTEND" = false ] && [ "$REBUILD_NGINX" = false ]; then
        echo "       • None (no container rebuild needed)"
      fi
    fi
  fi
  echo ""
  confirm_plan
}

confirm_plan() {
  # Prompt confirmation before proceeding if running in interactive shell
  if [[ -t 0 ]]; then
    read -p "Do you want to proceed with this deployment plan? (Y/n): " confirm
    # NOTE: The `|| true` is critical — without it, when the user types "Y",
    # the [[ ]] test returns exit code 1 (no match), making the function return 1,
    # which causes `set -e` to silently kill the entire script.
    [[ "$confirm" =~ ^[Nn]$ ]] && { log_error "Deployment cancelled by user."; exit 0; } || true
  else
    log_info "Non-interactive shell detected, proceeding automatically in 3 seconds..."
    sleep 3
  fi
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
  if GIT_SSH_COMMAND="sshpass -e ssh ${SSH_OPTS}" git push prod HEAD 2>&1; then
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
    # Rebuild migrator to ensure latest migration classes are present
    docker compose build migrator
    # Run migrator and capture output
    docker compose --profile migration run --rm migrator 2>&1
  ")

  echo "$MIGRATION_RESULT"

  if echo "$MIGRATION_RESULT" | grep -v -i "0 error\|0 warning\|#[0-9]\|info:\|DONE\|done\|resolve " | grep -qi "error\|fail\|exception"; then
    log_error "Migration may have failed — check output above"
    if [[ -t 0 ]]; then
      read -p "Continue with deployment anyway? (y/N): " confirm
      [[ "$confirm" =~ ^[Yy]$ ]] || { log_error "Aborting deployment."; exit 1; }
    else
      log_warn "Non-interactive shell detected, continuing deployment automatically..."
    fi
  else
    log_ok "Migrations applied successfully"
  fi
}

# =============================================================================
# STEP 6: Rebuild Docker containers
# =============================================================================
rebuild_containers() {
  log_step "Syncing static webp avatars to Docker assets volume..."
  remote "
    mkdir -p /var/lib/docker/volumes/massar_assets/_data/uploads/avatars
    cp -r ${SERVER_APP_DIR}/backend/src/NaderGorge.API/wwwroot/uploads/avatars/*.webp /var/lib/docker/volumes/massar_assets/_data/uploads/avatars/ || true
  "

  log_step "Rebuilding and restarting Docker containers (smart select)"
  log_info "Determining containers to rebuild..."

  if $FORCE_FULL; then
    log_info "Force full rebuild flag is set."
    remote "rm -f ${SERVER_APP_DIR}/.last_deployed_commit || true"
  fi

  remote "
    cd ${SERVER_APP_DIR}
    GIT_CMD=\"git --git-dir=${SERVER_GIT_DIR} --work-tree=${SERVER_APP_DIR}\"
    CURRENT_COMMIT=\$(\$GIT_CMD rev-parse HEAD)
    LAST_COMMIT_FILE=\".last_deployed_commit\"
    
    FORCE_FULL=false
    if [ ! -f \"\$LAST_COMMIT_FILE\" ]; then
      echo 'No last deployed commit found. Triggering full rebuild.'
      FORCE_FULL=true
    else
      LAST_COMMIT=\$(cat \"\$LAST_COMMIT_FILE\")
      if ! \$GIT_CMD cat-file -e \"\$LAST_COMMIT\" 2>/dev/null; then
        echo \"Last deployed commit \$LAST_COMMIT not found in git history. Triggering full rebuild.\"
        FORCE_FULL=true
      fi
    fi
    
    REBUILD_BACKEND=false
    REBUILD_WORKER=false
    REBUILD_FRONTEND=false
    REBUILD_NGINX=false
    REBUILD_ALL=false
    
    if [ \"\$FORCE_FULL\" = true ]; then
      REBUILD_ALL=true
    else
      echo \"Diffing between \$LAST_COMMIT and \$CURRENT_COMMIT...\"
      CHANGED_FILES=\$(\$GIT_CMD diff --name-only \"\$LAST_COMMIT\" \"\$CURRENT_COMMIT\")
      echo \"Changed files:\"
      echo \"\$CHANGED_FILES\"
      
      while IFS= read -r file; do
        if [ -z \"\$file\" ]; then continue; fi
        
        if [[ \"\$file\" =~ ^backend/ ]]; then
          REBUILD_BACKEND=true
        elif [[ \"\$file\" =~ ^worker/ ]]; then
          REBUILD_WORKER=true
        elif [[ \"\$file\" =~ ^frontend/ ]]; then
          REBUILD_FRONTEND=true
        elif [[ \"\$file\" =~ ^docker/nginx/ ]]; then
          REBUILD_NGINX=true
        elif [[ \"\$file\" = \"docker-compose.yml\" || \"\$file\" = \".env\" || \"\$file\" =~ ^docker-compose ]]; then
          echo \"Docker configuration change detected: \$file. Forcing full rebuild.\"
          REBUILD_ALL=true
          break
        else
          # Any other files (like scripts, docs, readme, etc.) do NOT trigger any rebuild
          echo \"Skipping container rebuild for system/doc file: \$file\"
        fi
      done <<< \"\$CHANGED_FILES\"
    fi
    
    if [ \"\$REBUILD_ALL\" = true ]; then
      echo \"\"
      echo \"🛠️  Rebuild Plan: [FULL REBUILD] — Rebuilding all services:\"
      echo \"   - db\"
      echo \"   - redis\"
      echo \"   - backend\"
      echo \"   - worker\"
      echo \"   - frontend (landing, student, admin, teacher, assistant)\"
      echo \"   - nginx\"
      echo \"\"
      echo \"Rebuilding all containers...\"
      docker compose up -d --build --force-recreate --remove-orphans 2>&1
    else
      echo \"\"
      echo \"🛠️  Rebuild Plan: [SELECTIVE REBUILD] — Rebuilding only modified services:\"
      if [ \"\$REBUILD_BACKEND\" = true ]; then echo \"   - backend\"; fi
      if [ \"\$REBUILD_WORKER\" = true ]; then echo \"   - worker\"; fi
      if [ \"\$REBUILD_FRONTEND\" = true ]; then echo \"   - frontend (landing, student, admin, teacher, assistant)\"; fi
      if [ \"\$REBUILD_NGINX\" = true ]; then echo \"   - nginx\"; fi
      echo \"\"
      
      # Rebuild and restart selectively
      SERVICES_TO_UP=\"\"
      
      if [ \"\$REBUILD_BACKEND\" = true ]; then
        echo \"Rebuilding backend...\"
        docker compose build backend
        SERVICES_TO_UP=\"\$SERVICES_TO_UP backend\"
      fi
      
      if [ \"\$REBUILD_WORKER\" = true ]; then
        echo \"Rebuilding worker...\"
        docker compose build worker
        SERVICES_TO_UP=\"\$SERVICES_TO_UP worker\"
      fi
      
      if [ \"\$REBUILD_FRONTEND\" = true ]; then
        echo \"Rebuilding frontend (landing)...\"
        docker compose build landing
        SERVICES_TO_UP=\"\$SERVICES_TO_UP landing student admin teacher assistant\"
      fi
      
      if [ \"\$REBUILD_NGINX\" = true ]; then
        echo \"Rebuilding nginx...\"
        docker compose build nginx
        SERVICES_TO_UP=\"\$SERVICES_TO_UP nginx\"
      fi
      
      if [ -n \"\$SERVICES_TO_UP\" ]; then
        echo \"Starting updated services:\$SERVICES_TO_UP\"
        docker compose up -d --force-recreate \$SERVICES_TO_UP 2>&1
      else
        echo \"No changes detected in backend, worker, frontend, or nginx. Skipping rebuilds.\"
      fi
    fi
    
    # Save the current commit as last deployed
    echo \"\$CURRENT_COMMIT\" > \"\$LAST_COMMIT_FILE\"
  " &
  REMOTE_PID=$!

  # Show spinner while building
  SPINNER='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
  i=0
  while kill -0 $REMOTE_PID 2>/dev/null; do
    printf "\r   ${CYAN}%s Building/Configuring...${RESET}" "${SPINNER:$((i % ${#SPINNER})):1}"
    sleep 0.2
    ((i++))
  done
  printf "\r   \n"

  wait $REMOTE_PID || true
  log_ok "Docker configuration applied successfully"

  # Clean up build cache to prevent disk from filling up
  log_step "Cleaning Docker build cache..."
  remote "docker builder prune -af 2>&1 | tail -1"
  log_ok "Build cache cleaned"
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
    log_info "Run this to see logs: SSHPASS='${SERVER_PASS}' sshpass -e ssh ${SSH_OPTS} root@${SERVER_HOST} 'docker compose -f ${SERVER_APP_DIR}/docker-compose.yml logs --tail=50'"
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
  REPO_ROOT="$(cd "${SCRIPT_DIR}/../../../.." && pwd)"
  cd "${REPO_ROOT}"

  check_deps
  get_branch

  if $DEPLOY; then
    get_rebuild_plan

    log_step "Running database schema verification check..."
    if python3 "${SCRIPT_DIR}/check_db_schema.py"; then
      log_ok "Database schema is in sync."
    else
      log_warn "Database schema is NOT in sync! Migrations will be applied during deployment."
    fi

    push_to_github
    push_to_prod
    checkout_on_server

    if ! $RUN_MIGRATIONS; then
      log_warn "Skipping migrations (--no-migrate flag)"
    elif ! $REBUILD_BACKEND && ! $REBUILD_ALL && ! $FORCE_FULL; then
      log_warn "Skipping migrations (no backend changes detected)"
    else
      run_migrations
    fi

    rebuild_containers
    health_check

    log_step "Final database schema verification check..."
    if python3 "${SCRIPT_DIR}/check_db_schema.py"; then
      log_ok "Database schema is 100% in sync! 🎉"
    else
      log_error "Database schema is STILL NOT in sync after deployment!"
      exit 1
    fi
  elif $RUN_MIGRATIONS; then
    # --migrate-only mode
    log_step "Running migrations only (no push/rebuild)"
    run_migrations
    log_ok "Done — migrations applied"

    log_step "Verifying database schema after migration..."
    if python3 "${SCRIPT_DIR}/check_db_schema.py"; then
      log_ok "Database schema is 100% in sync! 🎉"
    else
      log_error "Database schema is STILL NOT in sync after migrations!"
      exit 1
    fi
  fi

  echo ""
  echo -e "${BOLD}${GREEN}╔══════════════════════════════════════════╗${RESET}"
  echo -e "${BOLD}${GREEN}║       ✅ Deployment Complete!            ║${RESET}"
  echo -e "${BOLD}${GREEN}╚══════════════════════════════════════════╝${RESET}"
  echo ""
}

main "$@"
