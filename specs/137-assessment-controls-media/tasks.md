# Tasks: Assessment Controls And Question Media

**Input**: Design documents from `/specs/137-assessment-controls-media/`

## Phase 1: Setup

- [X] T001 Inspect current assessment files and confirm pre-existing dirty worktree is preserved.

## Phase 2: Foundational

- [X] T002 Add nullable `ImageUrl` to `QuestionBankItem` in `backend/src/NaderGorge.Domain/Entities/ExamEntities.cs`.
- [X] T003 Add nullable `ImageUrl` to `HomeworkQuestion` in `backend/src/NaderGorge.Domain/Entities/Homework/HomeworkQuestion.cs`.
- [X] T004 Add EF Core migration for `QuestionBankItem.ImageUrl` and `HomeworkQuestion.ImageUrl` under `backend/src/NaderGorge.Infrastructure/Migrations/`.
- [X] T005 Add admin question image upload endpoint in `backend/src/NaderGorge.API/Controllers/AdminController.cs` that accepts `image`, validates `image/*`, saves through `IContentImageStorage` using folder `questions`, and returns an assets-resolvable relative URL.

## Phase 3: User Story 1 - Configure Mandatory Assessments (P1)

- [X] T006 [US1] Verify and, if needed, preserve `isMandatory` in `frontend/src/components/admin/UnifiedAssessmentBuilder.tsx` for homework, lesson exams, and video exams.
- [X] T007 [US1] Add `isMandatory?: boolean` to `createInlineExam` payload type in `frontend/src/services/admin-service.ts`.
- [X] T008 [US1] Add `isMandatory` to Add Homework page recreate payload in `frontend/src/app/admin/content/homework/[id]/add-question/AddHomeworkQuestionPageClient.tsx`.
- [X] T009 [US1] Verify backend progression checks only block when `IsMandatory` is true in `backend/src/NaderGorge.Application/Features/Content/Queries/GetLessonsQuery.cs`, `StartExamAttemptCommand.cs`, and `StartHomeworkAttemptQuery.cs`.

## Phase 4: User Story 2 - Attach Question Images (P2)

- [X] T010 [US2] Add `imageUrl?: string` to `InlineExamQuestionDto` and image upload UI in `frontend/src/components/admin/QuestionEditor.tsx`.
- [X] T011 [US2] Add `uploadQuestionImage(file)` to `frontend/src/services/admin-service.ts`.
- [X] T012 [US2] Send `imageUrl` from `CreateInlineExamCommand` and `AddQuestionsToExamCommand` into `QuestionBankItem` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs`.
- [X] T013 [US2] Add `ImageUrl` to `UpdateExamQuestionCommand` and persist it in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminExamCommands.cs`.
- [X] T014 [US2] Send `imageUrl` from `AttachHomeworkCommand` into `HomeworkQuestion` in `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs`.
- [X] T015 [US2] Include question image URL in exam attempt and review DTOs in `StartExamAttemptCommand.cs` and `ExamResultBuilder.cs`.
- [X] T016 [US2] Include question image URL in homework attempt and review DTOs in `StartHomeworkAttemptQuery.cs` and `GetHomeworkResultQuery.cs`.
- [X] T017 [US2] Add `imageUrl` fields to `frontend/src/services/exam-service.ts`, `frontend/src/services/homework-service.ts`, and `frontend/src/services/admin-service.ts`.
- [X] T018 [US2] Render question images in `frontend/src/components/exams/ExamViewer.tsx`.
- [X] T019 [US2] Render question images in `frontend/src/components/homework/HomeworkViewer.tsx` and `frontend/src/components/homework/HomeworkResultPanel.tsx`.

## Phase 5: User Story 3 - Clean Question Text Display (P3)

- [X] T020 [US3] Add shared text helpers in `frontend/src/lib/question-text.ts` for safe rich display and plain text extraction.
- [X] T021 [US3] Use the shared helper in exam attempt/review rendering in `frontend/src/components/exams/ExamViewer.tsx`.
- [X] T022 [US3] Use the shared helper in homework attempt/review rendering in `frontend/src/components/homework/HomeworkViewer.tsx` and `HomeworkResultPanel.tsx`.
- [X] T023 [US3] Use plain text helper in admin/teacher table displays that currently show question text from `QuestionBankItemDto`.

## Final Phase: Polish & Validation

- [ ] T024 Run `dotnet test backend/NaderGorge.sln` and fix any build/test failures caused by this feature.
- [ ] T025 Run `npm run lint` in `frontend` and fix any errors caused by this feature.
- [ ] T026 Review UI against Massar product rules: clear labels, 44px controls, visible focus, no raw tags, no layout shift from image previews.
- [ ] T027 Update this `tasks.md` with completed checkboxes and summarize verification evidence.

## Dependencies

- Foundational tasks T002-T005 before image propagation.
- US1 can be verified independently after T006-T009.
- US2 depends on T002-T005 and T010-T019.
- US3 depends on T020 before T021-T023.

## Implementation Strategy

Deliver MVP first with mandatory controls verified. Then add question images end-to-end. Finish with display cleanup and validation.
