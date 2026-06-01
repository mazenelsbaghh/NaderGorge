# Developer Quickstart: Lesson Progression Stepper

## Prerequisites

- Frontend running on `http://localhost:3000`
- Backend running on `http://localhost:5245`
- A test student account enrolled in a package containing at least two sequence lessons.

## Testing Locally

1. **Verify Stepper UI (Exams & Homework):**
   - Log in as the test student and navigate to a lesson containing a homework assignment.
   - The homework UI should render via the new `AnimatedStepper` component instead of standard stacked fields.
   - Verify smooth horizontal sliding animations when clicking "Previous/Next".
   - Reload the page. Verify the questions map has been shuffled (e.g., Q1 is now Q4) and the multiple-choice options for a specific question are also shuffled.

2. **Verify Progression Lock (Enforcement):**
   - In the database, ensure Lesson 1 has `homework` and `examId`.
   - Attempt to directly access `http://localhost:3000/student/packages/{pkgId}/lessons/{Lesson-2-Id}`.
   - You should be greeted with a "Locked" or "Access Denied" view explicitly stating that you must pass Lesson 1's assessments first.
   - If the API returns 403, the frontend should catch this cleanly and render `<LockedLessonCard />`.

3. **Verify Success/Fail Logic:**
   - Take the exam for Lesson 1 and intentionally fail it.
   - Attempt to access Lesson 2. It must remain locked.
   - Retake the exam and achieve a passing score.
   - Submit the homework.
   - Attempt to access Lesson 2 again. It must now unlock successfully, granting full access to its video content.

## Troubleshooting

- **Stepper Content Scaling Issues**: If the `AnimatedStepper` jumps erratically or cuts off long questions, ensure the `onHeightReady` framer-motion logic is correctly capturing the child's bounding box. Disable `overflow: hidden` on children if necessary.
- **Randomization Desync**: If the user submits an answer but it maps to the wrong database ID, verify that the `shuffle()` logic strictly preserves the original ID object reference and only reorders the mapping array visually. The payload strictly requires `{ questionId, answerId }`.
