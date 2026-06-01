# Research: Full Docker Setup

**Feature**: `064-full-docker-setup`
**Date**: 2026-04-18
**Phase**: 0 — Resolve all unknowns before design

---

## Research Summary

All decisions are fully resolved. No NEEDS CLARIFICATION items remain.

---

## Decision 1: Frontend Docker Strategy — `next start` vs Static Export

**Decision**: Multi-stage build; serve with `node:20-alpine` + `next start -p 8738`.

**Rationale**:
- The frontend uses Next.js App Router with API route handlers (server-side proxy endpoints for Telegram, Google Drive, etc.). A static export (`next export`) would break these routes.
- `next start` runs the Node.js server that supports both SSR and API routes.
- Port `8738` matches the existing `package.json` dev script and `make dev` → no port change surprises.

**Alternatives considered**:
- `nginx` serving a static export — rejected because API routes require a runtime.
- `standalone` output mode (`output: 'standalone'` in `next.config.ts`) — **best practice**, will be used. The standalone output copies only necessary production files, dramatically shrinking the final image.

---

## Decision 2: Backend Docker Strategy — SDK vs Runtime Image

**Decision**: Two-stage Dockerfile: `mcr.microsoft.com/dotnet/sdk:8.0` (build + publish) → `mcr.microsoft.com/dotnet/aspnet:8.0` (runtime only).

**Rationale**:
- The backend targets `net8.0` (confirmed in `NaderGorge.API.csproj`).
- The SDK image is ~800 MB; the ASP.NET runtime image is ~200 MB. Multi-stage reduces final image size by ~75%.
- `dotnet publish -c Release -o /app/publish` produces a self-contained, optimized output folder.

**Alternatives considered**:
- Single-stage with SDK image — rejected (unnecessary ~600 MB overhead in every container).
- Self-contained single binary — rejected (larger binary, complicates EF migration tooling inside container).

---

## Decision 3: Worker Docker Strategy — ffmpeg Installation

**Decision**: `node:20-slim` base image; install `ffmpeg` via `apt-get` in the build stage; copy only `node_modules` + `dist/` to runtime stage using `node:20-slim` again.

**Rationale**:
- The worker uses `fluent-ffmpeg` which requires the `ffmpeg` system binary.
- `node:20-slim` is Debian-based → `apt-get install -y ffmpeg` works cleanly.
- `node:20-alpine` would require `apk add ffmpeg` but the `fluent-ffmpeg` npm package has fewer issues with the Debian ffmpeg binary.
- The worker compiles TypeScript to `dist/` before copying to runtime stage. `node_modules` is fully copied (no dev-dep pruning via `npm ci --omit=dev` in production install stage).

**Alternatives considered**:
- `node:20-alpine` — rejected due to musl libc compatibility uncertainty with some native addons; Debian (`slim`) is safer for `ffmpeg` + `pg` native bindings.
- Pre-built `jrottenberg/ffmpeg` base — rejected (unnecessary complexity; plain `node:20-slim + apt ffmpeg` is standard).

---

## Decision 4: Compose File Location — `docker/` vs Project Root

**Decision**: New `docker-compose.yml` at project root. The existing `docker/docker-compose.yml` is superseded (kept for historical reference only).

**Rationale**:
- `docker compose up` (no `-f` flag) looks for `compose.yml` or `docker-compose.yml` in the current directory. Placing it at root means Makefile targets can use `docker compose` without `-f docker/docker-compose.yml` everywhere.
- Reduces boilerplate in every Makefile target.
- The Telegram Bot API service from the old `docker/docker-compose.yml` is **excluded** — it is not part of the platform's containerized stack for this feature.

**Alternatives considered**:
- Keep it in `docker/` and use `-f docker/docker-compose.yml` — rejected (more verbose, error-prone in Makefile).
- Use both files via `-f docker/docker-compose.yml -f docker-compose.override.yml` — rejected (unnecessary complexity for single-host dev setup).

---

## Decision 5: Environment Variable Strategy — Single Root `.env` vs Per-Service

**Decision**: Single root `.env` file consumed by Docker Compose variable substitution (`${VAR_NAME}` syntax). Each service receives only its relevant variables via the `environment:` block in Compose.

**Rationale**:
- Docker Compose automatically loads `.env` from the same directory as the compose file (project root). Zero extra configuration.
- All secrets (JWT, Gemini API key, DB password, Telegram tokens) are in one place — simple to manage, simple to gitignore.
- Per-service variable scoping is enforced in the Compose file itself (only pass relevant vars to each container).

