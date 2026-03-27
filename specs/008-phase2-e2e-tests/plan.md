# Implementation Plan: E2E Testing for Phase 2 Academic Operations

## Technical Context
- **Frameworks**: Playwright for frontend testing, ASP.NET Core API for backend logic setup, React Next.js for UI.
- **Goal**: Formulate robust tests bridging User Story 1 (Homework), Story 2 (System Commitment), Story 3 (Assistant Tasks), Story 4 (Parent Report), and Story 5 (Gamification).
- **Environment**: Backend deployed locally in E2e mode using `ASPNETCORE_ENVIRONMENT=E2e`.
- **Database**: The existing E2E testing controller will be updated or leveraged to orchestrate pre-test data (e.g. create Homeworks, add gamification events, create an assistant). No manual database seeding is allowed inside the frontend.

## Constitution Check
- Must strictly use the existing Playwright configuration located natively in the frontend repository (`frontend/playwright.config.ts`).
- Should integrate with existing setup flows (like register functions) but avoid fragile locators in favor of `data-testid` or predictable ARIA labels where suitable.
- Must preserve separation between test isolation capabilities, dropping table states cleanly before and after testing runs.

## Phases
### Phase 1: Setup Academic Operations E2E Data
- Add `E2eTestingController` endpoint functions to:
  - Create Lesson with embedded Homework
  - Spawn an Assistant account
  - Flush Gamification data or Warning events reliably

### Phase 2: Develop Test Scenarios
- `student-academic.spec.ts`
  - Register student -> Open Lesson -> Take Homework -> Assert status PendingReview.
  - Verify gamification points added via the API call or Header UI display.
- `assistant-dashboard.spec.ts`
  - Login as assistant -> View Task Board -> Grade and Resolve the existing submission.
- `parent-report.spec.ts`
  - Open `{baseUrl}/parent-report/{studentId}` unauthenticated -> Validate total completed lessons, warning status, and exam grades accurately visually render.

### Phase 3: CI & Execution Polish
- Run `npm run test:e2e` in the `frontend` root.
- Ascertain less-than-2-minute execution speed and reliability constraints.
- Adjust UI locators and wait timers to ensure tests don't exhibit flaky behavior.
