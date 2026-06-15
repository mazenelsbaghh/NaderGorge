# Implementation Plan: Homework Progression & Location Fixes

**Branch**: `131-homework-progression-fixes` | **Date**: 2026-06-15 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/131-homework-progression-fixes/spec.md)
**Input**: Feature specification from `specs/131-homework-progression-fixes/spec.md`

## Summary

This plan resolves two critical issues reported by the user:
1. **Section-Isolated Progression Locking**: Restricts the previous lesson locking logic to ONLY look for lessons within the same section. If no previous lesson is found in the current section, the lesson is unlocked (no cross-section progression locking).
2. **Dedicated Homework Workspace**: Removes the duplicate inline homework questions interface from the bottom of the lesson detail page (`LessonViewer.tsx`), keeping it purely in the standalone `/student/homework/[homeworkId]` page, accessible via the "حل الواجب" button.

## Technical Context

- **Language/Version**: C# 13 (.NET 9) Backend, TypeScript (Next.js 16.2.1 / React 19) Frontend
- **Primary Dependencies**: MediatR (Backend CQRS), Framer Motion / Zustand (Frontend)
- **Storage**: PostgreSQL (Lesson, ContentSection entities)
- **Testing**: Playwright E2E tests (`frontend/tests/e2e/`)
- **Target Platform**: Web application (Next.js + C# API)

## Constitution Check

- **Layer impact**:
  - **Backend**: Logic changes inside `GetLessonDetailQueryHandler` and `GetLessonsQuery` to modify database queries for previous lesson locking.
  - **Frontend**: UI simplification inside `LessonViewer.tsx` to remove the inline homework layout.
  - **Worker**: None.
  - **Database/Docker**: None.
- **Automated Tests**: Playwright E2E tests will verify that the homework solver is not visible on the lesson page and that progression works correctly.
- **Docker Gate**: Ensure frontend and backend compile and build.

---

## Proposed Changes

### Backend

#### [MODIFY] [GetLessonDetailQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonDetailQuery.cs)
- Locate the previous lesson query around lines 134-153.
- Remove the `if (previousLesson == null)` fallback block that queries the `ContentSections` table to fetch the last lesson from the previous section.

#### [MODIFY] [GetLessonsQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonsQuery.cs)
- Locate the previous lesson lookup inside `GetBlockingStateAsync` around lines 92-106.
- Remove the fallback block that queries `ContentSections` to find the last lesson of the previous section.

### Frontend

#### [MODIFY] [LessonViewer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/content/LessonViewer.tsx)
- Remove `shuffledQuestions` state, `homeworkAnswers` state, `isSubmitting` state, and `homeworkSubmitted` state.
- Remove `handleHomeworkSubmit` function.
- Remove imports for `AnimatedStepper`, `FindTheMistakeInteract`, and `shuffleArray`.
- Remove the JSX element `{lesson.homework && ( ... )}` rendering the interactive homework stepper from the bottom of the page.

---

## Phase Closure & Verification Plan

### Automated Tests Required
Run existing E2E tests and add tests for homework solver page validation:
- Run `npx playwright test` under the `frontend` folder (or relevant command in `Makefile`).

### Docker Gate Required
- Run `make up` and compile verify.

### Manual QA Required
1. Log in as a student.
2. Access a lesson in a section.
3. Ensure no homework card or question list is rendered at the bottom.
4. Verify the carousel displays the homework button, and clicking it redirects to the standalone homework solving page.
5. Create a lesson at the beginning of a section and verify it is not locked by the last lesson of the previous section.

## Design Phases

### Phase 0: Research
- Consolidated progression locking logic and frontend homework component structure in `research.md`.

### Phase 1: Design & Contracts
- Documented data model requirements in `data-model.md` and verification commands in `quickstart.md`.
