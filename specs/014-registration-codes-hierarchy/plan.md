# Implementation Plan: Registration, Code System & Content Hierarchy Overhaul

**Branch**: `014-registration-codes-hierarchy` | **Date**: 2026-03-27 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/014-registration-codes-hierarchy/spec.md`

## Summary

Overhaul the student registration flow to collect all required data in a single step, expand the code system from 2 code scopes (package/lesson) to 6 types (term, month, lesson, video, exam, balance/credit) with QR auto-redemption and discount support, and restructure the content hierarchy from `Package > Section > Lesson` to `Package > Term > Section (Month) > Lesson` with direct-access UX for partial purchases.

## Technical Context

**Language/Version**: C# .NET 8 (backend), TypeScript 5.x / Next.js 14 (frontend)
**Primary Dependencies**: Entity Framework Core, React Query, Framer Motion, Zustand
**Storage**: PostgreSQL (Supabase), Redis
**Testing**: xUnit (backend), Jest/React Testing Library (frontend)
**Target Platform**: Web (desktop + mobile browsers)
**Project Type**: Web application (frontend + backend API + worker)
**Performance Goals**: API < 500ms p95, QR redemption < 2s E2E, registration form conditional fields < 500ms
**Constraints**: Single-flow registration, balance >= 0 always, QR auto-redeem + manual entry coexist
**Scale/Scope**: Student base per academic year, 6 code types, 4-level content hierarchy

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Modular Clean Architecture | ✅ PASS | Changes follow existing Domain/Application/Infrastructure/API layers. New entities added to Domain, new services to Application. |
| II | Provider Abstraction First | ✅ PASS | QR generation uses an abstracted service. Code types use a unified CodeEngine interface. |
| III | Security & Access Control | ✅ PASS | Registration validates all fields. Code redemption has rate limiting. Balance cannot go negative. |
| IV | Phased Delivery (MVP) | ✅ PASS | Phase 3 builds on completed Phase 1+2. No future-phase leakage. |
| V | Academic Content Integrity | ✅ PASS | Hierarchy remains: Package > Term > Section > Lesson. Video tags added. |
| VI | Single-Flow Registration & UX | ✅ PASS | Directly implementing this principle. Conditional fields for stage/grade/track. |
| VII | Observability & Ops | ✅ PASS | Audit logs for code modifications/revocations. Structured logging for balance changes. |
| VIII | Premium Design System | ✅ PASS | Registration form and code pages follow Editorial Scholar tokens. |

## Project Structure

### Documentation (this feature)

```text
specs/014-registration-codes-hierarchy/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── registration.md
│   ├── codes.md
│   └── content.md
└── tasks.md             # Phase 2 output (via /speckit.tasks)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   ├── Entities/
│   │   │   ├── StudentProfile.cs          # MODIFY: add 8 new fields
│   │   │   ├── CodeEntities.cs            # MODIFY: add CodeType enum, QR, expiration, discount
│   │   │   ├── ContentEntities.cs         # MODIFY: add Term entity between Package and Section
│   │   │   └── StudentBalance.cs          # NEW: balance/credit entity
│   │   └── Enums/
│   │       ├── EducationStage.cs          # NEW
│   │       ├── GradeLevel.cs              # NEW
│   │       ├── StudyTrack.cs              # NEW
│   │       ├── CodeType.cs                # NEW
│   │       └── Gender.cs                  # NEW
│   ├── NaderGorge.Application/
│   │   ├── Services/
│   │   │   ├── RegistrationService.cs     # MODIFY: single-flow with all fields
│   │   │   ├── CodeService.cs             # MODIFY: 6 code types, QR, discount
│   │   │   └── BalanceService.cs          # NEW: balance operations
│   │   └── DTOs/
│   │       ├── RegisterStudentDto.cs      # MODIFY: all new fields
│   │       ├── CodeGroupDto.cs            # MODIFY: code type, QR, discount
│   │       └── BalanceDto.cs              # NEW
│   ├── NaderGorge.Infrastructure/
│   │   ├── Data/
│   │   │   └── ApplicationDbContext.cs    # MODIFY: new entities + Term
│   │   ├── Migrations/                    # NEW: schema migration
│   │   └── Services/
│   │       └── QrCodeService.cs           # NEW: QR generation
│   └── NaderGorge.API/
│       └── Controllers/
│           ├── AuthController.cs          # MODIFY: registration endpoint
│           ├── CodesController.cs         # MODIFY: new code types + QR
│           ├── BalanceController.cs        # NEW
│           └── ContentController.cs       # MODIFY: term management

frontend/
├── src/
│   ├── app/
│   │   ├── (public)/register/page.tsx     # MODIFY: single-flow registration
│   │   ├── student/
│   │   │   └── page.tsx                   # MODIFY: direct-access shortcuts
│   │   ├── admin/
│   │   │   ├── codes/page.tsx             # MODIFY: 6 code types + QR + discount
│   │   │   ├── students/page.tsx          # MODIFY: new filter columns
│   │   │   └── content/page.tsx           # MODIFY: term management
│   │   └── api/
│   │       └── qr/route.ts               # NEW: QR scan handler
│   ├── components/
│   │   ├── registration/
│   │   │   ├── RegistrationForm.tsx       # MODIFY: all fields + conditional logic
│   │   │   └── AcademicFields.tsx         # NEW: stage/grade/track conditional
│   │   ├── codes/
│   │   │   ├── CodeTypeSelector.tsx       # NEW: 6 code types UI
│   │   │   ├── QrScanner.tsx              # NEW: camera QR scanner
│   │   │   └── QrDisplay.tsx             # NEW: printable QR display
│   │   └── content/
│   │       └── TermManager.tsx            # NEW: term CRUD
│   └── services/
│       ├── registration-service.ts        # MODIFY: new fields
│       ├── code-service.ts                # MODIFY: new types + QR
│       └── balance-service.ts             # NEW
```

**Structure Decision**: Web application pattern (frontend + backend) following existing Clean Architecture layers. All new entities go in Domain, services in Application, DB config in Infrastructure, endpoints in API.

## Complexity Tracking

> No Constitution Check violations. No complexity justifications needed.
