# Tasks: Graceful HR Warnings & KPI Fixes

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification
- [x] Phase 2: Technical Planning
- [x] Phase 3: Detailed Task Breakdown

## Part 1: SQL Translation Fixes (P1)

### Task 1.1: Fix GetMediaKpisQuery EF Core GroupBy Translation
- [ ] Modify `backend/src/NaderGorge.Application/Features/Admin/Media/Queries/GetMediaKpisQuery.cs` lines 52-64:
  - Query group stats on `MediaProductionPipelines` grouped by `AssignedAgentId` only.
  - Query matching `Users`' full names based on the editor IDs.
  - Combine the groups and user names in-memory.
  - Order by `TotalProduced` descending in-memory.
- [ ] Verify compilation of `GetMediaKpisQuery.cs`.

### Task 1.2: Fix GetCrmPerformanceReportQuery EF Core GroupBy Translation
- [ ] Modify `backend/src/NaderGorge.Application/Features/CRM/Queries/GetCrmPerformanceReportQuery.cs` lines 72-82:
  - Query group stats on `CrmCallLogs` grouped by `AgentId`.
  - Fetch corresponding full names from `Users`.
  - Combine in-memory.
- [ ] Verify compilation of `GetCrmPerformanceReportQuery.cs`.

## Part 2: Graceful HR Warnings & API Alignment (P1)

### Task 2.1: Update GetMyAttendanceQuery DTO and Handler
- [ ] In `backend/src/NaderGorge.Application/Features/HR/Queries/GetMyAttendanceQuery.cs`:
  - Define `public record MyAttendanceStatusDto(bool HasProfile, List<AttendanceLogDto> Logs);`
  - Update `GetMyAttendanceQuery` to request `ApiResponse<MyAttendanceStatusDto>`.
  - Update `GetMyAttendanceQueryHandler.Handle` to return `MyAttendanceStatusDto`. If profile is null, return `hasProfile: false` and empty list of logs. Otherwise return `hasProfile: true` and logs.

### Task 2.2: Update HrController GetMyAttendance Response Type
- [ ] In `backend/src/NaderGorge.API/Controllers/HrController.cs` line 56:
  - Update return type of `GetMyAttendance` to return `ApiResponse<MyAttendanceStatusDto>`.

### Task 2.3: Align frontend hr-service types
- [ ] In `frontend/src/services/hr-service.ts`:
  - Add `export interface MyAttendanceStatusDto { hasProfile: boolean; logs: AttendanceLogDto[]; }`
  - Update `getMyAttendance` signature to return `Promise<MyAttendanceStatusDto>`.

### Task 2.4: Update ClockInOutWidget UI Warnings and catch blocks
- [ ] In `frontend/src/components/admin/ClockInOutWidget.tsx`:
  - Add state `hasProfile` (default true).
  - Update `checkActiveSession` to read `hasProfile` and `logs` from `getMyAttendance()` response.
  - In `render`, if `hasProfile` is false, display a premium Arabic inline card warning that the employee profile is unconfigured, and hide/disable clock-in/out controls.
  - Remove `toast.error` from the `catch` blocks in `handleClockIn` and `handleClockOut` (only keep `setActionLoading(false)` or state updates).

### Task 2.5: Remove duplicate toast in VacationRequestModal
- [ ] In `frontend/src/components/admin/VacationRequestModal.tsx`:
  - Remove `toast.error` from the `catch` block of `submitVacation` call.

### Task 2.6: Update My Attendance page for missing profiles
- [ ] In `frontend/src/app/admin/hr/my-attendance/page.tsx`:
  - Update `fetchLogs` to read `response.logs` and `response.hasProfile`.
  - Add state `hasProfile`.
  - If `hasProfile` is false, show an inline warning banner explaining that the employee profile is not configured, and hide the "طلب إجازة" (Request Vacation) button.

## Part 3: Quality Gates & Verification

- [ ] Execute Quality Gate 1: `clean-code-guard` against all modified production code.
- [ ] Execute Quality Gate 2: `test-guard` against changed test files (if any).
- [ ] Verify warning-free compilation of backend C# and frontend TypeScript.
- [ ] Run backend tests `dotnet test` and python tests `pytest` to confirm success.
