# Implementation Plan: Unified Assessment Builder

**Branch**: `031-unify-assessment-builder` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/031-unify-assessment-builder/spec.md`

## Summary

The Unified Assessment Builder refactors the disparate Homework and Exam creation forms into a single, cohesive `UnifiedAssessmentBuilder.tsx` component. It extends the underlying domain entities (`Homework` and `Exam`) with boolean flags `IsMandatory` and `IsRandomized` via EF Core migrations to enable conditional progression locking and question shuffling across all assessment types.

## Technical Context

**Language/Version**: C# 12 (.NET 8.0) & TypeScript (Next.js 14)
**Primary Dependencies**: Entity Framework Core, React Hook Form, Framer Motion
**Storage**: PostgreSQL (Ef Core Migrations)
**Testing**: Integrated admin panel test course via Swagger E2E
**Target Platform**: Web application (Frontend + Backend API)
**Project Type**: Full Stack Platform (Educational LMS)
**Performance Goals**: UI should render seamlessly; payload serialization handles < 100 questions.
**Constraints**: Backend schema must maintain separate Exam/Homework entities to preserve historical analytics, meaning unification only occurs at the UI presentation level.
**Scale/Scope**: Impacts all created content from now on.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **VI. Single-Flow Registration & UX Simplicity**: The unification reduces cognitive load on admins, aligning with UX simplicity explicitly mandated for users and admins.
- **IX. Assessment & Time Integrity**: The addition of randomness does not tamper with server-side truth enforcement; randomization occurs algorithmically when generating student contexts.
- **V. Academic Content Integrity**: Progression locks via `IsMandatory` give the teacher content authority over flow without stifling students unnecessarily.

**Status**: ALL PASSED. No structural friction found.

## Project Structure

### Documentation (this feature)

```text
specs/031-unify-assessment-builder/
├── plan.md              # This file
├── research.md          # Generated
├── data-model.md        # Generated
├── quickstart.md        # Generated
├── contracts/           # Generated
└── tasks.md             # Empty
```

### Source Code

```text
backend/
├── src/
│   ├── NaderGorge.Domain/
│   │   └── Entities/ (Homework.cs, Exam.cs)
│   ├── NaderGorge.Application/
│   │   └── Features/Admin/Commands/ (Create/Update logic modifications)
│   └── NaderGorge.Infrastructure/
│       └── Migrations/ (UnifiedBuilderMigration.cs)

frontend/
├── src/
│   ├── components/
│   │   └── admin/
│   │       ├── UnifiedAssessmentBuilder.tsx
│   │       ├── AddHomeworkForm.tsx (Deleted - mapped to unified)
│   │       └── InlineExamEditor.tsx (Renamed and refactored)
│   └── app/
│       └── admin/content/lessons/[id]/page.tsx
```

**Structure Decision**: Option 2: Web application. The unification spans both the frontend React layer (building the component) and the robust backend domain modification (EF Core mapping).

## Complexity Tracking

No constitution violations were observed; no complexity tracking exceptions are necessary.
