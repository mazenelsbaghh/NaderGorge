# Implementation Plan: Platform Guards, Permissions, and Multi-Domain Routing Isolation

**Branch**: `106-platform-guards-and-permissions` | **Date**: 2026-06-09 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/106-platform-guards-and-permissions/spec.md`

## Summary

This plan outlines the technical changes required to isolate and separate the Nader Gorge / Massar Academy platform into 5 independent frontend surfaces (landing, student, admin, teacher, and assistant). This involves extending the route boundary decision module, creating individual Next.js guards (`TeacherGuard`, `AssistantGuard`, `StaffGuard`, `StudentGuard` checks), locking down permission checks so only the `Admin` role receives an automatic bypass, expanding the Docker Compose configuration and Nginx configuration for 5 frontend services, updating the local surface verification script, and adding E2E validation.

## Technical Context

**Language/Version**: TypeScript 5.x / Next.js 16.2.x / React 19, C# 13 / .NET 9, Docker Compose  
**Primary Dependencies**: Next.js App Router proxy, ASP.NET Core Web API, Nginx  
**Storage**: PostgreSQL (LessonVideo/Users), Redis  
**Testing**: static scripts (`verify-surface-separation.mjs`), E2E smoke tests  
**Target Platform**: Docker Compose local runtime & VPS  
**Project Type**: Multi-surface web platform  
**Performance Goals**: Route evaluation executes instantly on the edge (proxy/middleware layer) without blocking page loads.  
**Constraints**: Keep the Next.js standalone output configuration. Ensure role-based CORS permissions on the backend API are updated for all 5 surface domains.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: PASS. Layout guards are modularized in frontend components; API filters are self-contained in backend attributes.
- **Provider Abstraction First**: PASS. Not applicable here.
- **Security & Access Control**: PASS. Tightening permissions by removing the automatic bypass for Teachers and Supervisors is a major security improvement.
- **Phased Delivery with MVP Discipline**: PASS. Strictly focuses on Phase 1 guards, permissions, routing, and docker domains isolation.
- **Academic Content Integrity**: PASS. No academic model changes.
- **Premium Editorial Design System**: PASS. The access guards will use Cairo/RTL and premium Massar platform colors.

## Project Structure

### Documentation (this feature)

```text
specs/106-platform-guards-and-permissions/
├── plan.md              # This file
├── research.md          # Research findings and decisions
├── data-model.md        # Roles and claims mapping
├── quickstart.md        # Local startup guide
├── contracts/
│   └── surface-runtime-contract.md  # Routing and domains isolation contract
└── checklists/
    └── requirements.md  # Quality checklist
```

### Proposed Changes

#### Frontend Codebase

##### [MODIFY] [config.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/packages/surface-runtime/config.ts)
- Extend `SurfaceName` to include `teacher` and `assistant`.
- Extend `SurfaceOrigins` interface and returned configuration with `teacher` and `assistant` URLs.
- Update `getRouteBoundaryDecision` with the symmetric routing logic for all 5 surfaces.

##### [MODIFY] [proxy.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/proxy.ts)
- Support redirecting subdomains/ports of the new surfaces in `all` mode.

##### [MODIFY] [useHasPermission.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/hooks/useHasPermission.ts)
- Restrict permission bypass exclusively to the `Admin` role. Evaluates roles/claims for all other roles.

##### [MODIFY] [LoginForm.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/forms/LoginForm.tsx)
- Route students to the student domain, teachers to the teacher domain, assistants/staff to the assistant domain, and admins/supervisors to the admin domain.

##### [MODIFY] [AdminGuard.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/AdminGuard.tsx)
- Allow only `Admin` and `Supervisor` (with appropriate permissions) to access the admin surface.

##### [MODIFY] [StudentGuard.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/StudentGuard.tsx)
- Restrict to the `Student` role (allow admin/staff bypass under preview mode where appropriate).

##### [NEW] [TeacherGuard.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/TeacherGuard.tsx)
- Restrict to `Teacher` role. Redirect unauthorized users to login or student domain.

##### [NEW] [AssistantGuard.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/AssistantGuard.tsx)
- Restrict to `Assistant` or `Staff` roles. Redirect unauthorized users.

##### [NEW] [StaffGuard.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/layout/StaffGuard.tsx)
- Restrict to `Staff` role. Redirect unauthorized users.

#### Backend Codebase

##### [MODIFY] [HasPermissionAttribute.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Extensions/HasPermissionAttribute.cs)
- Change permission filter to bypass exclusively for `Admin` role. Evaluate claim check for `Teacher`, `Supervisor`, etc.

##### [MODIFY] [Program.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.API/Program.cs)
- Allow all 5 surface origins in the CORS policy configuration.

#### Operations and Deployment

##### [MODIFY] [docker-compose.yml](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/docker-compose.yml)
- Declare `teacher` and `assistant` frontend services running the standalone production image.
- Set up `APP_SURFACE`/`NEXT_PUBLIC_APP_SURFACE` values for both new services.
- Map local ports `8741` and `8742`.
- Update Nginx `depends_on` list.

##### [MODIFY] [massar.conf](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/docker/nginx/massar.conf)
- Separate virtual hosts for `teacher` subdomains pointing to `http://teacher:8738`.
- Separate virtual hosts for `staff` subdomains pointing to `http://assistant:8738`.
- Keep `admin` and `super` subdomains pointing to `http://admin:8738`.

##### [MODIFY] [verify-surface-separation.mjs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/scripts/verify-surface-separation.mjs)
- Extend tests to verify 5 frontend surfaces, checking redirections and route boundaries.

## Phase Closure & Verification Plan

### Automated Tests Required

- Run `npm test` and lint to ensure no frontend regression.
- Execute backend tests `dotnet test` if any exist in the project structure.
- Execute python e2e tests via `pytest tests/` to verify auth flows still pass.

### Docker Gate Required

- Validate config: `docker compose config -q`
- Run services: `make up` (or `docker compose up -d`)
- Run surface separation verification: `node scripts/verify-surface-separation.mjs`

### Manual QA Required

- Login as Student, attempt to load `http://localhost:8740/admin` (should redirect to student domain).
- Login as Teacher, verify redirect to `http://localhost:8741/teacher`.
- Login as Assistant, verify redirect to `http://localhost:8742/assistant`.
- Login as Admin, verify redirect to `http://localhost:8740/admin`.
