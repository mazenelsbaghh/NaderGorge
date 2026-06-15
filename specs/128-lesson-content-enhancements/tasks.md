# Tasks: 128 — Lesson Content Enhancements

**Input**: Design documents from `/specs/128-lesson-content-enhancements/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅

**Tests**: Tests are MANDATORY for this project when a phase changes behavior,
data, permissions, API contracts, worker jobs, or user-visible UI.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Database migration and shared service layer updates that unblock all user stories

- [x] T001 Add `IsActive` boolean property (default `true`) to `LessonVideo` entity in `backend/src/NaderGorge.Domain/Entities/ContentEntities.cs`
- [x] T002 Create EF Core migration `AddIsActiveToLessonVideo` via `dotnet ef migrations add` in `backend/src/NaderGorge.Infrastructure/Migrations/`
- [x] T003 [P] Add `toggleVideoActive(videoId: string)` method to `frontend/src/services/admin-service.ts`
- [x] T004 [P] Add `uploadResourceFile(file: File): Promise<{ url: string }>` method to `frontend/src/services/admin-service.ts`

**Checkpoint**: Schema updated, service layer ready for UI work

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Backend endpoints and query enhancements that user story UIs depend on

**⚠️ CRITICAL**: No frontend story work can begin until these endpoints exist

- [x] T005 Create `ToggleVideoActiveCommand.cs` (MediatR command + handler) in `backend/src/NaderGorge.Application/Features/Admin/Commands/ToggleVideoActiveCommand.cs`
- [x] T006 [P] Add `PATCH videos/{id:guid}/toggle-active` endpoint to `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T007 [P] Add `POST resources/upload` endpoint (IFormFile, 10MB limit, saves to `wwwroot/uploads/resources/`) to `backend/src/NaderGorge.API/Controllers/AdminController.cs`
- [x] T008 [P] Enhance `GetExamDashboardQuery` to include per-question stats (`CorrectCount`, `WrongCount`, `CorrectPercentage`) by joining `StudentAnswer` data in `backend/src/NaderGorge.Application/Features/Admin/Queries/GetExamDashboardQuery.cs`
- [x] T009 [P] Add `CorrectCount`, `WrongCount`, `TotalAttempts`, `CorrectPercentage` fields to `ExamQuestionSummaryDto` in the same query file
- [x] T010 Filter `IsActive == true` on videos in student-side lesson content queries (e.g., `GetLessonContentQuery` or equivalent student endpoint)
- [x] T011 Verify `dotnet build` passes with 0 errors in `backend/`

**Checkpoint**: All backend endpoints ready — frontend story work can begin

---

## Phase 3: User Story 1 — تعديل الفيديو بفورم (Priority: P1) 🎯 MVP

**Goal**: Replace `window.prompt` video editing with an inline form pre-filled with current data

**Independent Test**: Admin opens lesson → Videos tab → clicks Edit → sees pre-filled form → changes title → saves → list refreshes with new title

### Implementation for User Story 1

- [x] T012 [US1] Remove `handleEditVideo` function (window.prompt-based) from `frontend/src/components/admin/LessonVideoList.tsx`
- [x] T013 [US1] Add `editingVideoId` state and inline `VideoEditForm` component within `LessonVideoList.tsx` that renders below the video row being edited
- [x] T014 [US1] Pre-populate form fields (title, provider Dropdown, urlOrEmbedCode, order NumberField, maxWatchCount NumberField) from the video's current data
- [x] T015 [US1] Wire form submit to `adminService.updateVideo()`, on success clear editing state and call `onRefresh()`
- [x] T016 [US1] Add Cancel button that clears the editing state without saving

**Checkpoint**: Video editing works via inline form instead of browser prompts

---

## Phase 4: User Story 2 — تفعيل/إخفاء الفيديو (Priority: P2)

**Goal**: Add Eye/EyeOff toggle to control video visibility to students

**Independent Test**: Admin toggles a video inactive → video row dims → student lesson page hides that video

### Implementation for User Story 2

