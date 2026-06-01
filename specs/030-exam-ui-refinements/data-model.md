# Data Model & Interfaces: Exam UI Refinements

## Changed Entities

1. **LessonDetailDto** (Backend `NaderGorge.Application.Features.Content.Queries`)
   - Field `LockedReason` remains a `string`, but its message will dynamically embed the title of the blocking prerequisite. Example: "يجب اجتياز واجب 'تدريبات الحركة' أولاً".

2. **ExamQuestionDto / AnswerSubmissionDto** (Frontend `content-service.ts`)
   - No direct data schema changes, but frontend `answers` state will now effectively support `null` or excluded question IDs during submit if the user hasn't explicitly answered a "Skipped" question.
   - Wait: The backend allows partial submissions?
     - Let's check: The frontend already enforces validation before submit (`allAnswered`), but the new spec says users can skip and we can navigate back. Submitting skipped questions might just submit what's answered, or require returning.

## Interfaces & Contracts

1. **Admin System Controller** (Backend)
   - Route: `POST /api/Admin/System/SeedTestCourse`
   - Request Body: `None`
   - Response: `ActionResponse` indicating successful creation.

2. **Countdown Timer Component** (Frontend)
   - Props: `targetDate: Date`, `onTimeExpired: () => void`, `className?: string`.
   - The CSS requires `.countdown` class. It will utilize standard Tailwind layers or standalone CSS injected safely via an isolation `.css` file.
