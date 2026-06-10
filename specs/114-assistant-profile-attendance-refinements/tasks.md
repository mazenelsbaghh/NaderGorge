# Implementation Tasks: Assistant Profile & Attendance Refinements

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

## Backend Implementation Tasks (C#)

### Task 1: Prevent duplicate attendance logs on the same calendar day
- **File**: [ClockInCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/HR/Commands/ClockInCommand.cs)
- **Action**: Modify `ClockInCommandHandler.Handle` to fetch `localDate` (under Egypt timezone) and check if any `AttendanceLog` already exists for `profile.Id` on that date. If so, throw an `InvalidOperationException` with message `"لقد قمت بتسجيل الحضور بالفعل اليوم."`.

### Task 2: Include TargetDailyHours in GetMyAttendanceQuery
- **File**: [GetMyAttendanceQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/HR/Queries/GetMyAttendanceQuery.cs)
- **Action**:
  - Update `MyAttendanceStatusDto` record definition:
    `public record MyAttendanceStatusDto(bool HasProfile, List<AttendanceLogDto> Logs, int TargetDailyHours);`
  - In `GetMyAttendanceQueryHandler.Handle`, pass `profile.TargetDailyHours` (or default to `8` if `profile` is null) to `MyAttendanceStatusDto`.

---

## Frontend Implementation Tasks (TypeScript / React)

### Task 3: Update hr-service.ts interface
- **File**: [hr-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/hr-service.ts)
- **Action**: Add `targetDailyHours?: number;` to the `MyAttendanceStatusDto` interface.

### Task 4: Add early checkout warning in ClockInOutWidget
- **File**: [ClockInOutWidget.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/ClockInOutWidget.tsx)
- **Action**:
  - Store `targetDailyHours` state (defaulting to 8) from the `hrService.getMyAttendance()` response.
  - In `handleClockOut`:
    - Calculate elapsed hours: `const elapsedHours = (Date.now() - new Date(activeSession.clockIn).getTime()) / (1000 * 60 * 60);`
    - If `elapsedHours < targetDailyHours`, display a confirmation dialog using `window.confirm`:
      `تحذير: لم تكمل ساعات العمل المطلوبة اليوم بعد (${targetDailyHours} ساعات). هل أنت متأكد من تسجيل الانصراف؟`
    - If user cancels, abort the request. If they click OK, proceed to clock out.

### Task 5: Route assistants list table to full profile page
- **File**: [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/assistants/page.tsx)
- **Action**:
  - Import `useRouter` from `next/navigation`.
  - Update `onRowClick` of `AdminDataTable` to call `router.push(`/admin/assistants/${u.id}`)` instead of setting `selectedAssistant`.

### Task 6: Implement Full Assistant Profile Page
- **File**: [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/assistants/%5Bid%5D/page.tsx) [NEW]
- **Action**: Create a new dynamic route for the assistant profile. Fetch details for the assistant with user ID `id`:
  - Fetch basic user profile from `adminService.listUsers` matching the ID.
  - Fetch employee settings from `hrService.listEmployees` matching the ID.
  - Fetch attendance logs from `hrService.getAttendance` with search matching phone number.
  - Fetch audit logs from `adminService.getUserAuditLogs`.
  - Fetch vacations list from `hrService.getVacations` with search matching phone number.
  - Show a tabbed or grid layout containing Profile details (with active status toggle), Job/Salary profile configuration (with inline edit + save button calling `hrService.saveEmployeeProfile`), Attendance table, Activity timeline, and Vacation list with inline Approve/Reject action buttons.

---

## Quality Gates & Verification Tasks

### Task 7: Clean Code Guard Check
- **Action**: Run the `clean-code-guard` check on all modified production-code files (C# and TS/TSX) before testing.

### Task 8: Test Guard Check
- **Action**: Run `test-guard` to audit modified backend test files.
- **Test Command**: `dotnet test backend/tests/NaderGorge.Application.Tests/HR/AttendanceTests.cs`

### Task 9: Final Compilation and Verification Check
- **Action**: Verify the entire project builds successfully and execute manual QA tests to confirm everything works perfectly.
