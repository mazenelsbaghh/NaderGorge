# Implementation Plan: Assessment UI Fixes

**Branch**: `032-assessment-ui-fixes` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/032-assessment-ui-fixes/spec.md`

## Summary

This plan outlines the refactoring needed to clean up the student exam UI (removing animations/duplicate numbers, enforcing focus mode), add explicit "Go to Exam" / "Go to Homework" buttons on locked lessons, harmonize the admin homework/exam builder UI to use the same rich question tables, and resolve a 500 internal server error during exam submission.

## Technical Context

**Language/Version**: TypeScript (Next.js 14+), C# (.NET 8.0)
**Primary Dependencies**: React, Tailwind CSS, Framer Motion, ASP.NET Core
**Storage**: PostgreSQL (Entity Framework Core)
**Testing**: Playwright/Jest (Frontend), xUnit (Backend)
**Target Platform**: Web application (Desktop/Mobile responsive)
**Project Type**: Educational Platform (Web App)
**Performance Goals**: N/A (UI tweaks and bug fixes)
**Constraints**: Needs to conform strictly to "Curated Archive" design system; backend needs robust error handling.
**Scale/Scope**: Affects all students taking exams and all admins creating homework.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Modular Clean Architecture**: Pass. The UI fixes are localized to frontend components. Backend fixes are scoped to Exam endpoints.
- **IV. Phased Delivery**: Pass. This refines existing MVP phase features.
- **VI. Single-Flow Registration & UX Simplicity**: Pass. Making the exam view cleaner and adding quick navigation directly aligns with UX simplicity.
- **VII. Observability & Operational Readiness**: Pass. Fixing the 500 error improves operational readiness.
- **VIII. Premium Editorial Design System**: Pass. The removal of duplicate indicators and enforcement of clean focus mode aligns with the "Curated Archive" aesthetics.

## Project Structure

### Documentation (this feature)

```text
specs/032-assessment-ui-fixes/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    └── api.md
```

### Source Code Details

```text
backend/src/NaderGorge.Application/Features/Exams/Commands/
└── SubmitExamAttemptCommand.cs      # The source of the 500 Error

backend/src/NaderGorge.Application/Features/Content/Queries/
└── GetLessonDetailQuery.cs          # Populates the locked reason / blocking IDs

frontend/src/app/student/packages/[packageId]/lessons/[lessonId]/
└── page.tsx                         # Where the LessonViewer renders

frontend/src/components/admin/
└── UnifiedAssessmentBuilder.tsx     # Where the Admin UI table needs to be unified

frontend/src/components/content/
└── LessonViewer.tsx                 # Where the locked lesson quick actions will go

frontend/src/components/exams/
└── ExamViewer.tsx                   # Exam UI cleanup and focus mode automation
```

**Structure Decision**: Standard full-stack layout targeting the specific application layers.

## Complexity Tracking

> No violations found.
