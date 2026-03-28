# Quickstart: Phase 1 — Foundation and MVP Launch

**Date**: 2026-03-22

## Prerequisites

- **Git**: Feature branch `003-phase1-foundation-mvp`
- **.NET 8 SDK**: For backend API
- **Node.js 20+**: For frontend and BullMQ worker
- **Docker & Docker Compose**: For PostgreSQL and Redis local instances
- **pnpm** (recommended) or **npm**: For frontend/worker dependency management

## Step 1: Start Infrastructure

```bash
# From repo root
docker compose -f docker/docker-compose.yml up -d
# This starts PostgreSQL (port 5432) and Redis (port 6379)
```

## Step 2: Backend API

```bash
# From repo root
cd backend/src/NaderGorge.API
dotnet restore
dotnet ef database update   # Apply EF Core migrations
dotnet run                   # Starts API on https://localhost:5001
```

**Verify**: `curl https://localhost:5001/api/health` → `{ "status": "healthy" }`

## Step 3: Frontend

```bash
# From repo root
cd frontend
pnpm install
cp .env.example .env.local   # Set NEXT_PUBLIC_API_URL=https://localhost:5001
pnpm dev                     # Starts Next.js on http://localhost:3000
```

**Verify**: Open `http://localhost:3000` → Landing page renders.

## Step 4: BullMQ Worker

```bash
# From repo root
cd worker
pnpm install
cp .env.example .env         # Set REDIS_URL=redis://localhost:6379
pnpm dev                     # Starts worker, listening for jobs
```

**Verify**: Worker logs `[BullMQ] Worker started, waiting for jobs...`

## Step 5: Seed Data

```bash
cd backend/src/NaderGorge.API
dotnet run -- --seed          # Inserts default roles (Admin, Teacher, Assistant, Student)
                              # and a default Admin user (phone: 01000000000, pass: Admin@123)
```

## Quick Smoke Test

1. Open `http://localhost:3000` and register a new student account.
2. Log in with the registered phone number.
3. Log in to the Admin panel using the seeded admin credentials.
4. Create a Package → Section → Lesson → Video in the Admin panel.
5. Generate a code group (e.g., 5 codes for the package).
6. Activate one code as the student → Verify the package is now accessible.
7. Watch the video → Verify watch events appear in the tracking table.
8. Take the MCQ exam → Verify instant grading and score display.

## Key Environment Variables

| Variable | Service | Description |
|----------|---------|-------------|
| `DATABASE_URL` | Backend | PostgreSQL connection string |
| `REDIS_URL` | Backend + Worker | Redis connection string |
| `JWT_SECRET` | Backend | Secret key for JWT signing |
| `JWT_EXPIRY_MINUTES` | Backend | Access token TTL (default: 15) |
| `MAX_DEVICES_PER_USER` | Backend | Device limit (default: 2) |
| `NEXT_PUBLIC_API_URL` | Frontend | Backend API base URL |
