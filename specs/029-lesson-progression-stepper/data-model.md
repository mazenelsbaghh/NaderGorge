# Data Model & Interfaces: Lesson Progression Stepper

## Domain Entities

### `LessonProgress`
Tracks the student's passing status for specific lessons.
- `id`: UUID (PK)
- `studentId`: UUID (FK)
- `lessonId`: UUID (FK)
- `homeworkPassed`: Boolean (Default: false)
- `examPassed`: Boolean (Default: false)
- `unlocked`: Boolean (Computed from previous lesson's completion status)

### `AnimatedStepper Props`
Frontend component contract to interface with arrays of assessment questions.
- `questions`: Array<{ id: string, text: string, options: { id: string, text: string }[] }>
- `onComplete`: Callback function triggered after the last step is successfully answered/submitted.
- `onStepChange`: (optional) Callback to track analytics.
- `initialStep`: numeric (Default: 1)
- `requireAnswerToProceed`: boolean (Locks the "Next" button until an option is selected)

## API Communication Changes

### Query: `GetLessonDetail`
The backend must validate prerequisites. If `Lesson N-1` is incomplete, the backend currently returns a 403 or modifies the DTO. To handle this cleanly on the frontend:
- **Modified DTO Field**: `isLocked: boolean` and `lockedReason: string`.

## Frontend State (Zustand)

### `useProgressionStore`
Optional state to globally manage the active locked/unlocked state if navigating quickly, preventing unnecessary API calls if a student tries clicking locked items in a sidebar.
- `unlockedLessons`: Array<UUID>
- `setUnlocked`: (lessonId: UUID) => void
