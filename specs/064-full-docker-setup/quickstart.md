# Quick Start: Docker Development Workflow

**Feature**: `064-full-docker-setup`
**Audience**: Developers setting up Nader George platform for the first time
**Prerequisite**: Docker Desktop (or Docker Engine + Compose plugin) and `make` installed

---

## First-Time Setup (5 minutes)

### 1. Create your `.env` file

```bash
cp .env.example .env
```

Open `.env` and fill in the mandatory secrets (marked with `# REQUIRED`):

```env
# Database (defaults work for local dev)
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=nadergorge

# JWT — REQUIRED: change this to any 32+ character random string
JWT_SECRET=change_me_to_a_long_random_secret_string_here

# AI Worker — REQUIRED: get from https://aistudio.google.com/
GEMINI_API_KEY=AIzaSy...

# Telegram — REQUIRED: get API_ID / API_HASH from https://my.telegram.org
TELEGRAM_API_ID=12345678
TELEGRAM_API_HASH=abc123def456...
TELEGRAM_BOT_TOKEN=8400387683:AAGcs...
TELEGRAM_DUMP_CHAT_ID=658360132
```

### 2. Start everything

```bash
make up
```

This builds all Docker images and starts all 6 services. First run takes 3–5 minutes to download base images and compile. Subsequent runs are fast (< 30 seconds).

### 3. Apply database migrations

```bash
make migrate
```

Run once after first `make up`, and after every `make migrate-add`.

### 4. Verify everything is running

```bash
make ps
```

You should see all containers as `healthy`.

**Access points:**
| Service | URL |
|---------|-----|
| Frontend | http://localhost:8738 |
| Backend API | http://localhost:5245 |
| Swagger UI | http://localhost:5245/swagger |
| Bull Board (AI jobs) | http://localhost:3001/ui |

---

## Daily Workflow

### Start work

```bash
make up    # Start all services (fast if images already built)
```

### View logs

```bash
make logs             # All services
make logs-backend     # Backend only
make logs-worker      # AI worker only
make logs-frontend    # Frontend only
```

### Open a shell inside a container

```bash
make shell-backend    # .NET container shell
make shell-worker     # Node.js worker shell
make shell-db         # psql database shell
```

### Stop at end of day

```bash
make down    # Stop containers (data preserved in volumes)
```

---

## Database Migrations

### Apply pending migrations

```bash
make migrate
```

### Create a new migration (after changing an EF entity)

```bash
make migrate-add NAME=AddSomeNewFeature
```

The migration file is created in `backend/src/NaderGorge.Infrastructure/Data/Migrations/` and can be committed to git normally.

---

## Rebuilding After Code Changes

Docker images are **not** rebuilt automatically when you change source code.

```bash
# Rebuild everything
make build

# Rebuild one service only (faster)
make build-frontend
make build-backend
make build-worker

# Then restart
make up
```

> **Tip**: For active development with hot reload, use `make dev` (native local mode) instead of Docker. Docker images are best for CI validation and staging.

---

## Clean Slate (Nuclear Option)

⚠️ This destroys all data including the database.

```bash
make clean    # Stops containers AND deletes all volumes
make up       # Fresh start
make migrate  # Re-apply migrations
```

---

## Troubleshooting

### Port already in use

```bash
make stop   # Kills native processes on 5245, 8738, 3001
make down   # Stops Docker containers
make up     # Try again
```

### Backend won't start (DB not ready)

The backend has a `depends_on: db: condition: service_healthy`. If the DB is slow to initialize:

```bash
make logs-db   # Check DB logs
make restart   # Restart all services
```

### Migration fails

```bash
make logs-db   # Check if DB is healthy
make shell-db  # Connect to psql and verify the database exists
```

### "NAME is required" when running migrate-add

```bash
# Wrong:
make migrate-add

# Correct:
make migrate-add NAME=AddSomeFeature
```

---

## Switching Between Docker and Native Dev

Both workflows coexist:

```bash
make up   # Docker mode — full stack in containers

# OR

make dev  # Native mode — processes spawn directly on host
          # Requires .NET SDK, Node.js, and npm installed
```

They use the same ports, so run only one at a time. Use `make down` or `make stop` to switch between them.
