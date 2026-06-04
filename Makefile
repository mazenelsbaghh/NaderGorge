.PHONY: help \
        up down build restart ps clean \
        build-frontend build-backend build-worker \
        logs logs-frontend logs-backend logs-worker logs-db logs-redis \
        shell-frontend shell-backend shell-worker shell-db \
        migrate migrate-add \
        dev frontend backend stop

# ─── Default target ───────────────────────────────────────────────────────────
.DEFAULT_GOAL := help

# ─── Help ─────────────────────────────────────────────────────────────────────
help: ## Show all available make targets
	@echo ""
	@echo "  Nader George Platform — Make Targets"
	@echo "  ─────────────────────────────────────"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-22s\033[0m %s\n", $$1, $$2}'
	@echo ""

# =============================================================================
# 🐳 DOCKER WORKFLOW
# =============================================================================

up: ## Build (if needed) and start all services in the background
	@echo "🐳 Starting all services..."
	docker compose up --build -d
	@echo ""
	@echo "✅ All services started!"
	@echo "   Frontend:      http://localhost:8738"
	@echo "   Backend:       http://localhost:5245"
	@echo "   Swagger:       http://localhost:5245/swagger"
	@echo "   AI Bull-Board: http://localhost:3001/ui"
	@echo ""
	@echo "   Run 'make ps' to check container health"
	@echo "   Run 'make migrate' to apply DB migrations"

down: ## Stop and remove all containers (data volumes preserved)
	@echo "🛑 Stopping all services..."
	docker compose down
	@echo "   Done."

build: ## Rebuild ALL Docker images (no cache)
	@echo "🔨 Rebuilding all images..."
	docker compose build --no-cache

build-frontend: ## Rebuild only the frontend image
	@echo "🔨 Rebuilding frontend..."
	docker compose build --no-cache frontend

build-backend: ## Rebuild only the backend image
	@echo "🔨 Rebuilding backend..."
	docker compose build --no-cache backend

build-worker: ## Rebuild only the worker image
	@echo "🔨 Rebuilding worker..."
	docker compose build --no-cache worker

restart: ## Stop all containers then rebuild and start again
	@echo "🔄 Restarting all services..."
	docker compose down
	docker compose up --build -d
	@echo "   Done."

ps: ## Show status and health of all containers
	docker compose ps

clean: ## ⚠️  Stop containers AND destroy all named volumes (DATABASE DATA WILL BE LOST)
	@echo ""
	@echo "  ⚠️  WARNING: This will permanently destroy all database data!"
	@echo "  Press Ctrl+C within 5 seconds to cancel..."
	@echo ""
	@sleep 5
	docker compose down -v
	@echo "   Volumes destroyed."

# =============================================================================
# 📋 LOGS
# =============================================================================

logs: ## Tail live logs from ALL services
	docker compose logs -f

logs-frontend: ## Tail logs from frontend container
	docker compose logs -f frontend

logs-backend: ## Tail logs from backend container
	docker compose logs -f backend

logs-worker: ## Tail logs from worker container
	docker compose logs -f worker

logs-db: ## Tail logs from PostgreSQL container
	docker compose logs -f db

logs-redis: ## Tail logs from Redis container
	docker compose logs -f redis

# =============================================================================
# 🔑 SHELLS
# =============================================================================

shell-frontend: ## Open interactive shell in the frontend container
	docker compose exec frontend sh

shell-backend: ## Open bash shell in the backend container
	docker compose exec backend bash

shell-worker: ## Open shell in the worker container
	docker compose exec worker sh

shell-db: ## Open psql session in the database container
	@PGUSER=$${POSTGRES_USER:-postgres} PGDB=$${POSTGRES_DB:-nadergorge}; \
	docker compose exec db psql -U $$PGUSER $$PGDB

# =============================================================================
# 🗃️  DATABASE MIGRATIONS (EF Core — no host .NET SDK required)
# =============================================================================

migrate: ## Apply all pending EF Core migrations to the database
	@echo "⚙️  Running EF Core migrations..."
	docker compose --profile migration run --rm migrator
	@echo "   Migrations applied."

migrate-add: ## Scaffold a new EF Core migration (usage: make migrate-add NAME=MyMigration)
	@[ "$(NAME)" ] || (echo "" && echo "  ❌ NAME is required." && echo "     Usage: make migrate-add NAME=MyMigration" && echo "" && exit 1)
	@echo "⚙️  Adding migration: $(NAME)"
	docker run --rm \
		--network nadergorge_nadergorge_net \
		-v "$(PWD)/backend":/src \
		-w /src \
		-e "ConnectionStrings__DefaultConnection=Host=db;Database=nadergorge;Username=postgres;Password=postgres" \
		-e "ConnectionStrings__Redis=redis:6379,abortConnect=false" \
		mcr.microsoft.com/dotnet/sdk:9.0 \
		sh -c "dotnet tool install --global dotnet-ef && \
		       export PATH=\$$PATH:/root/.dotnet/tools && \
		       dotnet restore NaderGorge.sln && \
		       dotnet ef migrations add $(NAME) \
		         --project src/NaderGorge.Infrastructure \
		         --startup-project src/NaderGorge.API \
		         --output-dir Migrations"
	@echo "   Migration '$(NAME)' created in backend/src/NaderGorge.Infrastructure/Migrations/"

# =============================================================================
# 💻 LOCAL (NATIVE) DEVELOPMENT — original targets preserved
# =============================================================================

dev: stop ## Run all services natively (no Docker) — requires .NET SDK and Node.js on host
	@echo "🚀 Starting Backend ..."
	@cd backend/src/NaderGorge.API && \
		ASPNETCORE_ENVIRONMENT=E2e /usr/local/share/dotnet/x64/dotnet run --urls "http://localhost:5245" &
	@echo "⏳ Waiting for backend to start..."
	@sleep 8
	@echo "🤖 Starting Node Worker & AI Analyzer..."
	@cd worker && npm run dev &
	@echo "🎨 Starting Frontend..."
	@cd frontend && npm run dev &
	@echo ""
	@echo "✅ All services running!"
	@echo "   Frontend:      http://localhost:8738"
	@echo "   Backend:       http://localhost:5245"
	@echo "   AI Bull-Board: http://localhost:3001/ui"
	@echo ""
	@echo "Press Ctrl+C to stop all services"
	@wait

frontend: ## Run Next.js dev server natively
	@echo "🎨 Starting Frontend..."
	@cd frontend && npm run dev

backend: ## Run .NET backend natively (E2e mode)
	@echo "🚀 Starting Backend (E2e mode)..."
	@cd backend/src/NaderGorge.API && \
		ASPNETCORE_ENVIRONMENT=E2e /usr/local/share/dotnet/x64/dotnet run --environment E2e --urls "http://localhost:5245"

stop: ## Kill all native processes running on known ports
	@echo "🛑 Stopping any running services..."
	-@lsof -ti:5245 | xargs kill -9 2>/dev/null || true
	-@lsof -ti:8738 | xargs kill -9 2>/dev/null || true
	-@lsof -ti:3001 | xargs kill -9 2>/dev/null || true
	-@pkill -f "node dist/index.js" 2>/dev/null || true
	-@pkill -9 -f "dotnet.*NaderGorge" 2>/dev/null || true
	-@pkill -f "next dev" 2>/dev/null || true
	-@pkill -f "next-server" 2>/dev/null || true
	@sleep 2
	@echo "   Done."

# =============================================================================
# 🚀 DEPLOYMENT
# =============================================================================

deploy: ## Stage, commit, merge current branch to main, push to origin, and checkout original branch
	@CURRENT_BRANCH=$$(git rev-parse --abbrev-ref HEAD); \
	echo "🚀 Deploying from branch: $$CURRENT_BRANCH"; \
	echo "📦 Staging and committing changes..."; \
	git add .; \
	git commit -m "deploy: updates from $$CURRENT_BRANCH" || true; \
	echo "🔄 Switching to main..."; \
	git checkout main; \
	echo "📥 Pulling latest main..."; \
	git pull origin main; \
	echo "🔀 Merging $$CURRENT_BRANCH into main..."; \
	git merge $$CURRENT_BRANCH; \
	echo "📤 Pushing to main..."; \
	git push origin main; \
	echo "↩️ Switching back to $$CURRENT_BRANCH..."; \
	git checkout $$CURRENT_BRANCH; \
	echo "✅ Deployment push completed!"

deploy-production: deploy ## Push code to production server (VPS) and rebuild containers with migrations
	@echo "📤 Pushing code to production server git repository..."
	git push prod main
	@echo "🔌 Running deployment and database migration fixes on production server..."
	ssh -o StrictHostKeyChecking=no root@72.62.27.189 "python3 /var/www/nadergorge/scratch/fix_migrations_vps.py"

migrate-production: ## Populate migration history and run pending migrations on the VPS production server without rebuild
	@echo "🔌 Connecting and applying migrations to production database..."
	ssh -o StrictHostKeyChecking=no root@72.62.27.189 "python3 /var/www/nadergorge/scratch/fix_migrations_vps.py --skip-build"