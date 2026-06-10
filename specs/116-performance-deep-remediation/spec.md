# Feature Specification: 116-performance-deep-remediation

**Feature Branch**: `116-performance-deep-remediation`  
**Created**: 2026-06-11  
**Status**: Draft  
**Input**: User description: "Performance Deep Audit - 2026-06-11 - Resolve all performance and remediation findings listed in docs/performance-deep-audit-2026-06-11.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fast Public Page Loading (Priority: P1)
As a visitor or student visiting public pages (Home, About, FAQ, Login, Register), I want the pages to load instantly (under 250ms TTFB) and have minimal JavaScript payload, so that the initial experience is smooth even on poor mobile networks.

**Why this priority**: Crucial for student acquisition and user experience. Currently, root layout forces dynamic rendering, causing high TTFB.

**Independent Test**: Can be tested by running `npm run build` in the frontend and checking if `/`, `/about`, `/faq`, `/login`, and `/register` routes are marked as Static (`○`) or ISR (`●`) instead of Dynamic (`ƒ`).

**Acceptance Scenarios**:
1. **Given** a user navigates to `/login` or `/about`, **When** the page is requested, **Then** it serves static or cached HTML directly without executing server-side database/header logic for every single hit.
2. **Given** a production build of the frontend, **When** `npm run build` is run, **Then** `/` and other public pages are compiled as static routes.

---

### User Story 2 - Efficient Student Shell & Navigation (Priority: P1)
As a logged-in student navigating the platform, I want the sidebar and page content to load without initiating multiple redundant API calls or blocking the browser main thread with heavy animations, so that transitions feel instant.

**Why this priority**: The student dashboard is the most frequently visited screen. API waterfalls and global animation wrappers make every transition feel laggy.

**Independent Test**: Can be tested by opening the browser Network tab, clicking between student pages, and verifying that only the page-specific API is fetched, and the shell components do not re-fetch balance, notifications, or gamification on every page change.

**Acceptance Scenarios**:
1. **Given** a student is on the dashboard, **When** they click "Packages" or "Teachers", **Then** the sidebar does not re-trigger balance or notifications API requests.
2. **Given** any page change in the student layout, **When** the navigation completes, **Then** there is no global `framer-motion` page transition running on routes that do not need it.

---

### User Story 3 - Rapid Backend Queries (Priority: P1)
As a student viewing my dashboard or progress, or an admin listing code groups, I want data retrieval to be extremely fast (under 250ms) even when there are thousands of records in the database.

**Why this priority**: N+1 queries and full-graph loads slow down backend responses and overload PostgreSQL.

**Independent Test**: Can be verified by seeding thousands of access codes/lessons in the DB and measuring the API latency of `/student/dashboard` and `/admin/code-groups` to ensure they stay under 250ms.

**Acceptance Scenarios**:
1. **Given** a code group with 10,000 access codes, **When** listing code groups as an admin, **Then** the database query runs a count projection instead of loading all 10,000 access code entities into C# memory.
2. **Given** a student dashboard request, **When** the query runs, **Then** it retrieves counts and resume-points in a single query projection with `AsNoTracking`, without N+1 queries for exams or terms.

---

### Edge Cases

- **Surface Detection Failures**: How does the middleware handle surface isolation if the host/domain header is missing? It must default to a safe guest/landing surface and return a clean 404/Not Found response rather than throwing a server exception.
- **Cache Invalidation**: What happens when a student reads a notification or activates a code? The client-side shell cache must be updated or invalidated, ensuring the balance/unread counts are refreshed immediately without waiting for the cache timeout.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Student Flow**: Log in as a student, navigate between dashboard, lessons, and packages, check the browser console and network tab to ensure no redundant `/api/student/shell-bootstrap` calls run on route changes.
- **Manual QA Admin Flow**: Go to the Access Codes page, verify that listing code groups responds instantly and displays the correct count of used vs total codes.
- **Docker Acceptance**: Verify that `docker compose up` starts Nginx, backend, and frontend correctly. Verify that Nginx serves optimized static assets with compression headers.
- **External Dependencies**: Evolution API (for birthday WhatsApp messages) must be accessible if triggered, but should not block the main backend or student experience if offline.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001 (Static Public Routes)**: The root layout `frontend/src/app/layout.tsx` MUST NOT use `force-dynamic`. Public pages (FAQ, Landing, Login, Register) MUST compile as Static or ISR.
- **FR-002 (Surface Isolation)**: Surface detection MUST be moved to a middleware or a proxy-injected header rather than using synchronous `headers()` in the root layout.
- **FR-003 (Server Components)**: Convert at least 5 major non-interactive pages (e.g. read dashboards, lists) from Client Components (`use client`) to Server Components.
- **FR-004 (Template Animation Cleanup)**: Remove the global `framer-motion` wrapper from `frontend/src/app/template.tsx` and replace it with a standard CSS transition or remove it entirely where not required.
- **FR-005 (Shell Bootstrap Endpoint)**: Implement a unified `/api/student/shell-bootstrap` endpoint returning notifications count, balance, gamification, and profile basics in a single request.
- **FR-006 (Optimized C# Queries)**: Rewrite `GetDashboardQuery`, `GetProgressQuery`, `GetMistakesQuery`, `GetLessonDetailQuery`, and `ListCodeGroupsQuery` to use EF projections (`.Select()`), `.AsNoTracking()`, and eliminate N+1 loops.
- **FR-007 (Response Compression & Caching)**: Add Brotli response compression to backend `Program.cs` and output caching for public GET endpoints (settings, teachers, stats).
- **FR-008 (Asset Optimization)**: Compress SVG logos (currently 3.2MB each) to under 50KB and ensure other public assets are optimized.
- **FR-009 (Reduced Font Weights)**: Limit Montserrat and Tajawal loaded weights in root layout to only those used by the design system.
- **FR-010 (Surface E2E Access)**: Enforce domain-level route protection so student domain cannot load admin/teacher paths.

### Key Entities

- **LessonVideo**: Needs to support clean querying without loading full chapter/resource trees unless explicitly requested.
- **AccessCode**: Needs count projection capabilities inside the CodeGroup entity.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Page load time for public routes (TTFB) is under 250ms on local environment.
- **SC-002**: Initial JS bundle chunk size per route is reduced by at least 15% due to dynamic imports and template cleanup.
- **SC-003**: The number of API requests on student layout mount is reduced from 4+ to exactly 1 bootstrap request.
- **SC-004**: `/api/admin/code-groups` responds under 250ms even with 10,000+ access codes present in the database.
- **SC-005**: Static files (SVG logos, icons) are reduced in size by at least 90%.

## Assumptions

- We assume that the Next.js runtime environment has headers forwarded by the proxy (Nginx or Node proxy) to identify the active surface.
- We assume that the PostgreSQL database structure is not changing, and all optimizations can be done via EF Core query changes and index usage.
