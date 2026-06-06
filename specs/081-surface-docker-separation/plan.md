# Implementation Plan: Surface Docker Separation and Masar Platform Rename

**Branch**: `081-surface-docker-separation` | **Date**: 2026-06-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/081-surface-docker-separation/spec.md`

## Summary

Separate the platform runtime into dedicated Docker surfaces for landing, student, admin, and backend API while keeping the existing Next.js codebase and .NET API architecture intact. The implementation will run three independent frontend containers from the same production image, each with its own surface identity, host port, route boundary, health check, logs, and public origin. Client-side API calls will use a browser-reachable backend URL, while server-side Next.js routes will use Docker internal service DNS. User-visible and operational runtime identity will be updated to "منصة مسار" / "Masar Platform" and `masar_*` container naming.

## Technical Context

**Language/Version**: TypeScript 5.x / Next.js 16.2.x / React 19, C# 13 / .NET 9, Node.js 20, Docker Compose  
**Primary Dependencies**: Next.js App Router proxy, Axios service layer, ASP.NET Core Web API, EF Core, BullMQ worker, PostgreSQL 16, Redis 7  
**Storage**: PostgreSQL via Docker named volume; Redis via Docker named volume  
**Testing**: Static Docker Compose verification, frontend lint/build, backend build/tests, runtime curl checks, optional Playwright smoke checks  
**Target Platform**: Local and VPS Docker runtime, Linux containers, browser clients on distinct local ports  
**Project Type**: Multi-service web platform with one backend API, one worker, and three frontend runtime surfaces  
**Performance Goals**: Surface route rewrite/redirect adds no extra network hop beyond normal Next.js request handling; frontend image is built once and reused by all three frontend services  
**Constraints**: Preserve existing backend namespace and database migrations; do not split the frontend source into separate apps; avoid browser URLs that point to Docker-only hostnames; keep CORS strict to known surface origins  
**Scale/Scope**: Four externally addressable app surfaces plus worker/db/redis support services

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: PASS. Backend layers remain unchanged; Docker/runtime separation does not introduce direct cross-layer access.
- **Provider Abstraction First**: PASS. Video and worker integrations stay behind existing API/proxy boundaries.
- **Security & Access Control**: PASS. Dedicated admin/student surfaces keep auth guards; CORS becomes explicit for all surface origins.
- **Phased Delivery with MVP Discipline**: PASS. Scope is runtime separation and branding; no unrelated domain features.
- **Academic Content Integrity**: PASS. No academic content model changes.
- **Premium Editorial Design System**: PASS. Use existing Cairo/RTL tokens and Masar identity; no new visual pattern conflicts.
- **Multi-Provider Video Architecture**: PASS. Video embed proxy remains available on frontend surfaces and uses internal backend URLs where needed.
- **AI Worker Orchestration**: PASS. Worker remains separate and is only reached through the existing Next.js server-side proxy.

## Project Structure

### Documentation (this feature)

```text
specs/081-surface-docker-separation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── surface-runtime-contract.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
docker-compose.yml                  # separated Masar runtime services and ports
docker-compose.override.yml          # local db/redis overrides aligned with Masar names
Makefile                            # per-surface build/log/shell/verify targets
.env.example                        # documented port and public/internal URL settings

frontend/
├── Dockerfile                       # reusable standalone image for all frontend surfaces
├── src/
│   ├── packages/
│   │   └── surface-runtime/         # feature package: surface config and route rules
│   ├── app/
│   │   ├── layout.tsx               # Masar metadata
│   │   ├── page.tsx                 # server-side internal API URL
│   │   └── api/                     # server-only proxy routes use internal URLs
│   ├── components/                  # existing landing/admin/student components
│   ├── components/shared/           # reusable UI only if a shared surface component becomes necessary
│   └── services/                    # browser API client uses public backend URL
└── tests/
    └── surface-runtime.spec.ts      # route config tests if test harness is available

scripts/
└── verify-surface-separation.mjs    # Docker Compose and optional runtime verification
```

**Structure Decision**: Use runtime separation rather than source-code app splitting. This preserves the current Next.js App Router structure, avoids duplicate frontend builds, and still provides distinct containers, ports, health checks, logs, and route boundaries.

## Phase 0: Research

Research decisions are captured in [research.md](./research.md).

## Phase 1: Design & Contracts

Data model is captured in [data-model.md](./data-model.md). Runtime contract is captured in [contracts/surface-runtime-contract.md](./contracts/surface-runtime-contract.md). Operational quickstart is captured in [quickstart.md](./quickstart.md).

## Implementation Strategy

1. Add `frontend/src/packages/surface-runtime/config.ts` with typed surface names, default ports, public origin environment variables, and route-boundary decisions.
2. Update `frontend/src/proxy.ts` to use the surface runtime package:
   - landing root stays landing
   - student root rewrites to `/student`
   - admin root rewrites to `/admin`
   - wrong-surface `/student` or `/admin` requests redirect to the configured surface origin
   - existing admin subdomain behavior is preserved as the default fallback
3. Update frontend API URL handling:
   - browser code uses `NEXT_PUBLIC_API_URL`, defaulting to a host-reachable backend URL
   - server code uses `INTERNAL_API_URL` / `INTERNAL_BACKEND_URL`, defaulting to Docker DNS
4. Replace user-visible `مسار أكاديمي` and `Massar Academy` copy touched by this feature with `منصة مسار` and `Masar Platform`.
5. Replace root Docker Compose application container/service naming with `masar_*`, add `landing`, `student`, and `admin` services, and publish unique configurable ports.
6. Update Makefile commands for per-surface logs/build/shell and add `verify-surfaces`.
7. Add a static/runtime verification script to validate unique ports, required services, health checks, Masar naming, and optional running HTTP endpoints.

## UI/UX Planning Notes

- **Impeccable product register**: authenticated student/admin surfaces stay task-focused, dense, and familiar. No new decorative UI is introduced.
- **Impeccable brand register**: landing retains existing brand-led visual treatment and imagery; only runtime boundary and Masar identity are adjusted.
- **ui-ux-pro-max checklist applied**: retain strong focus states, WCAG contrast, responsive behavior at 375/768/1024/1440 widths, smooth hover states, and dashboard density.
- Existing Cairo/Tajawal Arabic-first typography is preserved because project context already committed to Arabic-first student workflows. The generic Fira/indigo design-system suggestion is rejected to avoid brand drift.

## Complexity Tracking

No constitution violations. The additional frontend containers are runtime instances of the same image, not new frontend apps.
