Created At: 2026-06-09T05:03:25Z
Completed At: 2026-06-09T05:03:25Z
File Path: `file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/102-multi-teacher-enforcement/tasks.md`

# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Multi-Teacher Enforcement

**Input**: Design documents from `specs/102-multi-teacher-enforcement/`
**Prerequisites**: [plan.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/102-multi-teacher-enforcement/plan.md), [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/102-multi-teacher-enforcement/spec.md)

## Phase 1: Backend DTO & Command Constraints

- [x] T001 [P] Modify `CreatePackageCommand` in [AdminContentCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs) to accept `Guid? TeacherId = null`.
- [x] T002 Modify `CreatePackageCommandHandler` in [AdminContentCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs) to:
  - Fail if `ProgramId` is null/empty.
  - Require and validate `TeacherId` if caller is Admin/Supervisor. Return `Fail` if missing or if the teacher is not found.
  - Verify that the chosen teacher teaches the subject of the selected program by querying `TeacherSubjects`. Return `Fail` if there is no association.
  - Remove first/default teacher fallback for Admin creation.
- [x] T003 [P] Modify `CreateQuestionCommand` in [AdminQuestionCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminQuestionCommands.cs) to accept `Guid? TeacherId = null`.
- [x] T004 Modify `CreateQuestionCommandHandler` in [AdminQuestionCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminQuestionCommands.cs) to:
  - Fail if `SubjectId` is null/empty.
  - Require and validate `TeacherId` if caller is Admin/Supervisor. Return `Fail` if missing.
  - Verify that the resolved `TeacherId` teaches the selected `SubjectId` by querying `TeacherSubjects`. Return `Fail` if there is no association.
  - Remove first/default teacher/subject fallback for Admin creation.

## Phase 2: Frontend Service Contracts & UI Dropdowns

- [x] T005 [P] Update `createPackage` and `createQuestion` in [admin-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/admin-service.ts) to strictly type the payloads and require `teacherId` and `subjectId`/`programId`.
- [x] T006 [P] Update the Admin package creation modal in [page.tsx (Content)](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/content/page.tsx) to fetch programs and teachers, and require their selection before saving.
- [x] T007 [P] Update the Teacher package creation modal in [page.tsx (Teacher Packages)](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/teacher/packages/page.tsx) to filter programs based on subjects taught by the teacher (so they can only choose valid subjects).
- [x] T008 [P] Update the Admin question creation form in [page.tsx (Questions)](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/questions/page.tsx) to force selecting a Subject and a Teacher.

## Phase 3: Copy & Dynamic Landing Section

- [x] T009 [P] Update FAQ page [page.tsx (FAQ)](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/faq/page.tsx) to remove history-exclusive copy.
- [x] T010 [P] Update [CircularGallerySection.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/landing/CircularGallerySection.tsx) to fetch and render dynamic teachers from `/api/public/teachers`.

## Phase 4: Verification, Testing, & Launch Drill

- [x] T011 [P] Create Python integration test `tests/test_multi_teacher_validation.py` to assert correct backend behavior.
- [x] T012 Rebuild the backend container and apply migrations natively or via migrator.
- [x] T013 Compile check the Next.js admin frontend and make sure it builds without errors.
- [x] T014 Run `.venv/bin/pytest tests/` and verify all tests pass successfully.
- [x] T015 Perform Launch Drill: `make down`, `docker compose build --no-cache`, `make up`, `make migrate`, verify services health, run tests.

## Phase 5: Quality Gates (Mandatory)

- [x] T016 Run `clean-code-guard` against all modified frontend/backend files and resolve any reported findings.
- [x] T017 Run `test-guard` against changed test files and resolve any findings.