- [x] T017 [P] [US2] Add Eye/EyeOff toggle button to the action buttons area in `frontend/src/components/admin/LessonVideoList.tsx`
- [x] T018 [P] [US2] Wire toggle button to call `adminService.toggleVideoActive(videoId)` with loading state
- [x] T019 [US2] Add dimmed row styling (`opacity-50`) and "مخفي" badge for inactive videos in `LessonVideoList.tsx`
- [x] T020 [US2] Ensure video `isActive` field is included in lesson cockpit DTO response so the frontend can read it

**Checkpoint**: Admin can toggle video visibility, students only see active videos

---

## Phase 5: User Story 3 — رفع الملفات مباشرة + لينك (Priority: P3)

**Goal**: Support both file upload and URL link for resource attachment

**Independent Test**: Admin opens Resources tab → switches to "رفع ملف" tab → uploads PDF → file appears in resource list with server URL

### Implementation for User Story 3

- [x] T021 [US3] Refactor `AddResourceForm.tsx` to add tab/radio toggle between "رفع ملف" and "لينك مباشر" modes in `frontend/src/components/admin/AddResourceForm.tsx`
- [x] T022 [US3] Implement file upload tab with `<input type="file">` and drag-over styling, accepted types: PDF, images, Word docs
- [x] T023 [US3] Wire file upload to `adminService.uploadResourceFile(file)` which calls `POST /api/admin/resources/upload`
- [x] T024 [US3] On upload success, use the returned URL as `fileUrl` and auto-submit resource creation via `adminService.createResource()`
- [x] T025 [US3] Add upload progress indicator and error handling (max 10MB validation client-side)

**Checkpoint**: Admin can upload files directly or paste URLs for lesson resources

---

## Phase 6: User Story 4 — بروفايل الامتحان المفصل (Priority: P4)

**Goal**: Create a dedicated exam profile page with per-question analytics, inline editing, and question addition

**Independent Test**: Admin clicks "عرض البروفايل" on an exam → navigates to exam profile page → sees stats and question list with correct/wrong percentages

### Implementation for User Story 4

- [x] T026 [P] [US4] Create exam profile server page at `frontend/src/app/admin/content/exams/[id]/page.tsx`
- [x] T027 [US4] Create `ExamProfilePageClient.tsx` at `frontend/src/app/admin/content/exams/[id]/ExamProfilePageClient.tsx` with:
  - Header section: exam title, description, stat cards (questions, total score, passing score, duration, attempts count, average score)
  - Students attempts table with name, phone, score, pass/fail, date
  - Questions list with per-question correct/wrong percentage bars
- [x] T028 [US4] Add "عرض البروفايل" navigation button to `frontend/src/components/admin/AttachedExamViewer.tsx` linking to `/admin/content/exams/{examId}`
- [x] T029 [US4] Add inline question editing capability in ExamProfilePageClient (click edit → editable text field → save)
- [x] T030 [US4] Add "إضافة سؤال" form at the bottom of the exam profile page using existing `UnifiedAssessmentBuilder` or inline form

**Checkpoint**: Exam profile page shows full analytics and allows question management

---

## Phase 7: User Story 5 — حفظ تلقائي للامتحان (Priority: P5)

**Goal**: Auto-save exam questions during creation to prevent data loss

**Independent Test**: Admin starts creating exam → adds 2 questions → refreshes page → questions are persisted

### Implementation for User Story 5

- [x] T031 [US5] Modify `UnifiedAssessmentBuilder` to detect when exam has been created (has ID) and switch to incremental question-add mode in `frontend/src/components/admin/UnifiedAssessmentBuilder.tsx`
- [x] T032 [US5] After first exam creation, each new question immediately calls `adminService.addQuestionToExam(examId, question)` instead of batching
- [x] T033 [US5] Add auto-save status indicator component: "⏳ جارٍ الحفظ..." → "✓ تم الحفظ" → "✕ خطأ في الحفظ"
- [x] T034 [US5] Handle auto-save errors gracefully with retry toast and don't block the user from adding more questions

**Checkpoint**: Exam creation auto-saves questions, no data lost on navigation

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final quality pass across all user stories

