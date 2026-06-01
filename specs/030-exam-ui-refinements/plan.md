# Implementation Plan: Exam UI Refinements and Locked Reasons

**Branch**: `030-exam-ui-refinements` | **Date**: 2026-03-31 | **Spec**: [spec.md](../spec.md)
**Input**: Feature specification from `/specs/030-exam-ui-refinements/spec.md`

## Summary

This plan outlines the refactoring of the lesson locked reason to display the explicit prerequisite assessment title by extending queries in the Backend. It also introduces a dynamic, visually polished, checkout-style question navigation bar (circles replacing lines) for the `ExamViewer` and `AnimatedStepper`. Finally, it includes the creation of an admin seeder endpoint to generate 0-Price content for friction-free enrollment testing.

## Technical Context

**Language/Version**: C# 12 (.NET 8.0) | TypeScript (React 18)
**Primary Dependencies**: EF Core 8, Tailwind CSS, Framer Motion
**Storage**: PostgreSQL (AppDbContext)
**Architecture**: Clean Architecture API + Next.js App Router
**Project Type**: Full-stack web application

## Constitution Check

*GATE: Passed. Modifications conform to Academic Content Integrity and Premium Editorial Design System.*

- No borders (lines) for main layout definitions. Progress indicators will rely on color fills or distinct strokes.
- Backend single-source-of-truth timer mechanics remain intact; UI timers only update visual states.
- Admin component reuse: Adheres to Shared Components where applicable (API Seeder triggered via Swagger / Backend).

## Project Structure

### Documentation (this feature)

```text
specs/030-exam-ui-refinements/
├── plan.md              # This file
├── research.md          # Technical approach decisions
├── data-model.md        # Dto shifts and endpoints
├── quickstart.md        # Not applicable
└── tasks.md             # Task breakdown
```

### Source Code

```text
frontend/
└── src/
    ├── components/
    │   ├── exams/
    │   │   ├── ExamViewer.tsx     # Overhauled question pagination and timer
    │   │   └── CountdownTimer.tsx # New DaisyUI-style CSS variable timer
    │   └── ui/
    │       └── animated-stepper.tsx # Adjusted to render circles instead of lines
    └── styles/
        └── globals.css              # Custom countdown masking rules

backend/
└── src/
    ├── NaderGorge.Application/
    │   └── Features/Content/Queries/
    │       └── GetLessonDetailQuery.cs     # Injecting prerequisite Titles into LockedReason
    └── NaderGorge.API/
        └── Controllers/
            └── AdminController.cs          # Test seeding endpoint
```

**Structure Decision**: Standard Full Stack split between Next.js React and .NET. Modifications heavily interact with existing UI components but require adding standalone elements (CountdownTimer) to keep concerns organized.

## Complexity Tracking

None defined. All changes adhere strictly to the Nader George architectural guidelines.
