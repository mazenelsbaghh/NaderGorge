# Implementation Plan: Docker/Domain Isolation and Role Permissions

**Branch**: `113-docker-domain-isolation` | **Date**: 2026-06-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/113-docker-domain-isolation-role-permissions/spec.md`

## Summary

Decouple each of the 5 web surfaces (Landing, Student, Teacher, Assistant, Admin) into dedicated Docker configurations, update the environment/host configurations to target the new `massar-academy.net` domain, clean up legacy domains from Nginx and CORS AllowedOrigins, and implement comprehensive E2E tests for cross-surface blocking, assistant permissions, and teacher bindings.

## Technical Context

**Language/Version**: C# 13 (.NET 9) Backend, TypeScript 5.x / Next.js 16.2.1 / React 19 Frontend  
**Primary Dependencies**: Next.js App Router, Axios, Zustand, Docker Compose, Playwright  
**Storage**: PostgreSQL (LessonVideo DB, StudentProfile, Role, Permissions)  
**Testing**: Playwright (`npx playwright test`), `scripts/verify-surface-separation.mjs`  
**Target Platform**: Docker-compose standalone services on `massar-academy.net`  
**Project Type**: Multi-subdomain Web Application  
**Performance Goals**: Instant cross-surface boundary blocks, decoupled container builds  
**Constraints**: Zero cross-subdomain session/CORS leakage, secure routing  
**Scale/Scope**: 5 isolated subdomains, full role separation  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Docker configurations (`docker-compose.yml`), frontend host environment configurations, Nginx routing configs, backend `appsettings.json` CORS allowed origins, E2E tests, and boundary check scripts.
- **Automated tests**: Extended Playwright tests mapping out students, teachers, assistants, and supervisors, plus `scripts/verify-surface-separation.mjs` containing subdomain validations.
- **Manual QA**: Verifying that each surface responds on the designated subdomain/port and returns a custom 404 page for wrong surfaces.
- **Docker gate**: Running `docker compose config -q` and starting the isolated services successfully.

## Project Structure

### Documentation (this feature)

```text
specs/113-docker-domain-isolation-role-permissions/
├── plan.md              # This file
├── research.md          # Research findings
├── data-model.md        # DB impacts (none)
├── quickstart.md        # Developer setup guide
└── tasks.md             # Task checklist (created in Phase 3)
```

### Source Code (repository root)

```text
backend/
├── src/
│   └── NaderGorge.API/
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── appsettings.E2e.json
frontend/
├── src/
│   ├── app/
│   │   └── not-found.tsx
│   ├── packages/surface-runtime/
│   │   └── config.ts
│   └── proxy.ts
├── tests/
│   └── e2e/
│       ├── admin-content.spec.ts
│       ├── admin-users.spec.ts
│       ├── assistant-dashboard.spec.ts
│       ├── auth.spec.ts
│       ├── codes-wallet.spec.ts
│       ├── codes.spec.ts
│       ├── package-code-profiles.spec.ts
│       ├── parent-report.spec.ts
│       ├── student-academic.spec.ts
│       └── student-journey.spec.ts
scripts/
└── verify-surface-separation.mjs
docker-compose.yml
```

**Structure Decision**: Web application with separate `frontend` and `backend` projects integrated via Docker Compose.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- E2E Playwright verification: `npx playwright test`
- Next.js build validation: `npm run build` inside `frontend/`
- Next.js lint validation: `npm run lint` inside `frontend/`
- Boundary separation verification: `node scripts/verify-surface-separation.mjs --static-only`

**Docker Gate Required**:
- Run `docker compose config -q` to verify container environments.
- Confirm Nginx / CORS AllowOrigins configuration correctness.

**Manual QA Required**:
- Access all subdomains: `app.massar-academy.net`, `teacher.massar-academy.net`, `staff.massar-academy.net`, `admin.massar-academy.net`
- Verify that attempting to load routes belonging to a different surface on a subdomain renders a custom 404 page.

**End-of-Phase Report Format**:
- Summary of docker configuration separation.
- Details of Nginx / CORS cleanup.
- Playwright E2E and surface validation script results.
- Go/No-go recommendation for production deployment.

## Complexity Tracking

*No violations to document.*
