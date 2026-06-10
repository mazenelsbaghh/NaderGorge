# Feature Specification: Performance Final Phase

**Feature Branch**: `118-performance-final-phase`  
**Created**: 2026-06-11  
**Status**: Approved  
**Input**: Complete final 4 audit items (#2, #9, #12, #13) from `docs/performance-deep-audit-2026-06-11.md`

## User Scenarios & Testing

### User Story 1 - Refactor Pages from Client to Server Components (Priority: P1)

27 page.tsx files use `"use client"` unnecessarily. The pattern is: page.tsx imports hooks (useState, useEffect) and does client-side data fetching. The refactoring approach is to extract the hook-heavy body into a `*PageClient.tsx` component and make the page.tsx a thin Server Component wrapper that imports the client component.

This removes `"use client"` from page-level files, enabling Next.js to:
- Static-generate pages where possible
- Stream SSR for dynamic pages
- Reduce the JS bundle sent to the client

**Why this priority**: This is the single highest-impact remaining item. Client Components prevent SSR streaming and force full JS hydration.

**Independent Test**: `npm run build` shows more static routes (○) and fewer dynamic routes (λ). All 27 converted pages still render correctly.

**Acceptance Scenarios**:

1. **Given** a page.tsx that uses `"use client"`, **When** refactored, **Then** page.tsx is a Server Component that imports a `*PageClient.tsx` client component.
2. **Given** the refactored pages, **When** building the frontend, **Then** the build succeeds with zero errors.
3. **Given** student/teacher pages, **When** navigating them, **Then** all features work identically.

---

### User Story 2 - Bundle Analysis & Heavy Library Audit (Priority: P2)

Identify and optimize heavy client-side libraries (framer-motion, react-quill-new) that are imported directly in pages instead of being dynamically imported.

**Why this priority**: Heavy libraries loaded eagerly increase initial bundle size and block interactivity.

**Independent Test**: Run `npx @next/bundle-analyzer` or check that framer-motion is only imported via `next/dynamic` in page-level components.

**Acceptance Scenarios**:

1. **Given** pages that import `framer-motion` directly, **When** refactored, **Then** motion components are dynamically imported or moved to client sub-components that are naturally code-split.
2. **Given** the frontend bundle, **When** analyzed, **Then** framer-motion does not appear in the shared/common chunk.

---

### User Story 3 - Docker Surface Isolation Decision (Priority: P2)

The 5 frontend surfaces (landing, student, admin, teacher, assistant) currently share one Docker image (`massar_frontend:local`) with `APP_SURFACE` set at runtime. Document the decision on whether to:
a) Keep the shared image (current approach) with a validation guard, or
b) Build separate per-surface images.

**Why this priority**: This is an architectural decision that prevents misconfiguration in production.

**Independent Test**: A startup validation script or environment variable check exists that prevents running without `APP_SURFACE`.

**Acceptance Scenarios**:

1. **Given** the shared Docker image, **When** started without `APP_SURFACE`, **Then** the container fails immediately with a clear error.
2. **Given** docker-compose.yml, **When** validated, **Then** all 5 frontend services have `APP_SURFACE` set.

---

### User Story 4 - Route Protection E2E Rules (Priority: P3)

Add middleware or route-level protection rules to prevent cross-surface navigation and enforce surface boundaries.

**Why this priority**: Without route rules, a student could accidentally access admin routes via direct URL, causing confusing errors.

**Independent Test**: middleware.ts correctly redirects unauthorized cross-surface access.

**Acceptance Scenarios**:

1. **Given** a student user, **When** navigating to `/admin/*`, **Then** they are redirected to the student dashboard.
2. **Given** middleware.ts, **When** reviewed, **Then** it enforces surface boundaries based on the active surface.

---

### Edge Cases

- Pages that use `useParams()` or `useSearchParams()` may need special handling (these hooks only work in Client Components).
- The `balance/page.tsx` is the only page that composes only components (no hooks) — easiest conversion.
- Docker without `APP_SURFACE` should fail fast, not silently serve wrong content.

### Manual QA & Docker Acceptance

- **Manual QA Flow 1**: Navigate all student pages after refactoring — verify no regressions.
- **Manual QA Flow 2**: Navigate all teacher pages — verify no regressions.
- **Docker Acceptance**: `docker compose config -q` passes. Each service has `APP_SURFACE`.

## Requirements

### Functional Requirements

- **FR-001**: All 27 page.tsx files with `"use client"` MUST be refactored to Server Components with extracted `*PageClient.tsx` client components.
- **FR-002**: Heavy libraries (framer-motion) MUST be imported only in client sub-components, not page.tsx.
- **FR-003**: Docker frontend MUST validate `APP_SURFACE` on startup.
- **FR-004**: Middleware MUST prevent cross-surface navigation.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Zero page.tsx files contain `"use client"` after refactoring.
- **SC-002**: Frontend builds successfully with zero errors.
- **SC-003**: framer-motion does not appear in shared chunks.
- **SC-004**: Docker container fails to start without `APP_SURFACE`.
- **SC-005**: Cross-surface navigation is blocked by middleware.

## Assumptions

- The existing component structure allows extracting page bodies into client components without major restructuring.
- Server Components in Next.js 16.2.1 support the current data patterns.
- The Docker entrypoint can be extended with an env var check.
- Next.js middleware can read the surface context to enforce boundaries.
