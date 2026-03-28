# Specification: E2E Testing for Phase 2 Academic Operations

**Short Name**: phase2-e2e-tests

## Feature Description
The goal is to create end-to-end (E2E) integration tests to verify that Phase 2 - Academic Operations operates correctly and seamlessly integrates with the Phase 1 components. This includes creating robust testing scenarios that cover the entire sequence of events from a student completing a lesson package, taking an exam/homework, grading by an assistant, and finally assessing the gamification points and parent reporting logic.

## User Scenarios & Testing

### Scenario 1: Complete Student Gamification Journey
- **Given** an authenticated student enrolled in a Phase 1 package
- **When** the student views a lesson video, triggers tracking events, and submits a final homework associated with it
- **Then** the application properly handles the submission in the backend database
- **And** gamification points are automatically awarded to the student's gamification profile.

### Scenario 2: Assistant Evaluation and Grading Queue
- **Given** an academic assistant navigating to the Assistant Dashboard
- **When** they view the pending tasks queue
- **Then** they should see the previously submitted homework by the student
- **And** resolving the task with grading notes updates the student's status successfully without breaking Phase 1 content access.

### Scenario 3: Parental Reporting Visibility
- **Given** an external request pulling the parent report summary by student ID
- **When** the Parent Report endpoint is called
- **Then** it accurately reflects the student's newly acquired points, completed lessons, exam passes, and recent warning alerts.

## Functional Requirements
- E2E tests must be written exclusively using Playwright for the frontend/backend integrated journey.
- Tests must utilize `E2eTestingController.cs` to cleanly setup and teardown test data without affecting production tables.
- The testing suite must successfully execute via `npm run test:e2e` inside the `frontend` directory.
- Tests must validate the points calculation for Student Gamification.
- Tests must validate that gating rules of mandatory homework are properly applied to Phase 1 Lesson objects.

## Success Criteria
- 100% pass rate of the new Playwright test suite for the Academic Operations path.
- CI/CD or local test execution completes in under 2 minutes for the newly added scenarios.
- The tests accurately mock or handle Auth state from Phase 1.
- No actual SMS/Emails are dispatched during the tests (Worker jobs for alerts are safely mocked/isolated in E2E environment).

## Key Entities
- `StudentGamification`
- `LessonProgress`
- `HomeworkSubmission`
- `AssistantTaskQueue`
- `StudentExamAttempt`
