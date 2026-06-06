.PHONY: help \
        up down build restart ps clean \
        build-frontend build-landing build-student build-admin build-backend build-worker \
        logs logs-frontend logs-landing logs-student logs-admin logs-backend logs-worker logs-db logs-redis \
        shell-frontend shell-landing shell-student shell-admin shell-backend shell-worker shell-db \
        verify-surfaces verify-surfaces-static \
        migrate migrate-add \
        dev frontend backend stop

.DEFAULT_GOAL := help

help: ## Show all available make targets
	@echo ""
	@echo "  Masar Platform — Make Targets"
	@echo "  ─────────────────────────────"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-24s\033[0m %s\n", $$1, $$2}'
	@echo ""

# =============================================================================
# DOCKER WORKFLOW
# =============================================================================

up: ## Build if needed and start all Docker services in the background
	@echo "Starting Masar Platform services..."
	docker compose up --build -d
	@echo ""
	@echo "Masar Platform services started."
	@echo "   Landing:       http://localhost:$${MASAR_LANDING_PORT:-8738}"
	@echo "   Student:       http://localhost:$${MASAR_STUDENT_PORT:-8739}"
	@echo "   Admin:         http://localhost:$${MASAR_ADMIN_PORT:-8740}"
	@echo "   Backend:       http://localhost:$${MASAR_BACKEND_PORT:-5245}"
	@echo "   Swagger:       http://localhost:$${MASAR_BACKEND_PORT:-5245}/swagger"
	@echo "   AI Bull-Board: http://localhost:$${MASAR_WORKER_PORT:-3001}/ui"
	@echo ""
	@echo "   Run 'make ps' to check container health"
	@echo "   Run 'make verify-surfaces-static' to verify Compose separation"
	@echo "   Run 'make migrate' to apply DB migrations"

down: ## Stop and remove all containers (data volumes preserved)
	@echo "Stopping Masar Platform services..."
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
	@echo "Restarting Masar Platform services..."
	docker compose down
	docker compose up --build -d
	@echo "Done."

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

verify-surfaces-static: ## Verify Compose service separation, ports, healthchecks, env, and Masar naming
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
	@PGUSER=$${POSTGRES_USER:-postgres} PGDB=$${POSTGRES_DB:-masar_platform}; \
	docker compose exec db psql -U $$PGUSER $$PGDB

# =============================================================================
# DATABASE MIGRATIONS (EF Core, no host .NET SDK required)
# =============================================================================

migrate: ## Apply all pending EF Core migrations to the database
	@echo "Running EF Core migrations..."
	docker compose --profile migration run --rm migrator
	@echo "Migrations applied."

migrate-add: ## Scaffold a new EF Core migration (usage: make migrate-add NAME=MyMigration)
	@[ "$(NAME)" ] || (echo "" && echo "  NAME is required." && echo "     Usage: make migrate-add NAME=MyMigration" && echo "" && exit 1)
	@echo "Adding migration: $(NAME)"
	docker run --rm \
		--network masar_net \
		-v "$(PWD)/backend":/src \
		-w /src \
		-e "ConnectionStrings__DefaultConnection=Host=db;Database=masar_platform;Username=postgres;Password=postgres" \
		-e "ConnectionStrings__Redis=redis:6379,abortConnect=false" \
		mcr.microsoft.com/dotnet/sdk:9.0 \
		sh -c "dotnet tool install --global dotnet-ef && \
		       export PATH=\$$PATH:/root/.dotnet/tools && \
		       dotnet restore NaderGorge.sln && \
		       dotnet ef migrations add $(NAME) \
		         --project src/NaderGorge.Infrastructure \
		         --startup-project src/NaderGorge.API \
		         --output-dir Migrations"
	@echo "Migration '$(NAME)' created in backend/src/NaderGorge.Infrastructure/Migrations/"

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
	@echo "Masar local services running."
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
# DEPLOYMENT
# =============================================================================

deploy: ## Stage, commit, merge current branch to main, push to origin, and checkout original branch
	@CURRENT_BRANCH=$$(git rev-parse --abbrev-ref HEAD); \
	echo "Deploying from branch: $$CURRENT_BRANCH"; \
	echo "Staging and committing changes..."; \
	git add .; \
	git commit -m "deploy: updates from $$CURRENT_BRANCH" || true; \
	echo "Switching to main..."; \
	git checkout main; \
	echo "Pulling latest main..."; \
	git pull origin main; \
	echo "Merging $$CURRENT_BRANCH into main..."; \
	git merge $$CURRENT_BRANCH; \
	echo "Pushing to main..."; \
	git push origin main; \
	echo "Switching back to $$CURRENT_BRANCH..."; \
	git checkout $$CURRENT_BRANCH; \
	echo "Deployment push completed."

deploy-production: deploy ## Push code to production server and rebuild containers with migrations
	@echo "Pushing code to production server git repository..."
	git push prod main
	@echo "Running deployment and database migration fixes on production server..."
	ssh -o StrictHostKeyChecking=no root@72.62.27.189 "python3 /var/www/nadergorge/scratch/fix_migrations_vps.py"

migrate-production: ## Populate migration history and run pending migrations on the VPS production server without rebuild
	@echo "Connecting and applying migrations to production database..."
	ssh -o StrictHostKeyChecking=no root@72.62.27.189 "python3 /var/www/nadergorge/scratch/fix_migrations_vps.py --skip-build"
