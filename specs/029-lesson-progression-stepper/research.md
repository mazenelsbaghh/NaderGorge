# Research: Lesson Progression & Stepper UI

## Context

We need to implement a mandatory progression system where a lesson cannot be unlocked until the prior lesson's homework and exam are successfully completed. Simultaneously, we need to introduce a randomized, animated "Stepper" component for all assessments (homework and exams) based on a React Bits template.

## Areas Investigated

### 1. Assessment Randomization
**Decision**: Shuffle questions and answers dynamically on the frontend during the initialization of the Stepper, but submit exact `questionId` to the backend.
**Rationale**: Attempting to persist a shuffled array per user per attempt on the backend requires a heavy relational model (Attempt -> AttemptedQuestion -> ShuffledOptions). By shuffling the arrays randomly on the frontend using something like Fisher-Yates shuffle during the initial mount of the assessment, we maintain unique UX while the backend simple validates `[ { questionId, answerId } ]` without needing to care about ordering.
**Alternatives considered**: Backend-side persistence of shuffled maps. Rejected due to over-engineering for a simple anti-cheating mechanism.

### 2. Mandatory Progression Validation
**Decision**: Backend API must validate prerequisites before returning `LessonDetailDto`, returning a `403 Forbidden` with locked status details if prerequisites are incomplete. The frontend will catch the 403 or specific `isLocked` flag in the hierarchy and render a "LockedState" UI.
**Rationale**: Frontend-only locks can be bypassed by manual API calls or inspecting React DevTools. Prerequisite checking strictly belongs to the server domain.
**Alternatives considered**: Validating purely via frontend Zustand store. Rejected (security risk).

### 3. The Shared Stepper Component
**Decision**: Refactor the provided `motion/react` based Stepper into `frontend/src/components/ui/animated-stepper.tsx`.
**Rationale**: The provided code relies on `motion/react` which in modern Framer Motion is practically `framer-motion`. We must adapt its specific props to accept an array of questions, handle custom submit functions, and synchronize with the existing `HomeworkSubmission` workflow in `LessonViewer.tsx`. We will replace `#5227FF` colors with our `var(--admin-primary)` tokens to match the Editorial Design System.

## Constitution Alignment

- **VIII. Aesthetics and Principles:** Utilizing fluid `framer-motion` springs for question transitions perfectly aligns with the requirement for smooth page transitions and avoiding jarring cuts. Replacing hardcoded colors with brand CSS variables ensures strict conformity.
