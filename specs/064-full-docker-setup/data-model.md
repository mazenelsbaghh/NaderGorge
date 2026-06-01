# Data Model: Docker Environment Configuration

**Feature**: `064-full-docker-setup`
**Phase**: 1 — Design

---

## Overview

This feature introduces no new database tables. The "data model" for a Docker infrastructure feature is its **environment variable schema** — the contracts between the host machine's `.env` file and each containerized service.

---

## Environment Variable Schema

### Root `.env` / `.env.example` (all variables)

| Variable | Service(s) | Required | Default (dev) | Description |
|----------|-----------|----------|---------------|-------------|
| `POSTGRES_USER` | db, backend, worker | Yes | `postgres` | PostgreSQL superuser username |
| `POSTGRES_PASSWORD` | db, backend, worker | Yes | `postgres` | PostgreSQL superuser password |
| `POSTGRES_DB` | db, backend, worker | Yes | `nadergorge` | PostgreSQL database name |
| `POSTGRES_HOST` | backend, worker | Yes | `db` | Docker service DNS name for PostgreSQL |
| `POSTGRES_PORT` | backend, worker | No | `5432` | PostgreSQL port (internal) |
| `REDIS_HOST` | backend, worker | Yes | `redis` | Docker service DNS name for Redis |
| `REDIS_PORT` | backend, worker | No | `6379` | Redis port (internal) |
| `JWT_SECRET` | backend | Yes | *(must set)* | JWT signing secret (min 32 chars) |
| `JWT_ISSUER` | backend | No | `NaderGorgeAPI` | JWT issuer claim |
| `JWT_AUDIENCE` | backend | No | `NaderGorgeClients` | JWT audience claim |
| `JWT_EXPIRY_MINUTES` | backend | No | `60` | Access token lifetime |
| `JWT_REFRESH_DAYS` | backend | No | `30` | Refresh token lifetime |
| `MAX_DEVICES_PER_STUDENT` | backend | No | `2` | Device limit config |
| `EVOLUTION_API_BASE_URL` | backend | No | *(optional)* | WhatsApp Evolution API base URL |
| `EVOLUTION_API_KEY` | backend | No | *(optional)* | WhatsApp Evolution API key |
| `EVOLUTION_API_INSTANCE` | backend | No | `Nader` | WhatsApp instance name |
| `GEMINI_API_KEY` | worker | Yes | *(must set)* | Google Gemini API key |
| `API_CALLBACK_SECRET` | worker, backend | Yes | `secretxyz` | Shared secret for worker→backend callbacks |
| `NEXT_PUBLIC_API_URL` | frontend | Yes | `http://backend:5245/api` | Public API URL (internal Docker DNS) |
| `NEXT_PUBLIC_BACKEND_URL` | frontend | Yes | `http://backend:5245` | Backend base URL |
| `ASPNETCORE_ENVIRONMENT` | backend | No | `Docker` | ASP.NET Core environment name |
| `ASPNETCORE_URLS` | backend | No | `http://+:5245` | Backend listen address |
| `CORS_ALLOWED_ORIGINS` | backend | No | `http://localhost:8738` | CORS allowed frontend origin |

---

## Service Configuration Mapping

### `db` (PostgreSQL)
```
POSTGRES_USER=${POSTGRES_USER}
POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
POSTGRES_DB=${POSTGRES_DB}
```

### `redis` (Redis)
No custom environment variables (uses defaults).

### `backend` (.NET ASP.NET Core)
EF Core connection string assembled from parts:
```
ConnectionStrings__DefaultConnection=Host=${POSTGRES_HOST};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
Redis__ConnectionString=${REDIS_HOST}:${REDIS_PORT}
JwtSettings__Secret=${JWT_SECRET}
JwtSettings__Issuer=${JWT_ISSUER}
JwtSettings__Audience=${JWT_AUDIENCE}
JwtSettings__ExpirationMinutes=${JWT_EXPIRY_MINUTES}
JwtSettings__RefreshExpirationDays=${JWT_REFRESH_DAYS}
DeviceLimits__MaxDevicesPerStudent=${MAX_DEVICES_PER_STUDENT}
EvolutionApi__BaseUrl=${EVOLUTION_API_BASE_URL}
EvolutionApi__ApiKey=${EVOLUTION_API_KEY}
EvolutionApi__InstanceName=${EVOLUTION_API_INSTANCE}
Cors__AllowedOrigins=${CORS_ALLOWED_ORIGINS}
ApiCallbackSecret=${API_CALLBACK_SECRET}
ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT}
ASPNETCORE_URLS=${ASPNETCORE_URLS}
```

### `worker` (Node.js BullMQ)
```
REDIS_URL=redis://${REDIS_HOST}:${REDIS_PORT}
DB_CONNECTION_STRING=Host=${POSTGRES_HOST};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
GEMINI_API_KEY=${GEMINI_API_KEY}
API_CALLBACK_SECRET=${API_CALLBACK_SECRET}
```

### `frontend` (Next.js)
```
NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}
NEXT_PUBLIC_BACKEND_URL=${NEXT_PUBLIC_BACKEND_URL}
```

---

## Named Volumes

| Volume Name | Used By | Purpose |
|-------------|---------|---------|
| `pgdata` | db | PostgreSQL data persistence |
| `redisdata` | redis | Redis AOF/RDB persistence |

---

## Docker Networks

| Network | Type | Services |
|---------|------|---------|
| `nadergorge_net` | bridge (default) | All 5 services |

All services communicate over this internal network using Docker service DNS names (`db`, `redis`, `backend`, `worker`, `frontend`). Only ports that need external access are published to the host.

---

## Port Mapping (Host → Container)

| Service | Host Port | Container Port | Notes |
|---------|-----------|----------------|-------|
| frontend | 8738 | 8738 | Matches existing `npm run dev` port |
| backend | 5245 | 5245 | Matches existing `make dev` backend port |
| worker (Bull Board) | 3001 | 3001 | BullMQ dashboard UI |
| db | 5432 | 5432 | Exposed for host-side tools (DBeaver, etc.) |
| redis | 6379 | 6379 | Exposed for host-side tools (RedisInsight, etc.) |

---

## Dependency Graph

```
frontend
  └── depends_on: backend (service_healthy)

backend
  └── depends_on: db (service_healthy), redis (service_healthy)

worker
  └── depends_on: db (service_healthy), redis (service_healthy)

migrator (ephemeral)
  └── depends_on: db (service_healthy)

db, redis
  └── (no dependencies — base services)
```
