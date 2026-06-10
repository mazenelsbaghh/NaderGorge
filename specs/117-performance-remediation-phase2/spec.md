# Feature Specification: Performance Remediation Phase 2

**Feature Branch**: `117-performance-remediation-phase2`  
**Created**: 2026-06-11  
**Status**: Approved  
**Input**: Complete remaining performance audit items (2, 6, 9, 10, 14, 15, 17) from `docs/performance-deep-audit-2026-06-11.md`

## User Scenarios & Testing

### User Story 1 - Backend Query Optimization (Priority: P1)

Fix remaining deep Include chains and missing AsNoTracking in `GetMistakesQuery` and `GetLessonDetailQuery` to reduce response latency and memory usage for student-facing endpoints.

**Why this priority**: These queries serve every student on every page load. Deep Include chains load entire entity graphs into memory, causing high memory usage and slow response times.

**Independent Test**: Run the backend (`dotnet build backend/NaderGorge.sln`). Verify no Include chains exist in GetMistakesQuery for exams and no missing AsNoTracking in GetLessonDetailQuery watch events query.

**Acceptance Scenarios**:

1. **Given** a student with exam history, **When** requesting `/api/student/mistakes`, **Then** the query uses SQL projections instead of deep Include chains for exams/questions/options, and response time is under 300ms.
2. **Given** a student viewing a lesson, **When** requesting `/api/content/lessons/{id}`, **Then** the VideoWatchEvents query uses `AsNoTracking()` and the lesson detail query uses projections where possible.

---

### User Story 2 - Hero Image Optimization (Priority: P1)

Optimize landing page hero images from ~1.4MB each to under 500KB using modern image formats.

**Why this priority**: Hero images are loaded on the public landing page — the first thing every visitor sees. 1.4MB images significantly increase load time.

**Independent Test**: Check `frontend/public/images/landing-hero.png` and `landing-hero-dark.png` are replaced with optimized WebP variants under 500KB each.

**Acceptance Scenarios**:

1. **Given** the landing page, **When** a visitor loads it, **Then** hero images are served as optimized WebP files under 500KB each.

---

### User Story 3 - Worker Source Maps Cleanup (Priority: P2)

Remove `.d.ts.map` files from `worker/src/` and configure `tsconfig.json` to output generated files to `dist/` instead.

**Why this priority**: Source maps polluting `src/` add noise to the repository and affect scanning tools. Quick fix.

**Independent Test**: Verify no `.d.ts.map` files exist in `worker/src/` after the fix.

**Acceptance Scenarios**:

1. **Given** the worker project, **When** building with `tsc`, **Then** no `.d.ts.map` files are generated in `worker/src/`. Generated files go to `dist/`.

---

### User Story 4 - Nginx Cache Headers (Priority: P2)

Add proper cache headers for static assets in the Nginx configuration to improve client-side caching.

**Why this priority**: Without cache headers, browsers re-download static assets (JS, CSS, images) on every visit. Adding immutable cache for `_next/static/*` and long cache for images reduces repeat load times dramatically.

**Independent Test**: Verify `docker/nginx/massar.conf` contains cache rules for `_next/static/*` and image assets.

**Acceptance Scenarios**:

1. **Given** a returning visitor, **When** loading any page, **Then** `_next/static/*` assets are served with `Cache-Control: public, max-age=31536000, immutable`.
2. **Given** image assets, **When** served via Nginx, **Then** they have `Cache-Control: public, max-age=604800` (7 days).

---

### User Story 5 - Performance Budget Script (Priority: P2)

Create a local performance budget script that validates SVG/image sizes, checks for force-dynamic in root layout, and reports static vs dynamic route counts.

**Why this priority**: Without automated checks, performance regressions will silently return. A budget script acts as a guardrail.

**Independent Test**: Run `node scripts/perf-budget.mjs` and verify it produces a pass/fail report.

**Acceptance Scenarios**:

1. **Given** the frontend project, **When** running `node scripts/perf-budget.mjs`, **Then** it checks logo SVG sizes (< 50KB), hero image sizes (< 500KB), and detects force-dynamic in root layout.
2. **Given** a build output, **When** running the script, **Then** it reports the count of static vs dynamic routes.

---

### Edge Cases

- GetMistakesQuery: student with no exam attempts should return empty list without errors.
- GetLessonDetailQuery: lesson with no videos or no chapters should still return valid response.
- Hero images: landing page should gracefully handle missing WebP support via fallback.
- Nginx config: API routes must NOT be cached.
- Performance budget: script should exit with non-zero code on failures for CI integration.

### Manual QA & Docker Acceptance

- **Manual QA Flow 1**: Student loads lesson detail page — verify data loads correctly and no N+1 queries.
- **Manual QA Flow 2**: Visit landing page — verify hero images load quickly and look correct.
- **Docker Acceptance**: `docker compose config -q` passes. Nginx config validates.

## Requirements

### Functional Requirements

- **FR-001**: `GetMistakesQuery` MUST use SQL projections instead of deep Include chains for exam/question/option data.
- **FR-002**: `GetLessonDetailQuery` MUST use `AsNoTracking()` on all read queries including VideoWatchEvents.
- **FR-003**: Hero images MUST be converted to WebP format and be under 500KB each.
- **FR-004**: Worker `tsconfig.json` MUST output generated declaration maps to `dist/` not `src/`.
- **FR-005**: Nginx configuration MUST include immutable cache headers for `_next/static/*`.
- **FR-006**: A performance budget script MUST validate asset sizes and route types.

## Success Criteria

### Measurable Outcomes

- **SC-001**: `GetMistakesQuery` contains zero `Include()` chains — uses only projections.
- **SC-002**: All read queries in `GetLessonDetailQuery` use `AsNoTracking()`.
- **SC-003**: Hero images are under 500KB each (down from 1.4MB).
- **SC-004**: Zero `.d.ts.map` files exist in `worker/src/`.
- **SC-005**: Performance budget script runs and passes with zero violations.
- **SC-006**: Backend builds with zero warnings. Frontend builds successfully.

## Assumptions

- The existing WebP support in browsers is sufficient (97%+ global support).
- Nginx configuration lives in `docker/nginx/massar.conf`.
- Worker project uses TypeScript with a `tsconfig.json` that can be modified.
- Performance budget script is for local/CI use, not production runtime.
