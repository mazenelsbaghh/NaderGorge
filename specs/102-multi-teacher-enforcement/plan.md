# Implementation Plan: Multi-Teacher Enforcement

**Branch**: `102-multi-teacher-enforcement` | **Date**: 2026-06-09 | **Spec**: [specs/102-multi-teacher-enforcement/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/102-multi-teacher-enforcement/spec.md)
**Input**: Feature specification from `/specs/102-multi-teacher-enforcement/spec.md`

## Summary

Enforce strict validation constraints for the Multi-Teacher and Multi-Subject architecture. This involves updating backend MediatR commands (`CreatePackageCommand` and `CreateQuestionCommand`) to accept explicit teacher/subject parameters from Administrators, adding robust cross-validation checks, eliminating default fallbacks to hardcoded values, and modifying both the Administrator and Teacher frontend creation workflows to enforce selecting correct subjects and teachers. Lastly, we will clean up the FAQ text and landing page teacher dynamic fetching.

## Technical Context

**Language/Version**: C# (.NET 9, C# 13), TypeScript 5.x, React 19, Next.js 16.2.1  
**Primary Dependencies**: MediatR, EF Core 9, Axios, Lucide  
**Storage**: PostgreSQL  
**Testing**: pytest (local Python integration test suite)  
**Target Platform**: Docker-compose stack  
**Project Type**: Monorepo Web Application  

## Constitution Check

- **Layer impact**: Backend Commands & API Controllers, Frontend components and services.
- **Automated tests required**: Backend unit tests validating that package/question creation fails without explicit teacher/subject assignment. Python E2E integration test validating correct assignment.
- **Manual QA flows**: Log in as Admin/Teacher and verify that creation forms require subject and teacher, and that dropdown lists are restricted by teacher authority.
- **Docker gate commands**: Rebuild and migrate backend container, run Python tests.

---

## Proposed Changes

### [Backend - API & Commands]

#### [MODIFY] [AdminContentCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs)
- Update `CreatePackageCommand` signature to accept `Guid? TeacherId = null`.
- Update `CreatePackageCommandHandler` to:
  - Require `ProgramId` explicitly (no fallback to default Program).
  - Resolve the `teacherId` using the current user's profile if they are a Teacher.
  - If the user is Admin/Supervisor: require `TeacherId` explicitly. Return `Fail` if it is null or empty.
  - Validate that the resolved `TeacherId` exists in the database.
  - Query the Program to get its `SubjectId`, and verify that `TeacherSubjects` contains a mapping between the selected `TeacherId` and `SubjectId`. Return `Fail` if they do not match.

#### [MODIFY] [AdminQuestionCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminQuestionCommands.cs)
- Update `CreateQuestionCommand` signature to accept `Guid? TeacherId = null`.
- Update `CreateQuestionCommandHandler` to:
  - Require `SubjectId` explicitly. Return `Fail` if it is null or empty.
  - Resolve the `teacherId` using the current user's profile if they are a Teacher.
  - If the user is Admin/Supervisor: require `TeacherId` explicitly. Return `Fail` if it is null or empty.
  - Verify that the resolved `TeacherId` teaches the selected `SubjectId` by querying `TeacherSubjects`. Return `Fail` if not.

### [Frontend - Services & UI]

#### [MODIFY] [admin-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/admin-service.ts)
- Strictly type the payloads for `createPackage` and `createQuestion` to require `teacherId` and `programId`/`subjectId` respectively.

#### [MODIFY] [teacher-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/teacher-service.ts)
- Add a helper `getTeacherSubjects` or reuse `getTeachers` endpoints to retrieve programs/subjects mapped to the authenticated teacher.

#### [MODIFY] [page.tsx (Admin Content)](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/content/page.tsx)
- Modify the package creation modal form to fetch and display dropdown selections for:
  - Subject/Program (from `/api/admin/programs` or content-service).
  - Teacher (from `/api/admin/teachers` or teacher-service).
- Force selection of both dropdowns before enabling the save button.

#### [MODIFY] [page.tsx (Admin Questions)](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/questions/page.tsx)
- Modify the question creation form to require:
  - Selecting a Subject.
  - Selecting a Teacher (or auto-assign if created in teacher view).

#### [MODIFY] [faq/page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/faq/page.tsx)
- Clean up any Arabic copy that limits the platform coverage to History (التاريخ).

#### [MODIFY] [CircularGallerySection.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/landing/CircularGallerySection.tsx)
- Fetch active teachers dynamically from the `/api/public/teachers` endpoint, only falling back to placeholders if empty.

---

## Phase Closure & Verification Plan

### Automated Tests Required
- Run `.venv/bin/pytest tests/test_operations_tasks.py` and other test files.
- Add a new integration test script `tests/test_multi_teacher_validation.py` to assert that:
  - Admin package creation fails if Teacher or Program is missing.
  - Admin question creation fails if Teacher or Subject is missing.
  - Valid creations map successfully.

### Docker Gate Required
- Rebuild/restart containers using `make down && docker compose build --no-cache && make up && make migrate`.

### Manual QA Required
- Admin login (`20000000000`): create a package, ensure dropdowns are populated and require selection.
- Teacher login (`20000000004`): create package, confirm program dropdown is filtered to their assigned subjects.
