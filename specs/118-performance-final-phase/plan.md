# Technical Plan: 118-performance-final-phase

## Approach

### US1: Page Refactoring (27 pages → Server Components)

**Pattern**: For each page.tsx with `"use client"`:
1. Rename the existing export function to `*PageClient`
2. Move it to a new file at the same level: `*PageClient.tsx` with `"use client"`  
3. Make `page.tsx` a Server Component that just imports and renders the client component

**Benefits**: Even though the client component still ships JS, the page.tsx itself becomes a Server Component which enables:
- Next.js route-level code splitting
- SSR streaming (the shell renders server-side, client component hydrates)
- Build-time static analysis for the router

### US2: Bundle Optimization

framer-motion appears in 16 files. After US1, all framer-motion usage will be in `*PageClient.tsx` files which are naturally code-split by Next.js. No additional dynamic imports needed — the Server Component wrapper already achieves this.

### US3: Docker Startup Validation

Add a `docker-entrypoint.sh` wrapper that checks `APP_SURFACE` and `NEXT_PUBLIC_APP_SURFACE` before starting the Next.js server.

### US4: Route Surface Middleware

Create `frontend/src/middleware.ts` that reads `NEXT_PUBLIC_APP_SURFACE` and blocks cross-surface navigation.

## Verification

```bash
cd frontend && npm run build
grep -rl '"use client"' frontend/src/app --include="page.tsx" | wc -l  # should be 0
docker compose config -q
```
