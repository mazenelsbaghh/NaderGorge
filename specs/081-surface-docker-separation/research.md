# Research: Surface Docker Separation and Massar Platform Rename

## Decision: Runtime surface separation uses one frontend image with three containers

**Rationale**: The current Next.js app already contains landing, student, and admin routes in one App Router tree. Splitting the source into three projects would duplicate shared services, auth stores, video proxies, and design tokens. Running three containers from the same image gives the requested Docker/port/log/health separation while preserving the current architecture.

**Alternatives considered**:
- Three separate frontend projects: rejected because it creates high duplication and would require large routing/auth refactors.
- One frontend container with reverse-proxy path routing: rejected because it does not satisfy separate ports and independent container logs.

## Decision: Use browser public API URL plus internal server API URL

**Rationale**: Browser bundles cannot call `http://backend:5245` because that hostname only exists inside Docker. Next.js server routes and server components should use Docker DNS, while client-side Axios should use a browser-reachable backend origin.

**Alternatives considered**:
- Keep `NEXT_PUBLIC_API_URL=http://backend:5245/api`: rejected because browser requests fail outside Docker network.
- Proxy all browser API calls through Next.js: deferred because it changes every API call path and adds runtime load to frontend containers.

## Decision: Route boundaries live in `frontend/src/proxy.ts`

**Rationale**: The project already has a Next.js proxy for subdomain routing. Extending it with `APP_SURFACE` centralizes route boundaries and avoids scattering redirects across pages, guards, and layouts.

**Alternatives considered**:
- Per-page redirects in `/page.tsx`: rejected because it duplicates logic and cannot catch wrong-surface deep links consistently.
- External nginx only: rejected because local Docker verification should work without an additional reverse proxy.

## Decision: Massar rename is user-visible and operational, not namespace-wide

**Rationale**: C# namespaces, solution names, migration names, and historical internal paths are deeply coupled and not user-facing. Renaming them would be risky and unrelated to Docker separation. Docker service/container names, visible UI copy, metadata, and docs changed by this feature will use Massar.

**Alternatives considered**:
- Rename all namespaces and solution files: rejected because it is high-risk, migration-heavy, and outside the requested runtime separation.

## Decision: Verification includes static Compose checks and optional runtime checks

**Rationale**: Static checks catch duplicate ports, missing health checks, and wrong service names quickly. Runtime checks confirm actual HTTP behavior when containers are running.

**Alternatives considered**:
- Runtime-only checks: rejected because they require a full stack and secrets.
- Static-only checks: rejected because they cannot prove route rewrites and health endpoints respond.
