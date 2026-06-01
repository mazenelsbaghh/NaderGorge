# Quickstart Guide: Unified Assessment Builder

## Overview

The feature unifies the frontend component used for building assessments (`Exams` and `Homework`) in the admin interface into a single component named `UnifiedAssessmentBuilder` while introducing toggles for `IsMandatory` and `IsRandomized`.

## Implementing the Unified Builder

1. **Backend Database Changes**
   - Add `IsRandomized` boolean directly to both `Homework` and `Exam` tables via EF Core Migration.
   - Add `IsMandatory` boolean to the `Exam` table.
   - Update your `Command` classes and Handlers to accept and map these payload flags.

2. **Frontend Unification**
   - Take the current `InlineExamEditor.tsx` component.
   - Rename/refactor it to `UnifiedAssessmentBuilder.tsx`.
   - Add a `type: 'exam' | 'homework'` prop to define submission URL (`/api/Admin/exams` vs `/api/Admin/homework`) and UI text labels (e.g. "Create Exam" vs "Create Homework").
   - Include two new switch toggles for Randomization and Mandatory.
   - Remap the internal query object so that `Homework` questions arrays conform to the required endpoint format on submission (e.g., mapping `points` to `pointsActive`, parsing `options` to string arrays for Homework while maintaining object logic for Exams).

3. **Backend Logic**
   - In your exam/homework retrieval endpoint (e.g., `GetLessonDetailQueryHandler`), if `IsRandomized` is `true`, shuffle the `Questions` collection using `Order` modifications.

4. **Testing**
   - Run `# dotnet run` and `# npm run dev`.
   - Go to a lesson in the admin panel and add a standard Homework, checking Randomize.
   - Verify it saves successfully.
   - Visit the student page and verify questions are loaded in random order.
