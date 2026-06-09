# Implementation Plan: Endpoint Alignment and Sidebar Sync

**Branch**: `100-endpoint-alignment-and-sidebar-sync` | **Date**: 2026-06-09 | **Spec**: [specs/100-endpoint-alignment-and-sidebar-sync/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/100-endpoint-alignment-and-sidebar-sync/spec.md)
**Input**: Feature specification from `/specs/100-endpoint-alignment-and-sidebar-sync/spec.md`

## Summary

Conduct a comprehensive review of all frontend and backend endpoints and DTO structures to align schemas and prevent serialization errors. Additionally, sync the admin dashboard sidebar navigation by adding the missing administrative routes and styling the mobile menu modal to support vertical scrolling.

## Technical Context

**Language/Version**: C# (.NET 9, C# 13), TypeScript 5.x, React 19, Next.js 16.2.1  
**Primary Dependencies**: lucide-react, axios, MediatR, EF Core 9  
**Storage**: PostgreSQL  
**Testing**: pytest (local Python test harness)  
**Target Platform**: Docker-compose stack (Linux containers)  
**Project Type**: Monorepo Web Application  
**Performance Goals**: N/A (Admin UX usability and backend alignment)  
**Constraints**: RTL-first styling, standard permission checks, mobile responsiveness  

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Backend Controllers (verification of DTO schema/routes), Frontend Components (`AdminShellChrome.tsx` sidebar sync), Frontend Services (confirm API mappings).
- **Automated tests required**: Verify build completeness of frontend and backend. Run python endpoint tests in `tests/test_operations_tasks.py`.
- **Manual QA flows**: Log in as Admin and verify sidebar links:
  - "المواد الدراسية" (/admin/subjects)
  - "المعلمين" (/admin/teachers)
  - "المالية والرواتب" (/admin/finance)
  - "الموارد البشرية" (/admin/hr)
  - Verify that clicking each navigates successfully and that the mobile drawer menu scrolls vertically without clipping.
  - Verify that students cannot view or access these pages.

---

## Proposed Changes

### [Frontend - Navigation]

#### [MODIFY] [AdminShellChrome.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AdminShellChrome.tsx)
- Import `Library`, `GraduationCap`, `Coins`, and `Users` from `lucide-react`.
- Add route mapping for `/admin/subjects` (المواد الدراسية), `/admin/teachers` (المعلمين), `/admin/finance` (المالية والرواتب), and `/admin/hr` (الموارد البشرية) to the `AdminShellRoute` union type.
- Add these entries to the `navItems` array with their corresponding icons and permissions:
  - `/admin/subjects`: permission `content.manage`, icon `Library`
  - `/admin/teachers`: permission `users.manage`, icon `GraduationCap`
  - `/admin/finance`: permission `users.manage`, icon `Coins`
  - `/admin/hr`: permission `hr.manage`, icon `Users`
- Update the mobile menu `<aside>` container in the TSX (around line 491) by adding the Tailwind CSS classes `max-h-[80vh] overflow-y-auto` to ensure the menu can be scrolled on smaller screens when administrative items overflow.

### [Frontend & Backend - Audit and Schema Alignment]

#### [VERIFY] [API Endpoints Alignment]
- Review properties and models returned by C# API responses vs TS type declarations:
  - **HR**: Verify fields in `EmployeeDto`, `EmployeeProfileDto`, `AttendanceLogDto`, and `VacationDto` in `hr-service.ts` match property names in `AdminHrController.cs` and their MediatR query responses.
  - **Finance**: Verify fields in `PayrollRecordDto`, `AdminPayoutDto`, `TeacherAccountDto`, and `TeacherPayoutDto` in `finance-service.ts` match `AdminFinanceController.cs` and `TeacherFinanceController.cs` DTO classes.
  - **Subjects & Teachers**: Verify fields in `SubjectDto` and `TeacherDto` in `teacher-service.ts` match `AdminController.cs` responses.
- Enums check: Since the backend serializes some enums as strings (due to `JsonStringEnumConverter`) and others as integers, ensure that components handling enums (e.g. status/priority badges) safely parse both string names and numeric values.

---

## Verification Plan

### Automated Tests
- Run backend build verification:
  `dotnet build backend/NaderGorge.sln`
- Run frontend type check and build verification:
  `cd frontend && npm run build`
- Run python regression tests:
  `python3 tests/test_operations_tasks.py`

### Manual Verification
- Rebuild/restart frontend containers:
  `docker compose build admin student landing`
- Log in as Admin (`20000000000`/`password`) and verify the new links appear in the sidebar with proper icons.
- Verify mobile viewport: resize window to trigger bottom menu, click "المزيد", and confirm the modal displays all items and is scrollable.
- Log in as Student (`20000000001`/`password`) and verify that none of the links are visible or accessible.
