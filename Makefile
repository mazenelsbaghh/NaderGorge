.PHONY: help \
        up down build restart ps clean \
        build-frontend build-landing build-student build-admin build-backend build-worker \
        logs logs-frontend logs-landing logs-student logs-admin logs-backend logs-worker logs-db logs-redis \
        shell-frontend shell-landing shell-student shell-admin shell-backend shell-worker shell-db \
        verify verify-backend verify-frontend verify-worker verify-docker verify-e2e verify-surfaces verify-surfaces-static \
        verify-performance-budgets verify-performance-budget-contracts \
        migrate migrate-add \
        ops-plan ops-check ops-build ops-fast ops-db-guard ops-db-migration \
        prod-status prod-audit prod-logs prod-plan prod-db-inventory \
        prod-db-fast-preview prod-db-fast \
        prod-small-preview prod-small prod-release-id \
        prod-build-preview prod-build prod-gate-preview prod-gate \
        prod-release-preview prod-release prod-fast-release \
        dev frontend backend stop \
        logs-production logs-production-backend \
        android-builder-start android-builder-stop android-builder-reset android-gradle-cache-clean android-builder-shell \
        build-mobile-android build-mobile-android-offline test-mobile-android build-mobile-ios build-mobile

.DEFAULT_GOAL := help

ANDROID_PROJECT_DIR := $(CURDIR)/mobile/parent-android
ANDROID_BUILDER_NAME := parent-android-builder
ANDROID_BUILDER_IMAGE := mobiledevops/android-sdk-image:34.0.0
ANDROID_GRADLE_VOLUME := parent_android_gradle_cache
PERFORMANCE_BASELINE ?= artifacts/performance-167/baseline/frontend-routes.json
PERFORMANCE_CANDIDATE ?= artifacts/performance-167/final/frontend-routes.json
PYTHON ?= python3
OPS_BASE ?= AUTO
SMALL_BASE ?= HEAD^
RELEASE ?=
MANIFEST ?= artifacts/production/build/$(RELEASE)/manifest.json
BACKUP_EVIDENCE ?= artifacts/production/migration-gates/$(RELEASE).json
NODE ?= node-1
SERVICE ?= backend
MINUTES ?= 15
REASON ?=
CONFIRM ?=
COMPONENT ?= frontend
SSH_SKILL_SCRIPTS := .agents/skills/ssh-server/scripts

