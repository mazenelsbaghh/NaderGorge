# Implementation Plan: تحديث نموذج تسجيل الطالب

**Branch**: `016-registration-form-updates` | **Date**: 2026-03-28 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/016-registration-form-updates/spec.md`

## Summary

Update the student registration form to: (1) add a cascading district/neighborhood dropdown dependent on the selected governorate, (2) remove the student code field from registration, (3) allow two phone numbers for the student (primary + optional secondary), and (4) allow two phone numbers for the parent/guardian (primary + optional secondary). Changes span the .NET backend (entity, command, validator, migration, DTOs), the Next.js frontend (form, validation schema, service types), and the admin users view.

## Technical Context

**Language/Version**: C# .NET 8 (Backend), TypeScript/Next.js (Frontend)
**Primary Dependencies**: Entity Framework Core, MediatR, FluentValidation, Zod, Framer Motion
**Storage**: PostgreSQL (Docker-managed, NOT Supabase)
**Testing**: Playwright E2E tests (frontend), no backend unit test runner detected
**Target Platform**: Web (Desktop + Mobile browsers)
**Project Type**: Web application (frontend + backend)
**Performance Goals**: Registration completes < 3 minutes, district list loads < 1 second
**Constraints**: Backward-compatible with existing data (student code stays optional in DB)
**Scale/Scope**: ~4 files backend, ~4 files frontend, 1 EF migration

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Modular Clean Architecture | ✅ PASS | Changes confined to Auth module (command/handler), Domain (entity), Infrastructure (migration/config), Frontend (form component, service) |
| II. Provider Abstraction | ✅ N/A | No external integrations involved |
| III. Security & Access Control | ✅ PASS | All phone numbers validated at both frontend (Zod) and backend (FluentValidation). Registration endpoint already rate-limited |
| IV. Phased Delivery with MVP Discipline | ✅ PASS | This is a Phase 3 registration refinement — aligned with constitution |
| V. Academic Content Integrity | ✅ N/A | No academic content changes |
| VI. Single-Flow Registration & UX Simplicity | ✅ PASS | Maintains single-flow. Enhances data collection (district) and simplifies (removes student code) |
| VII. Observability & Operational Readiness | ✅ PASS | Uses EF Core migration system. No raw SQL. Structured error responses maintained |
| VIII. Premium Editorial Design System | ✅ PASS | Form follows existing auth.css design system tokens. Preview panel updated to match |

**GATE RESULT**: ✅ ALL PASSED — proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/016-registration-form-updates/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── register-api.md
└── checklists/
    └── requirements.md  # Quality checklist
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   └── Entities/
│   │       └── StudentProfile.cs              # Add District, SecondaryPhone, SecondaryParentPhone. StudentCode → optional
│   ├── NaderGorge.Application/
│   │   └── Features/
│   │       ├── Auth/Commands/
│   │       │   └── RegisterCommand.cs         # Update command record, validator, handler
│   │       └── Admin/Queries/
│   │           ├── ListUsersQuery.cs           # Update DTO to include new fields
│   │           └── StudentProfileExtendedDto.cs  # Add new fields
│   └── NaderGorge.Infrastructure/
│       └── Data/
│           └── AppDbContext.cs                 # Update StudentProfile EF config

frontend/
├── src/
│   ├── components/forms/
│   │   └── RegistrationForm.tsx               # Major changes: remove studentCode, add district, dual phones
│   ├── services/
│   │   └── auth-service.ts                    # Update RegisterData interface
│   ├── data/                                  # [NEW] Static governorate-district mapping
│   │   └── governorate-districts.ts
│   └── app/admin/users/
│       └── page.tsx                           # Update to show district instead of studentCode
```

**Structure Decision**: Follows existing web application layout (backend/ + frontend/). New static data file in `frontend/src/data/` for governorate-district mapping. No new modules or architectural changes.

## Complexity Tracking

> No violations to justify — all changes follow existing patterns.