**Alternatives considered**:
- Per-service `.env` files (`env_file:` directive per service) — rejected (three separate files to manage, harder to document, still need a master reference).
- Docker secrets — rejected (overkill for development/staging; appropriate for production swarm, out of scope for this feature).

---

## Decision 6: EF Core Migration Execution Inside Docker

**Decision**: `make migrate` runs `docker compose exec backend dotnet ef database update --project /src/NaderGorge.Infrastructure --startup-project /src/NaderGorge.API` inside the running backend container.

**Rationale**:
- The backend `Dockerfile` uses a **build-stage image** that retains the .NET SDK and source code for the migration exec target. A separate `migration` service in Compose (using the SDK image + source volume) is cleaner.
- Actually, cleaner solution: a dedicated `migrator` service in Compose that uses `mcr.microsoft.com/dotnet/sdk:8.0`, mounts the backend source and exits after running migrations. This avoids exec-ing into the production runtime container (which has no SDK).
- `depends_on: db: condition: service_healthy` ensures DB is ready before migrations run.

**Alternatives considered**:
- `docker compose exec backend dotnet ef ...` — rejected because the final backend runtime image (`aspnet:8.0`) has no .NET SDK, making `dotnet ef` unavailable.
- Baking migrations into the backend startup — rejected (dangerous for production; Compose spec calls for explicit `make migrate` trigger).

---

## Decision 7: `make migrate-add NAME=...` Inside Docker

**Decision**: `make migrate-add NAME=<name>` starts a short-lived SDK container with the backend source mounted as a volume, runs `dotnet ef migrations add $(NAME) --project ... --startup-project ...`, and removes the container. The generated migration file lands in the host filesystem through the volume mount.

**Rationale**:
- The migration file must land in `backend/src/NaderGorge.Infrastructure/Data/Migrations/` on the **host** so it can be committed to git.
- A volume mount (`-v $(PWD)/backend:/src`) ensures the generated file writes through to the host.
- Using `docker run --rm` (not `compose exec`) with the SDK image avoids needing to keep the SDK in the production backend image.

---

## Decision 8: Healthcheck for Backend

**Decision**: `healthcheck` pings the backend's `/health` endpoint (or `/swagger` in development). Interval: 10s, timeout: 5s, retries: 5, start_period: 30s.

**Rationale**:
- ASP.NET Core apps are added via `builder.Services.AddHealthChecks()` and mapped via `app.MapHealthChecks("/health")`. This endpoint is extremely fast (in-process check).
- `start_period: 30s` gives the .NET app time to JIT compile on first startup without false-negative healthchecks.

---

## Decision 9: Next.js `output: 'standalone'` Configuration

**Decision**: Add `output: 'standalone'` to `next.config.ts`. The Dockerfile then copies only `.next/standalone` + `.next/static` + `public/` to the runtime layer.

**Rationale**:
- Standalone output bundles a minimal Node.js server (`server.js`) with only the production dependencies the Next.js bundler detects as needed — no full `node_modules` in the final image.
- Results in dramatically smaller frontend image (~200–300 MB vs ~800 MB+ with full node_modules).

---

## Decision 10: Makefile Phony Targets and Documentation

**Decision**: All Docker targets are `.PHONY`. A `make help` target is added that `grep`s `##` comments from the Makefile and formats them as a usage guide.

**Rationale**:
- `.PHONY` prevents conflicts with any files named `up`, `down`, etc.
- `make help` via `grep` on `##` comments is a well-established, zero-dependency pattern for self-documenting Makefiles.
- The existing `make dev`, `make frontend`, `make backend`, `make stop` targets are preserved verbatim.

---

## Resolved Unknowns

| # | Unknown | Resolution |
|---|---------|------------|
| 1 | Next.js serving strategy | `next start` with standalone output |
| 2 | Backend image size | Multi-stage SDK→aspnet |
| 3 | ffmpeg in worker | apt-get in node:20-slim |
| 4 | Compose file location | Project root |
| 5 | Env var management | Single root `.env` |
| 6 | EF migration exec | Dedicated `migrator` service with SDK image |
| 7 | `migrate-add` file output | `docker run --rm` with volume mount |
| 8 | Backend healthcheck | `/health` endpoint, start_period=30s |
| 9 | Frontend image size | `output: 'standalone'` in next.config.ts |
| 10 | Makefile documentation | `make help` with `##` grep pattern |
