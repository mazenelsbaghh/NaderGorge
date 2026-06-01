# Makefile Target Reference Contract

**Feature**: `064-full-docker-setup`
**Type**: Developer Interface Contract
**Phase**: 1 — Design

---

## Purpose

This document defines the complete contract for all Makefile targets introduced by this feature. It serves as the authoritative specification for what each `make <target>` command does, its prerequisites, and its expected outcome.

---

## Target Inventory

### Docker Workflow Targets

| Target | Command Signature | Description |
|--------|------------------|-------------|
| `up` | `make up` | Build images if needed + start all services detached |
| `down` | `make down` | Stop and remove all containers (keeps volumes) |
| `build` | `make build` | Force rebuild all Docker images (no cache by default) |
| `build-frontend` | `make build-frontend` | Rebuild only the frontend image |
| `build-backend` | `make build-backend` | Rebuild only the backend image |
| `build-worker` | `make build-worker` | Rebuild only the worker image |
| `restart` | `make restart` | Stop all containers then start again (`down` + `up`) |
| `ps` | `make ps` | Show status, ports, and health of all containers |
| `logs` | `make logs` | Tail live logs from ALL services |
| `logs-frontend` | `make logs-frontend` | Tail logs from frontend container only |
| `logs-backend` | `make logs-backend` | Tail logs from backend container only |
| `logs-worker` | `make logs-worker` | Tail logs from worker container only |
| `logs-db` | `make logs-db` | Tail logs from PostgreSQL container only |
| `logs-redis` | `make logs-redis` | Tail logs from Redis container only |
| `shell-frontend` | `make shell-frontend` | Open interactive shell in frontend container |
| `shell-backend` | `make shell-backend` | Open interactive shell in backend container |
| `shell-worker` | `make shell-worker` | Open interactive shell in worker container |
| `shell-db` | `make shell-db` | Open `psql` shell in database container |
| `clean` | `make clean` | Stop containers AND remove all named volumes (data loss!) |
| `migrate` | `make migrate` | Apply all pending EF Core migrations to the database |
| `migrate-add` | `make migrate-add NAME=<name>` | Scaffold a new EF Core migration with the given name |
| `help` | `make help` | Print all available targets with descriptions |

### Local Dev Targets (preserved from existing Makefile)

| Target | Command Signature | Description |
|--------|------------------|-------------|
| `dev` | `make dev` | Run all services natively (no Docker) |
| `frontend` | `make frontend` | Run Next.js dev server natively |
| `backend` | `make backend` | Run .NET backend natively |
| `stop` | `make stop` | Kill all native processes on known ports |

---

## Detailed Contracts

### `make up`

```
Preconditions:  Docker daemon running; .env file exists at project root
Actions:        docker compose up --build -d
Postconditions: All 6 containers running and healthy
Exit code:      0 on success, non-zero if any container fails to start
Side effects:   Creates Docker network and named volumes if not present
```

### `make down`

```
Preconditions:  Containers may or may not be running (idempotent)
Actions:        docker compose down
Postconditions: All containers stopped and removed; volumes PRESERVED
Exit code:      0 always
Side effects:   None
```

### `make clean`

```
Preconditions:  None
Actions:        docker compose down -v
Postconditions: All containers stopped, removed; ALL named volumes DESTROYED
Exit code:      0 always
Side effects:   ⚠️ DATABASE DATA PERMANENTLY DELETED. Prints warning before executing.
```

### `make migrate`

```
Preconditions:  db container is healthy; backend source available
Actions:        docker compose run --rm migrator
                (migrator: dotnet ef database update --project NaderGorge.Infrastructure 
                           --startup-project NaderGorge.API)
Postconditions: All pending migrations applied to PostgreSQL
Exit code:      0 on success; non-zero if migration fails
Side effects:   Schema changes committed to the database
```

### `make migrate-add NAME=<name>`

```
Preconditions:  NAME variable must be set; Docker daemon running
Actions:        docker run --rm \
                  -v $(PWD)/backend:/src \
                  -w /src \
                  mcr.microsoft.com/dotnet/sdk:8.0 \
                  dotnet ef migrations add $(NAME) \
                    --project src/NaderGorge.Infrastructure \
                    --startup-project src/NaderGorge.API \
                    --output-dir Data/Migrations
Postconditions: New migration file in backend/src/NaderGorge.Infrastructure/Data/Migrations/
Exit code:      Error with message "NAME is required. Usage: make migrate-add NAME=MyMigration" if NAME unset
Side effects:   New files written to host filesystem via volume mount
```

### `make shell-db`

```
Preconditions:  db container running
Actions:        docker compose exec db psql -U ${POSTGRES_USER} ${POSTGRES_DB}
Postconditions: Interactive psql session
Exit code:      Inherits from psql
```

---

## Environment Variable Requirements at Runtime

The following variables MUST exist in `.env` for `make up` to succeed:

**Mandatory (no defaults):**
- `JWT_SECRET`
- `GEMINI_API_KEY`

**Optional (have defaults in Compose):**
- `POSTGRES_USER` → default: `postgres`
- `POSTGRES_PASSWORD` → default: `postgres`
- `POSTGRES_DB` → default: `nadergorge`
- All other variables → see `data-model.md` for defaults
