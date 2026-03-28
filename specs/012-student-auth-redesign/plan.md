# Implementation Plan: Student Auth Redesign

**Branch**: `012-student-auth-redesign` | **Date**: 2026-03-26 | **Spec**: [specs/012-student-auth-redesign/spec.md](spec.md)
**Input**: Feature specification from `/specs/012-student-auth-redesign/spec.md`

## Summary

Redesign the student registration and login pages to use a premium, single-page Stitch UI template adapted for Dark Mode (matching the admin aesthetic). Replace the multi-step "code entry" registration process with a single comprehensive form to streamline onboarding.

## Technical Context

**Language/Version**: TypeScript (Next.js 15+ Frontend) / C# (.NET 8 Backend)
**Primary Dependencies**: React Hook Form, Zod, Axios, MediatR, Entity Framework Core
**Storage**: PostgreSQL (existing user & profile tables)
**Testing**: Playwright (E2E)
**Target Platform**: Web Application
**Performance Goals**: Client-side validation prevents unnecessary network requests; fast rendering of the login/register static shell.
**Constraints**: Must match the exact aesthetic of the provided Stitch template ("Light Royal Version" structure transposed onto Dark Mode tokens).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: PASS - Auth components will be isolated in frontend `features/auth` or `app/auth`.
- **Two-Step Registration**: **FAIL/VIOLATION** - Principle VI dictates a two-step flow. The user explicitly requested an override to place all fields on one page and remove the code prerequisite. This will be justified in the Complexity Tracking.
- **Premium Editorial Design System**: PASS - Adhering strictly to `--admin-bg` and glassmorphism styling as requested.

## Project Structure

### Documentation (this feature)

```text
specs/012-student-auth-redesign/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── api-contracts.md
└── tasks.md             # Phase 2 output (generated next)
```

### Source Code (repository root)

```text
frontend/
├── src/
│   ├── app/
│   │   ├── login/
│   │   │   └── page.tsx
│   │   └── register/
│   │       └── page.tsx
│   ├── components/
│   │   └── auth/          # Reusable UI components
│   └── services/
│       └── auth-service.ts
backend/
├── src/NaderGorge.Application/
│   └── Features/Auth/Commands/
│       └── RegisterStudentCommand.cs
└── src/NaderGorge.API/
    └── Controllers/
        └── AuthController.cs
```

**Structure Decision**: Web application layout modifying the existing `frontend/src/app/(login|register)` routes and updating the backend Auth commands to accept flat payload.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Principle VI: Two-Step Registration & UX Simplicity | The stakeholder explicitly requested removing the continuation/"code" step and consolidating registration to a single page. | Sticking to the two-step flow was rejected due to direct stakeholder override. To maintain simplicity, we will ensure the single form is highly organized and validated. |
