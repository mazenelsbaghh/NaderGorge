# Quickstart: Student Theme Color Customization

## Goal

Validate that a student can choose distinct curated color palettes for light mode and dark mode, and that those selections persist across authenticated sessions.

## Prerequisites

- Development frontend, backend API, and database are running.
- A student account exists and can sign in successfully.
- The feature branch schema changes, if any, have been applied.

## Manual Verification Flow

1. Sign in as a student and open the student area.
2. Confirm the current light/dark mode toggle still works before using any new palette controls.
3. Open the new student theme settings surface from the shell by clicking the settings icon in the student sidebar or the mobile header actions.
4. In light mode, choose a non-default light palette and verify:
   - the new palette applies immediately
   - navigation, cards, buttons, and lesson content remain readable
   - the chosen option is visibly marked as selected
5. Switch to dark mode and choose a non-default dark palette. Verify the same expectations.
6. Refresh the page while staying signed in. Confirm the active mode and the saved palette for that mode are restored correctly.
7. Sign out, sign back in, and confirm the same saved palette choices are restored.
8. If an old saved palette is manually removed or deprecated in test data, confirm the student experience falls back to the approved default palette and still loads without errors.

## API Verification

1. Call `GET /api/student/theme-preferences` as an authenticated student.
2. Verify the response includes:
   - selected palette identifiers for light and dark modes
   - approved available palette options grouped by mode
   - default palette identifiers
3. Call `PUT /api/student/theme-preferences` with valid palette identifiers for both modes.
4. Verify a subsequent `GET` returns the updated values.
5. Call `PUT /api/student/theme-preferences` with an invalid or deprecated palette identifier and confirm the request is rejected with a consistent error response.

## Regression Checks

- Student shell navigation still renders correctly in both modes.
- Admin and public authentication pages do not inherit student-only palette choices.
- Existing light/dark toggles continue to function on student pages.
- No unreadable text, invisible actions, or low-contrast status indicators appear in any approved palette.
