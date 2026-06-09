# Implementation Plan: Phase 1 - Access Model, Staff Surfaces, and Permission Boundaries

**Branch**: `089-access-model-permissions-boundaries` | **Date**: 2026-06-07 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/089-access-model-permissions-boundaries/spec.md)
**Input**: Feature specification from `/specs/089-access-model-permissions-boundaries/spec.md`

## Summary

This phase configures the platform permissions framework and seeds the operational role values (`Supervisor` and `Staff`) ahead of the HR, CRM, and Financial modules implementation. 

We will:
1. Extend the C# `RoleType` enum and seeder to support new system roles.
2. Expose the new fine-grained permission keys (`hr.manage`, `crm.manage`, `finance.manage`, etc.) in the frontend settings view.
3. Include the permission list in the backend login and refresh DTO response, persisting it in the frontend auth store.
4. Enforce route guarding and conditional sidebar rendering based on these permissions.
5. Create a test migration to ensure Postgres databases are synced.

## Technical Context

- **Language/Version**: .NET 9 (C# 13), Next.js 16.2.1 / React 19 (TypeScript 5.x)
- **Primary Dependencies**: Entity Framework Core 9, MediatR, Zustand
- **Storage**: PostgreSQL 16
- **Testing**: `dotnet test`, frontend linting checks, static boundary verifications
- **Target Platform**: Web application (Admin and Student portals)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer Impact**:
  - **Backend**: Update `RoleType` enum, update `Seeder.cs`, update `UserDto` definitions in MediatR authentication command/query payloads, and register backend controllers with appropriate permission checks.
  - **Frontend**: Update Zustand store, update sidebar packages/menu filtering, update `AdminSettingsPage` permission checklist, and add a custom permission verification hook.
  - **Database**: Add EF Core migration for role inserts.
- **Automated Tests**:
  - Add backend tests validating that custom roles create/update correctly, and that permission checks return correct status codes (e.g. `403 Forbidden`).
- **Manual QA**:
  - Verify that logging in as `Staff` or `Supervisor` with partial permissions restricts sidebar links and routes correctly.
- **Docker Gate**:
  - `docker compose config -q`
  - `make up` (if daemon running)
  - `make migrate`
  - Health check URLs and surface verification.
- **No Next Phase until Verified**:
  - Phase 2 (HR Core) will not start until this authorization framework is validated.

## Project Structure

### Documentation (this feature)

```text
specs/089-access-model-permissions-boundaries/
├── plan.md              # This file
├── spec.md              # Feature Specification
└── checklists/
    └── requirements.md  # Spec validation checklist
```

### Source Code

```text
backend/src/
├── NaderGorge.Domain/
│   └── Enums/
│       └── RoleType.cs                  # Update: Add Supervisor & Staff
├── NaderGorge.Infrastructure/
│   └── Data/
│       └── Seeder.cs                    # Update: Seed new roles
├── NaderGorge.Application/
│   └── Features/Auth/
│       └── Commands/
│           ├── LoginCommand.cs          # Update: return Permissions in UserDto
│           └── RefreshTokenCommand.cs   # Update: return Permissions in UserDto

frontend/src/
├── stores/
│   └── auth-store.ts                    # Update: persist permissions
├── packages/admin/
│   └── navigation.tsx                   # Update: Add optional permission mapping
├── app/admin/
│   ├── layout.tsx                       # Update: filter Sidebar menu items
│   └── settings/
│       └── page.tsx                     # Update: list new permissions
```

**Structure Decision**: The current multi-surface separation architecture will be used. Permission checks will be implemented in the frontend navigation and layout wrappers.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Build check: `dotnet build backend/NaderGorge.sln`
- Backend test run: `dotnet test backend/NaderGorge.sln`
- Frontend build and lint: `npm run lint` and `npm run build` inside `frontend/`
- Boundary audit: `node scripts/verify-surface-separation.mjs --static-only`

**Docker Gate Required**:
- Run `docker compose config -q` to validate the setup.
- Run `make up` and `make migrate` when docker daemon is active.

**Manual QA Required**:
- **Super Admin Flow**: Log in to `/admin/settings` and verify the roles checklist contains the 8 new permissions.
- **Create Staff Role**: Create a role named "مساعد كول سنتر" with `crm.manage` permission. Create a user with this role, log in, and verify that only CRM/Student-related links are visible in the sidebar.
- **Negative Check**: Attempt to navigate to `/admin/settings` as the Call Center user. Verify the page blocks access and redirects/shows unauthorized screen.

**End-of-Phase Report Format**:
- Implemented scope details.
- Compilation status and automated test run results.
- Docker gate log references.
- Risks/TODOs for Phase 2.
