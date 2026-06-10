# Research and Technical Decisions: 116-performance-deep-remediation

This document captures the analysis, rationale, and alternatives considered for each performance issue identified in the audit.

## 1. Root Layout Dynamic Rendering
- **Decision**: Remove `export const dynamic = 'force-dynamic'` and `headers()` from `frontend/src/app/layout.tsx`. Introduce a lightweight head script that sets the `data-massar-surface` attribute on the client side using `window.location.host` (if `process.env.APP_SURFACE` is not set).
- **Rationale**: Calling `headers()` in the root layout makes every route in the Next.js App Router dynamic by default. Removing it allows public pages to compile as static pages, drastically improving Time to First Byte (TTFB).
- **Alternatives Considered**: Keeping it dynamic but implementing custom caching at the Nginx level. Rejected because native static optimization in Next.js is cleaner and reduces node server CPU utilization.

## 2. Server Components vs Client Components
- **Decision**: Audit all `page.tsx` files. Remove `"use client"` from list/detail/dashboard pages that are primarily read-heavy. Extract interactive widgets (e.g. modals, buttons, forms) into client components.
- **Rationale**: Converting pages to Server Components reduces the hydration cost and the amount of JS sent to the client.
- **Example Targets**: 
  - `frontend/src/app/student/page.tsx` (Student dashboard)
  - Public pages like `/about`, `/faq`

## 3. Template Navigation Animation Wrapper
- **Decision**: Replace `framer-motion` in `frontend/src/app/template.tsx` with a simple CSS transition, or remove it entirely if not needed.
- **Rationale**: Currently, every page navigation transitions using a client-side `motion.div`, loading `framer-motion` globally. Replacing it with a CSS-based transition avoids loading the heavy motion library on pages that don't need it.

## 4. Student Shell API Waterfall
- **Decision**: Merge `balance`, `notifications count`, and `gamification summary` into a single bootstrap endpoint `/api/student/shell-bootstrap`. Add a 30-60s client-side cache and avoid re-fetching on page navigation.
- **Rationale**: Reduces 4-5 API requests on initial layout mount to a single request. Prevents route change event listeners from triggering duplicate network requests.

## 5. Optimized C# Database Queries (N+1 and AsNoTracking)
- **Decision**:
  - Add `AsNoTracking()` to all read-only EF Core queries.
  - Rewrite `GetDashboardQuery.cs` to project to DTO directly using `.Select()` instead of full entity includes.
  - Implement bulk retrieval of upcoming exams instead of a loop query.
  - Rewrite `ListCodeGroupsQuery.cs` to project counts (`cg.AccessCodes.Count()`) instead of loading all `AccessCodes` collections.
- **Rationale**: Drastically reduces SQL execution time and memory allocations in .NET.

## 6. Response Compression & Caching
- **Decision**: Add `UseResponseCompression` (Brotli/Gzip) in backend `Program.cs`. Enable OutputCaching for public read endpoints (`/api/public/settings`, `/api/public/teachers`).
- **Rationale**: Compressing JSON payloads saves bandwidth and TTFB. Caching public endpoints avoids hits to the database.

## 7. Heavy Asset and Font Optimizations
- **Decision**:
  - Optimize the 3.2MB logo SVG files by stripping embedded raster images/metadata.
  - Reduce Tajawal and Montserrat font weights loaded in root layout to the minimum required.
- **Rationale**: Direct page weight reduction from ~5MB to <1MB.