- [x] T035 [P] Export new components from `frontend/src/components/admin/index.ts` (ExamProfilePageClient if needed)
- [x] T036 Verify `npm run build` passes with 0 errors in `frontend/`
- [x] T037 [P] Run `dotnet build` in `backend/` and confirm 0 new warnings/errors
- [x] T038 Code cleanup: remove any unused imports, dead code, or console.logs across all modified files
- [x] T039 Run quickstart.md validation checklist manually

---

## Phase 9: End-of-Phase Verification, Docker Gate & Manual QA Report

**Purpose**: Prove the phase is complete in the real project environment

- [x] T040 Run `dotnet build` in `backend/` and record results
- [x] T041 Run `npm run build` in `frontend/` and record results
- [x] T042 Run `docker compose config -q` and confirm valid compose
- [x] T043 Run `make migrate` to apply `AddIsActiveToLessonVideo` migration
- [x] T044 Complete manual QA checklist:
  - Admin: Edit video via inline form → verify data persists
  - Admin: Toggle video active/inactive → verify dim styling
  - Student: Verify inactive videos hidden from lesson content
  - Admin: Upload PDF file → verify in resource list
  - Admin: View exam profile → verify per-question stats
  - Admin: Add question via auto-save → refresh → verify persisted
- [x] T045 Write end-of-phase report with implemented scope, commands run, automated results, Docker result, manual QA status, risks, and go/no-go

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (migration + entity field)
- **User Stories (Phase 3–7)**: All depend on Phase 2 backend endpoints
  - US1 (Phase 3): Independent — no cross-story deps
  - US2 (Phase 4): Independent — no cross-story deps
  - US3 (Phase 5): Independent — no cross-story deps
  - US4 (Phase 6): Independent — no cross-story deps
  - US5 (Phase 7): Independent — no cross-story deps
- **Polish (Phase 8)**: Depends on all user stories
- **Verification (Phase 9)**: Depends on Phase 8

### User Story Dependencies

- **US1 (Video Edit Form)**: Phase 2 only → modifies `LessonVideoList.tsx`
- **US2 (Video Toggle)**: Phase 2 (T005/T006 endpoint) → modifies `LessonVideoList.tsx` (⚠️ merge with US1 changes)
- **US3 (Resource Upload)**: Phase 2 (T007 endpoint) → modifies `AddResourceForm.tsx`
- **US4 (Exam Profile)**: Phase 2 (T008/T009 stats) → new page + modifies `AttachedExamViewer.tsx`
- **US5 (Auto-Save)**: Phase 2 only → modifies `UnifiedAssessmentBuilder.tsx`

### Within Each User Story

- Models/DTOs before services
- Services before UI components
- Core implementation before polish
- Story complete before moving to next priority

### Parallel Opportunities

- T003 + T004 can run in parallel (different service methods)
- T005 + T007 + T008 can run in parallel (different backend files)
- T017 + T018 can run in parallel (same file but different sections)
- T026 can run in parallel with T028 (different files)
- US3 + US4 + US5 are fully independent and can be done in parallel

---

## Parallel Example: User Story 1

```bash
# US1 tasks are sequential (same file: LessonVideoList.tsx)
# T012 → T013 → T014 → T015 → T016
# But US1 can run in parallel with US3, US4, US5
```

## Parallel Example: Phase 2 (Backend)

```bash
# These can all run in parallel:
Task: "T005 — ToggleVideoActiveCommand.cs"
Task: "T007 — POST resources/upload endpoint"
Task: "T008 — Enhance GetExamDashboardQuery with stats"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T004)
2. Complete Phase 2: Foundational (T005–T011)
3. Complete Phase 3: US1 — Video Edit Form (T012–T016)
4. **STOP and VALIDATE**: Test inline video editing independently
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Backend ready
2. US1 (Video Edit) → Test → Deploy ✅
3. US2 (Video Toggle) → Test → Deploy ✅
4. US3 (Resource Upload) → Test → Deploy ✅
5. US4 (Exam Profile) → Test → Deploy ✅
6. US5 (Auto-Save) → Test → Deploy ✅
7. Polish + Verification → Final deploy

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- US1 + US2 both modify `LessonVideoList.tsx` — implement sequentially to avoid conflicts
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Do not start the next phase until the end-of-phase Docker/manual QA gate is complete