help: ## Show all available make targets
	@echo ""
	@echo "  Massar Platform — Make Targets"
	@echo "  ─────────────────────────────"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-24s\033[0m %s\n", $$1, $$2}'
	@echo ""

# =============================================================================
# DOCKER WORKFLOW
# =============================================================================

up: ## Build if needed and start all Docker services in the background
	@echo "Starting Massar Platform services..."
	docker compose up --build -d
	@echo ""
	@echo "Massar Platform services started."
	@echo "   Landing:       http://localhost:$${MASSAR_LANDING_PORT:-8738}"
	@echo "   Student:       http://localhost:$${MASSAR_STUDENT_PORT:-8739}"
	@echo "   Admin:         http://localhost:$${MASSAR_ADMIN_PORT:-8740}"
	@echo "   Backend:       http://localhost:$${MASSAR_BACKEND_PORT:-5245}"
	@echo "   Swagger:       http://localhost:$${MASSAR_BACKEND_PORT:-5245}/swagger"
	@echo "   AI Bull-Board: http://localhost:$${MASSAR_WORKER_PORT:-3001}/ui"
	@echo ""
	@echo "   Run 'make ps' to check container health"
	@echo "   Run 'make verify-surfaces-static' to verify Compose separation"
	@echo "   Run 'make migrate' to apply DB migrations"

down: ## Stop and remove all containers (data volumes preserved)
	@echo "Stopping Massar Platform services..."
	docker compose down
	@echo "Done."

build: ## Rebuild ALL Docker images with no cache
	@echo "Rebuilding all images..."
	docker compose build --no-cache

build-frontend: ## Rebuild the shared frontend image used by landing/student/admin
	@echo "Rebuilding shared frontend image..."
	docker compose build --no-cache landing

build-landing: ## Rebuild the landing frontend image
	@echo "Rebuilding landing surface image..."
	docker compose build --no-cache landing

build-student: ## Rebuild the student frontend image
	@echo "Rebuilding shared frontend image for the student surface..."
	docker compose build --no-cache landing

build-admin: ## Rebuild the admin frontend image
	@echo "Rebuilding shared frontend image for the admin surface..."
	docker compose build --no-cache landing

build-backend: ## Rebuild only the backend image
	@echo "Rebuilding backend..."
	docker compose build --no-cache backend

build-worker: ## Rebuild only the worker image
	@echo "Rebuilding worker..."
	docker compose build --no-cache worker

restart: ## Stop all containers then rebuild and start again
	@echo "Restarting Massar Platform services..."
	docker compose down
	docker compose up --build -d
	@echo "Done."

quick: ## ⚡ Quick rebuild backend + frontend (uses cache, fastest)
	@echo "⚡ Quick rebuild backend + all frontend surfaces..."
	docker compose up -d --build backend landing student admin teacher assistant
	@echo "✅ Done! All services updated."

quick-back: ## ⚡ Quick rebuild backend only
	@echo "⚡ Rebuilding backend..."
	docker compose up -d --build backend
	@echo "✅ Backend updated."

quick-front: ## ⚡ Quick rebuild frontend surfaces only
	@echo "⚡ Rebuilding frontend surfaces..."
	docker compose up -d --build landing student admin teacher assistant
	@echo "✅ Frontend updated."

hot: hot-back hot-front ## 🔥 Ultra-fast: build locally + inject into containers (NO image rebuild)

hot-back: ## 🔥 Build backend locally → copy into container → restart
	@echo "🔥 Building backend locally..."
	cd backend && dotnet publish src/NaderGorge.API/NaderGorge.API.csproj -c Release -o /tmp/massar-backend-publish
	@echo "📦 Injecting into container..."
	docker cp /tmp/massar-backend-publish/. massar_platform-backend-1:/app/
	docker restart massar_platform-backend-1
	@echo "✅ Backend hot-updated! (no image rebuild)"

hot-front: ## 🔥 Build frontend locally → copy into containers → restart
	@echo "🔥 Building frontend locally..."
	cd frontend && NEXT_PUBLIC_API_URL=http://localhost:5245/api NEXT_PUBLIC_BACKEND_URL=http://localhost:5245 npm run build
	@echo "📦 Injecting into admin container..."
	@docker cp frontend/.next/standalone/. massar_admin:/app/
	@docker cp frontend/.next/static/. massar_admin:/app/.next/static/
	@docker restart massar_admin
	@echo "📦 Injecting into other surfaces..."
	@for svc in massar_landing massar_student massar_teacher massar_assistant; do \
		docker cp frontend/.next/standalone/. $$svc:/app/ 2>/dev/null; \
		docker cp frontend/.next/static/. $$svc:/app/.next/static/ 2>/dev/null; \
		docker restart $$svc 2>/dev/null; \
	done || true
	@echo "✅ All frontends hot-updated! (no image rebuild)"

ps: ## Show status and health of all containers
	docker compose ps

clean: ## Stop containers and destroy all named volumes (DATABASE DATA WILL BE LOST)
	@echo ""
	@echo "  WARNING: This will permanently destroy all database data."
	@echo "  Press Ctrl+C within 5 seconds to cancel..."
	@echo ""
	@sleep 5
	docker compose down -v
	@echo "Volumes destroyed."

# =============================================================================
# VERIFICATION
# =============================================================================

verify-surfaces-static: ## Verify Compose service separation, ports, healthchecks, env, and Massar naming
	node scripts/verify-surface-separation.mjs --static-only

verify-surfaces: ## Verify Compose separation and running HTTP endpoints
	node scripts/verify-surface-separation.mjs

endpoint-inventory: ## Regenerate backend endpoint inventory artifacts
	node scripts/generate-endpoint-inventory.mjs

test-python: ## Install Python test requirements and run smoke/inventory tests
	python3 -m pip install -r tests/requirements.txt
	python3 -m pytest -q

docker-volumes: ## Create external Docker volumes required by docker-compose.yml
	docker volume create masar_pgdata
	docker volume create masar_redisdata

verify-audit-remediation: ## Run audit remediation verification commands
	dotnet build backend/NaderGorge.sln
	dotnet test backend/NaderGorge.sln --no-build
	cd frontend && npm run lint && npm run build
	cd worker && npm run build
	python3 -m pip install -r tests/requirements.txt
	python3 -m pytest tests/test_endpoint_inventory.py tests/test_codes.py tests/test_purchases.py tests/test_video.py -q
	node scripts/generate-endpoint-inventory.mjs --check
	docker compose config -q

verify: verify-backend verify-frontend verify-worker verify-docker verify-performance-budgets ## Run the repository verification contract

verify-performance-budget-contracts: ## Run local performance budget and production cache/matrix contracts
	cd frontend && node --test scripts/check-route-performance-budgets.test.mjs
	node --check deploy/production/load/platform-workflows.js
	$(PYTHON) -m pytest -q \
		deploy/production/tests/test_static_cache_contract.py \
		deploy/production/tests/test_performance_budget_verification.py \
		deploy/production/tests/test_load_contract.py

verify-performance-budgets: verify-performance-budget-contracts ## Enforce candidate route/request/navigation/query budgets
	$(PYTHON) deploy/production/scripts/verify_performance_budgets.py \
		--baseline "$(PERFORMANCE_BASELINE)" \
		--candidate "$(PERFORMANCE_CANDIDATE)"

verify-backend: ## Restore, build, and test the backend solution
	dotnet restore backend/NaderGorge.sln
	dotnet build backend/NaderGorge.sln --no-restore
	@if [ -n "$${ConnectionStrings__DefaultConnection:-}" ]; then \
		dotnet test backend/NaderGorge.sln --no-build; \
	else \
		echo "Skipping PostgreSQL integration tests: ConnectionStrings__DefaultConnection is not set."; \
		dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --no-build; \
	fi

verify-frontend: ## Lint and build the frontend
	cd frontend && npm run lint && npm run build

verify-worker: ## Build the Node.js worker
	cd worker && npm run build

verify-docker: ## Validate Docker Compose configuration
	docker compose config -q

verify-e2e: ## Run Phase 1 auth/session browser smoke; requires backend E2e mode on api.lvh.me:5245
	cd frontend && \
		NEXT_PUBLIC_API_URL=http://api.lvh.me:5245/api \
		NEXT_PUBLIC_BACKEND_URL=http://api.lvh.me:5245 \
		npx playwright test tests/e2e/auth.spec.ts tests/e2e/admin-users.spec.ts tests/e2e/parent-report.spec.ts --project=chromium -g "Phase 1|Parent report"

# =============================================================================
# LOGS
# =============================================================================

logs: ## Tail live logs from ALL services
	docker compose logs -f

logs-frontend: ## Tail logs from all frontend surfaces
	docker compose logs -f landing student admin

logs-landing: ## Tail logs from the landing surface
	docker compose logs -f landing

logs-student: ## Tail logs from the student surface
	docker compose logs -f student

logs-admin: ## Tail logs from the admin surface
	docker compose logs -f admin

logs-backend: ## Tail logs from backend container
	docker compose logs -f backend

logs-worker: ## Tail logs from worker container
	docker compose logs -f worker

logs-db: ## Tail logs from PostgreSQL container
	docker compose logs -f db

logs-redis: ## Tail logs from Redis container
	docker compose logs -f redis

# =============================================================================
# SHELLS
# =============================================================================

shell-frontend: ## Open a shell in the landing frontend container
	docker compose exec landing sh

shell-landing: ## Open a shell in the landing container
	docker compose exec landing sh

shell-student: ## Open a shell in the student container
	docker compose exec student sh

shell-admin: ## Open a shell in the admin container
	docker compose exec admin sh

shell-backend: ## Open bash shell in the backend container
	docker compose exec backend bash

shell-worker: ## Open shell in the worker container
	docker compose exec worker sh

shell-db: ## Open psql session in the database container
	@PGUSER=$${POSTGRES_USER:-postgres} PGDB=$${POSTGRES_DB:-massar_platform}; \
	docker compose exec db psql -U $$PGUSER $$PGDB

# =============================================================================
# DATABASE MIGRATIONS (EF Core, no host .NET SDK required)
# =============================================================================

migrate: ## Apply all pending EF Core migrations to the database
	@echo "Running EF Core migrations..."
	docker compose --profile migration run --rm migrator
	@echo "Migrations applied."

migrate-add: ops-db-migration ## Alias: scaffold an EF migration without downloading tools

# =============================================================================
# LOCAL (NATIVE) DEVELOPMENT
# =============================================================================

dev: stop ## Run all services natively (requires .NET SDK and Node.js on host)
	@echo "Starting Backend ..."
	@cd backend/src/NaderGorge.API && \
		ASPNETCORE_ENVIRONMENT=E2e /usr/local/share/dotnet/x64/dotnet run --urls "http://localhost:5245" &
	@echo "Waiting for backend to start..."
	@sleep 8
	@echo "Starting Node Worker & AI Analyzer..."
	@cd worker && npm run dev &
	@echo "Starting Frontend..."
	@cd frontend && npm run dev &
	@echo ""
	@echo "Massar local services running."
	@echo "   Frontend:      http://localhost:8738"
	@echo "   Backend:       http://localhost:5245"
	@echo "   AI Bull-Board: http://localhost:3001/ui"
	@echo ""
	@echo "Press Ctrl+C to stop all services"
	@wait

frontend: ## Run Next.js dev server natively
	@echo "Starting Frontend..."
	@cd frontend && npm run dev

backend: ## Run .NET backend natively (E2e mode)
	@echo "Starting Backend (E2e mode)..."
	@cd backend/src/NaderGorge.API && \
		ASPNETCORE_ENVIRONMENT=E2e /usr/local/share/dotnet/x64/dotnet run --environment E2e --urls "http://localhost:5245"

stop: ## Kill all native processes running on known ports
	@echo "Stopping any running services..."
	-@lsof -ti:5245 | xargs kill -9 2>/dev/null || true
	-@lsof -ti:8738 | xargs kill -9 2>/dev/null || true
	-@lsof -ti:8739 | xargs kill -9 2>/dev/null || true
	-@lsof -ti:8740 | xargs kill -9 2>/dev/null || true
	-@lsof -ti:3001 | xargs kill -9 2>/dev/null || true
	-@pkill -f "node dist/index.js" 2>/dev/null || true
	-@pkill -9 -f "dotnet.*NaderGorge" 2>/dev/null || true
	-@pkill -f "next dev" 2>/dev/null || true
	-@pkill -f "next-server" 2>/dev/null || true
	@sleep 2
	@echo "Done."

# =============================================================================
# OPERATIONS AND PRODUCTION
# =============================================================================

ops-plan: ## Detect affected frontend/backend/worker/database and Docker images
	bash $(SSH_SKILL_SCRIPTS)/ops.sh plan --base="$(OPS_BASE)"

ops-check: ## Run DB guard and checks only for affected components
	bash $(SSH_SKILL_SCRIPTS)/ops.sh check --base="$(OPS_BASE)"

ops-build: ## Build only locally affected Docker images with live progress
	bash $(SSH_SKILL_SCRIPTS)/ops.sh build --base="$(OPS_BASE)"

ops-fast: ## Urgent local path: DB guard, focused checks, cached affected build
	bash $(SSH_SKILL_SCRIPTS)/ops.sh fast --base="$(OPS_BASE)"

ops-db-guard: ## Block EF model changes that do not include a migration
	bash $(SSH_SKILL_SCRIPTS)/ops.sh db-guard --base="$(OPS_BASE)"

ops-db-migration: ## Add EF migration using installed tools (NAME=Required; no downloads)
	@[ "$(NAME)" ] || (echo "Usage: make ops-db-migration NAME=DescribeTheSchemaChange" && exit 2)
	bash $(SSH_SKILL_SCRIPTS)/ops.sh db-add "$(NAME)" --base="$(OPS_BASE)"

prod-status: ## Show three-node Production health/quorum/release with evidence
	bash $(SSH_SKILL_SCRIPTS)/massar.sh status

prod-audit: ## Run the read-only three-node Production audit
	bash $(SSH_SKILL_SCRIPTS)/massar.sh audit

prod-logs: ## Read redacted Production logs (NODE/SERVICE/MINUTES)
	bash $(SSH_SKILL_SCRIPTS)/massar.sh logs "$(NODE)" "$(SERVICE)" "$(MINUTES)"

prod-plan: ## Show affected areas and immutable Production image plan
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh plan --base="$(OPS_BASE)"

prod-release-id: ## Print the exact immutable release ID; never type a short SHA
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh release-id

prod-small-preview: ## Preview one-command safe release (COMPONENT/REASON)
	@[ "$(REASON)" ] || (echo "REASON is required for prod-small-preview" && exit 2)
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh small-release \
		--component="$(COMPONENT)" --reason="$(REASON)" --base="$(SMALL_BASE)"

prod-small: ## Build, gate, and roll safely; CONFIRM must match COMPONENT uppercase
	@[ "$(REASON)" ] || (echo "REASON is required for prod-small" && exit 2)
	@expected="$$(printf '%s' "$(COMPONENT)" | tr '[:lower:]' '[:upper:]')"; \
		[ "$(CONFIRM)" = "$$expected" ] || \
		(echo "Refusing: review prod-small-preview then use CONFIRM=$$expected" && exit 2)
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh small-release \
		--component="$(COMPONENT)" --reason="$(REASON)" \
		--base="$(SMALL_BASE)" --yes

prod-db-inventory: ## Read-only: compare expected EF tables/migrations with Production
	bash $(SSH_SKILL_SCRIPTS)/database.sh inventory

prod-db-fast-preview: ## Preview no-build repair using current release migrator
	@[ "$(REASON)" ] || (echo "REASON is required for prod-db-fast-preview" && exit 2)
	bash $(SSH_SKILL_SCRIPTS)/database.sh fast \
		--base="$(OPS_BASE)" --reason="$(REASON)"

prod-db-fast: ## Repair DB drift with current migrator; CONFIRM=DB-ONLY required
	@[ "$(REASON)" ] || (echo "REASON is required for prod-db-fast" && exit 2)
	@[ "$(CONFIRM)" = "DB-ONLY" ] || \
		(echo "Refusing: use CONFIRM=DB-ONLY after reviewing prod-db-fast-preview" && exit 2)
	bash $(SSH_SKILL_SCRIPTS)/database.sh fast \
		--base="$(OPS_BASE)" --reason="$(REASON)" --yes

prod-build-preview: ## Preview node-3 immutable build (RELEASE required)
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh build --release="$(RELEASE)"

prod-build: ## Build/distribute four immutable images on node-3 (RELEASE required)
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh build --release="$(RELEASE)" --yes

prod-gate-preview: ## Preview backup/restore migration gate (RELEASE/MANIFEST)
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh gate \
		--release="$(RELEASE)" --manifest="$(MANIFEST)" --output="$(BACKUP_EVIDENCE)"

prod-gate: ## Create backup/restore migration evidence (RELEASE/MANIFEST)
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh gate \
		--release="$(RELEASE)" --manifest="$(MANIFEST)" \
		--output="$(BACKUP_EVIDENCE)" --yes

prod-release-preview: ## Preview migrate + rolling three-node release
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh release \
		--release="$(RELEASE)" --manifest="$(MANIFEST)" \
		--backup-evidence="$(BACKUP_EVIDENCE)" --base="$(OPS_BASE)"

prod-release: ## Run reviewed migrate + zero-downtime rolling release
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh release \
		--release="$(RELEASE)" --manifest="$(MANIFEST)" \
		--backup-evidence="$(BACKUP_EVIDENCE)" --base="$(OPS_BASE)" --yes

prod-fast-release: ## Urgent safe release; REASON required, safety gates remain
	@[ "$(REASON)" ] || (echo "REASON is required for prod-fast-release" && exit 2)
	bash $(SSH_SKILL_SCRIPTS)/deploy.sh fast-release \
		--release="$(RELEASE)" --manifest="$(MANIFEST)" \
		--backup-evidence="$(BACKUP_EVIDENCE)" --base="$(OPS_BASE)" \
		--reason="$(REASON)" --yes

deploy: prod-release-preview ## Safe default: deployment command is preview-only

deploy-production: prod-release ## Backward-compatible safe rolling Production release

migrate-production: ## Refuse unbound DB migration; use prod-gate then prod-release
	@echo "Blocked: Production migrations require release manifest and backup/restore evidence."
	@echo "Run make prod-gate, then make prod-release."
	@exit 2

logs-production: prod-logs ## Backward-compatible redacted Production logs

logs-production-backend: ## Redacted backend logs from NODE (default node-1)
	@$(MAKE) prod-logs NODE="$(NODE)" SERVICE=backend MINUTES="$(MINUTES)"


# =============================================================================
# MOBILE BUILDS
# =============================================================================

android-builder-start: ## Start/reuse the persistent Android Docker builder
	@test -d "$(ANDROID_PROJECT_DIR)" || (echo "Android project directory not found: $(ANDROID_PROJECT_DIR)" && exit 1)
	@docker volume create "$(ANDROID_GRADLE_VOLUME)" >/dev/null
	@if docker ps --format '{{.Names}}' | grep -qx "$(ANDROID_BUILDER_NAME)"; then \
		echo "Android builder already running: $(ANDROID_BUILDER_NAME)"; \
	elif docker ps -a --format '{{.Names}}' | grep -qx "$(ANDROID_BUILDER_NAME)"; then \
		echo "Starting existing Android builder: $(ANDROID_BUILDER_NAME)"; \
		docker start "$(ANDROID_BUILDER_NAME)" >/dev/null; \
	else \
		echo "Creating Android builder: $(ANDROID_BUILDER_NAME)"; \
		docker run -d \
			--name "$(ANDROID_BUILDER_NAME)" \
			--dns 8.8.8.8 \
			-v "$(ANDROID_PROJECT_DIR):/app" \
			-v "$(ANDROID_GRADLE_VOLUME):/home/mobiledevops/.gradle" \
			-e GRADLE_USER_HOME=/home/mobiledevops/.gradle \
			-w /app \
			"$(ANDROID_BUILDER_IMAGE)" \
			tail -f /dev/null >/dev/null; \
	fi
	@if ! docker exec "$(ANDROID_BUILDER_NAME)" sh -lc 'test -w /home/mobiledevops/.gradle'; then \
		docker exec -u root "$(ANDROID_BUILDER_NAME)" chown -R mobiledevops:mobiledevops /home/mobiledevops/.gradle; \
	fi

android-builder-stop: ## Stop the persistent Android Docker builder
	@docker stop "$(ANDROID_BUILDER_NAME)" >/dev/null 2>&1 || true
	@echo "Android builder stopped."

android-builder-reset: ## Recreate the Android Docker builder without deleting Gradle cache
	@docker rm -f "$(ANDROID_BUILDER_NAME)" >/dev/null 2>&1 || true
	@$(MAKE) android-builder-start

android-gradle-cache-clean: ## Delete the persistent Android Gradle Docker volume cache
	@docker rm -f "$(ANDROID_BUILDER_NAME)" >/dev/null 2>&1 || true
	@docker volume rm "$(ANDROID_GRADLE_VOLUME)" >/dev/null 2>&1 || true
	@echo "Android Gradle cache deleted."

android-builder-shell: android-builder-start ## Open a shell in the Android Docker builder
	docker exec -it -w /app "$(ANDROID_BUILDER_NAME)" bash

build-mobile-android: android-builder-start ## Fast debug APK build using the persistent Android builder
	docker exec -w /app "$(ANDROID_BUILDER_NAME)" ./gradlew assembleDebug --build-cache --parallel

build-mobile-android-offline: android-builder-start ## Fast offline debug APK build after dependencies are cached
	docker exec -w /app "$(ANDROID_BUILDER_NAME)" ./gradlew assembleDebug --offline --build-cache --parallel

test-mobile-android: android-builder-start ## Run Android unit tests using the persistent Android builder
	docker exec -w /app "$(ANDROID_BUILDER_NAME)" ./gradlew test --build-cache --parallel

build-mobile-ios: ## Compile and test the iOS mobile app on host
	@if [ -d "mobile/parent-ios" ]; then \
		echo "Building iOS app..."; \
		cd mobile/parent-ios && (xcodebuild -scheme NaderGorgeParent -sdk iphonesimulator clean build test || swift test && swift build); \
	elif [ -d "parent-ios" ]; then \
		echo "Building iOS app..."; \
		cd parent-ios && (xcodebuild -scheme NaderGorgeParent -sdk iphonesimulator clean build test || swift test && swift build); \
	else \
		echo "iOS project directory not found. Checked 'mobile/parent-ios' and 'parent-ios'."; \
	fi

build-mobile: build-mobile-android build-mobile-ios ## Compile and test both mobile apps
