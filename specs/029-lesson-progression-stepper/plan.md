# Implementation Plan: Lesson Progression Stepper

**Branch**: `029-lesson-progression-stepper` | **Date**: 2026-03-31 | **Spec**: [spec.md](./spec.md)

## Summary

This feature enforces sequential lesson progression by requiring students to pass the prior lesson's homework and exam before accessing new content. It also introduces a unified, beautifully animated `AnimatedStepper` component (powered by Framer Motion) for both homework and exams. To deter cheating, the questions and options within these assessments will be randomized locally on the frontend upon rendering, while strictly validating prerequisite logic on the backend.

## Technical Context

**Language/Version**: TypeScript / C# .NET 8
**Primary Dependencies**: Next.js App Router, React (framer-motion, lucide-react), Zustand, React Query
**Storage**: PostgreSQL via Prisma (Backend)
**Target Platform**: Web (Responsive Desktop & Mobile)
**Project Type**: Fullstack Web Application (Next.js Frontend + .NET Backend)
**Performance Goals**: 60fps animations for Stepper, <200ms API response for lesson progression validation.
**Constraints**: Must adhere strictly to the "Editorial Design System". Frontend randomization must not alter backend data schemas.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Design System Conformity**: The provided React Bits stepper must be heavily refactored to consume `var(--admin-primary)` and `var(--admin-card)` tokens instead of hardcoded hex values, fulfilling Constitution Principle VIII.
- **Frontend Architecture**: Utilizing standard React Hooks and framer-motion inside `/components/ui` fulfills the modular architecture guideline.
- **Backend Architecture**: The Progression Service will evaluate prerequisite completions before serving standard Lesson DTOs, aligning with the domain-driven N-Tier backend pattern.

**Status**: PASS

## Project Structure

### Documentation (this feature)

```text
specs/029-lesson-progression-stepper/
├── plan.md              # This file
├── research.md          # Architecture decisions
├── data-model.md        # API contracts & frontend state
├── quickstart.md        # Testing and onboarding notes
└── tasks.md             # Implementation tasks (generated next)
```

### Source Code

```text
backend/src/NaderGorge.Core/
├── Features/
│   └── Lessons/
│       ├── Validations/LessonProgressionValidator.cs
│       └── Queries/GetLessonDetailQueryHandler.cs

frontend/src/
├── components/
│   ├── ui/
│   │   └── animated-stepper.tsx          # Shared Stepper UI component
│   └── content/
│       ├── LessonViewer.tsx              # Integrates Stepper for homework
│       └── ExamViewer.tsx                # Integrates Stepper for exams
└── stores/
    └── lesson-progression-store.ts       # Optional: Client-side progression cache
```

**Structure Decision**: The frontend component `animated-stepper.tsx` will be placed in the generic `ui` directory as it is highly reusable. The progression checks will be augmented in the backend `GetLessonDetail` flow, ensuring the frontend simply reacts to locked payloads or HTTP 403s.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*(No violations detected)*
