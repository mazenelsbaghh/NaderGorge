# Implementation Plan: 011-admin-student-profile

**Branch**: `011-admin-student-profile` | **Date**: 2026-03-26 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/011-admin-student-profile/spec.md`

## Summary

Implement a comprehensive 360-degree Student Profile Details view accessible by administrators enabling the viewing of extensive academic, gamification, financial, and session-history details for any single student via the Admin Dashboard. The system aggregates backend profile capabilities using ASP.NET Core MediatR queries and records granular Admin actions inside an Audit Log schema. The frontend is built with Next.js App Router seamlessly incorporating the established "Editorial Scholar" Shared Component Library (AdminShellChrome, AdminDataTable) to enforce strict UI standards and Gamification-enabled feature overrides.

## Technical Context

**Language/Version**: C# 12/.NET 8, TypeScript 5, React/Next.js (App Router)  
**Primary Dependencies**: Next.js, MediatR, Entity Framework Core, TailwindCSS, Framer Motion  
**Storage**: PostgreSQL (Entity Framework Core)  
**Testing**: Playwright (Frontend E2E), xUnit (Backend)  
**Target Platform**: Web (Desktop optimized primarily for Admin use)  
**Project Type**: Web Application consisting of ASP.NET Core API and full-stack Next.js frontend  
**Performance Goals**: <1.5s Load time for Profile resolution  
**Constraints**: Fully utilize `var(--admin-*)` theme tokens, no manual DOM manipulation outside React.  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Design System strictly enforced (No direct pixel hex colors, adhere to `var(--admin-*)`).
- [x] Minimal DOM layout constraints (no 1px borders, "Ghost Border" Fallback only 
- [x] Backend CQRS patterns utilizing MediatR strictly followed for new Commands/Queries.

## Project Structure

### Documentation (this feature)

```text
specs/011-admin-student-profile/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.API/
│   │   └── Controllers/AdminController.cs
│   ├── NaderGorge.Application/
│   │   └── Admin/
│   │       ├── Queries/GetStudentProfileDetailQuery.cs
│   │       ├── Commands/OverrideVideoLimitCommand.cs
│   │       ├── Commands/AdjustGamificationPointsCommand.cs
│   │       └── Commands/ToggleStudentSystemAccessCommand.cs
│   └── NaderGorge.Domain/
│       └── Entities/AdminAuditLog.cs
└── tests/

frontend/
├── src/
│   ├── app/
│   │   └── admin/
│   │       └── users/
│   │           └── [id]/
│   │               └── page.tsx
│   └── services/
│       └── admin-service.ts
└── tests/
```

**Structure Decision**: A dedicated dynamic route `/admin/users/[id]` utilizing the shared internal layout component structure without side-effects logic leaking outside the page. Backend operates natively via the established application structures.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (No Violations Found) | - | - |
