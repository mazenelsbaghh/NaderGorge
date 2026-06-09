# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed in [spec.md](./spec.md)
- [x] Phase 2: Technical Planning (`speckit-plan`) completed in [plan.md](./plan.md)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed in [tasks.md](./tasks.md)

---

# Tasks: Role Pages and Permissions Completion

**Input**: Design documents from `/specs/110-role-pages-and-permissions/`
**Prerequisites**: plan.md, spec.md

**Tests**: Backend unit tests, python E2E permission tests, and frontend build tests.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel
- **[Story]**: Which user story this task belongs to

---

## Phase 1: Backend Features & Endpoints

**Purpose**: Implement backend commands, queries, and controller actions.

- [ ] T001 [P] [US1] Create `backend/src/NaderGorge.Application/Features/Teacher/TeacherActivity.cs` defining `GetTeacherActivityQuery` and handler. Let it fetch active student watch events, top watched videos, and inactive student alerts (>7 days).
- [ ] T002 [P] [US1] Expose `GET /api/teacher/activity` inside `TeacherController.cs` calling `GetTeacherActivityQuery`.
- [ ] T003 [P] [US2] Create `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentProfileQuery.cs` to return complete student profile details (including device counts and limits).
- [ ] T004 [P] [US2] Create `backend/src/NaderGorge.Application/Features/Student/Commands/UpdateStudentProfileCommand.cs` to handle updates of student address, school name, secondary phone, and parent phones.
- [ ] T005 [P] [US3] Create `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentNotificationsQuery.cs` returning all in-app notifications.
- [ ] T006 [P] [US3] Create `backend/src/NaderGorge.Application/Features/Student/Commands/MarkNotificationAsReadCommand.cs` to mark an in-app notification as read.
- [ ] T007 [P] [US2/3] Register profile and notification actions (`GET profile`, `PUT profile`, `GET notifications`, `POST notifications/{id}/read`) in `StudentController.cs`.
- [ ] T008 [P] [US4] Secure `AssistantController.cs` endpoints under `my/*` by applying `[Authorize(Roles = "Admin,Supervisor,Assistant,Staff,AssistantAcademic,AssistantReviewer")]` to `GetMyTasks`, `GetTaskDetails`, `UpdateStatus`, and `AddComment`.

---

## Phase 2: Frontend Pages & Chrome Layouts

**Purpose**: Create missing frontend routes and update navigation.

- [ ] T009 [US1] Create `frontend/src/app/teacher/activity/page.tsx` rendering active student progress grids, watch duration statistics, and inactive student alerts.
- [ ] T010 [US2] Create `frontend/src/app/student/profile/page.tsx` presenting personal details, device status, and profile update forms.
- [ ] T011 [US3] Create `frontend/src/app/student/notifications/page.tsx` displaying in-app notification feeds and mark-as-read buttons.
- [ ] T012 [US2/3] Update `frontend/src/components/layout/StudentShellChrome.tsx` to include profile/notifications links, fetch notification list to count unread notifications, and render a notification badge in the header/mobile nav.

---

## Phase 3: Testing & Verification

**Goal**: Verify all modifications with C# and Python tests.

- [ ] T013 [US4] Add C# unit tests in `TeacherIsolationTests.cs` verifying teacher binding constraints.
- [ ] T014 [US4] Create `tests/test_assistant_permissions.py` E2E check asserting that Student/Teacher tokens are rejected (403/401) on `my/*` assistant task APIs.
- [ ] T015 [US4] Run `dotnet test` in `backend/` to verify all C# tests pass successfully.
- [ ] T016 [US4] Run `.venv/bin/python -m pytest tests/ -q` to verify python E2E tests pass.

---

## Phase 4: Quality Gate & Builds

**Goal**: Ensure clean code, testing, and production build compliance.

- [ ] T017 [P] Execute `clean-code-guard` against all modified/created production files and resolve any findings.
- [ ] T018 [P] Execute `test-guard` against all modified or created test files.
- [ ] T019 [P] Perform a final production build verification on the frontend (`npm run build` or equivalent) to ensure no warnings or compilation issues exist.
