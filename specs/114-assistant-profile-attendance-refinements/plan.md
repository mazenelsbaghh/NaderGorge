# Technical Implementation Plan: Assistant Profile & Attendance Refinements

This plan details the implementation of a single daily check-in validation check, an early checkout confirmation dialog/warning in the frontend, and a dedicated, full profile page for assistants under the admin panel.

## Proposed Changes

---

### Backend Components (C# / .NET 9 API & Core)

#### [MODIFY] [ClockInCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/HR/Commands/ClockInCommand.cs)
- Query the database to see if any `AttendanceLog` exists for the employee with the same `Date` (Cairo local date calculated using `Egypt Standard Time` or `Africa/Cairo` timezone).
- If a record exists, throw an `InvalidOperationException` with the message `"لقد قمت بتسجيل الحضور بالفعل اليوم."` (You have already checked in today) to block multiple sessions.

#### [MODIFY] [GetMyAttendanceQuery.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/HR/Queries/GetMyAttendanceQuery.cs)
- Retrieve `profile.TargetDailyHours` from the `EmployeeProfile`.
- Expose `TargetDailyHours` in the response DTO `MyAttendanceStatusDto`.
- Update the constructor and mapping logic to populate `TargetDailyHours` (default to `8` if the profile is not found or null).

---

### Frontend Components (Next.js / React 19)

#### [MODIFY] [hr-service.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/services/hr-service.ts)
- Add `targetDailyHours?: number;` to the `MyAttendanceStatusDto` interface.

#### [MODIFY] [ClockInOutWidget.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/ClockInOutWidget.tsx)
- Fetch `targetDailyHours` from the attendance response and store it in state (defaulting to `8`).
- In `handleClockOut`:
  - Calculate the elapsed working hours:
    `const elapsedHours = (Date.now() - new Date(activeSession.clockIn).getTime()) / (1000 * 60 * 60);`
  - If `elapsedHours < targetDailyHours`, display a confirmation dialog using browser `window.confirm` or a custom styling warning telling the user:
    `تحذير: لم تكمل ساعات العمل المطلوبة اليوم بعد (${targetDailyHours} ساعات). هل أنت متأكد من تسجيل الانصراف؟`
  - If the user cancels the confirmation, abort the operation. If they accept, proceed with the checkout API call.

#### [MODIFY] [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/assistants/page.tsx)
- Update the table's `onRowClick` handler: instead of setting `selectedAssistant` state to open `AssistantProfileModal`, navigate to `/admin/assistants/${u.id}` using Next.js `useRouter`.

#### [NEW] [page.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/admin/assistants/%5Bid%5D/page.tsx)
- Create a brand new, premium full-page component `/admin/assistants/[id]`.
- Fetch assistant basic user details (`adminService.listUsers`), employee settings (`hrService.listEmployees`), attendance records (`hrService.getAttendance`), audit logs (`adminService.getUserAuditLogs`), and vacations (`hrService.getVacations`).
- Show the following sections:
  1. **Profile Card**: Avatar initials, Full Name, Phone, Role, Date Joined, Status (with toggle button to Suspend/Activate).
  2. **Employee Settings (Salary & Hours)**: Displays Basic Salary, Standard Start Time, Target Daily Hours, with an inline form to edit and save changes via `hrService.saveEmployeeProfile`.
  3. **Attendance Log**: A table showing all the assistant's check-ins and check-outs with duration, late minutes, status, IP, and user agent.
  4. **Activity Logs**: A vertical timeline showing the audit events performed by the assistant.
  5. **Vacations**: A table showing all vacation requests submitted by the assistant with inline Approve/Reject action buttons.

## Verification Plan

### Automated Tests
- Build and compile frontend and backend to check for zero compilation errors.
- Run existing C# application unit tests for attendance logic:
  - `dotnet test backend/tests/NaderGorge.Application.Tests/HR/AttendanceTests.cs`

### Manual Verification
1. Open the assistant dashboard and clock in/out twice. Verify the second attempt is blocked and displays an error toast.
2. Clock in and immediately click clock out. Verify the early checkout confirmation dialog appears and correctly blocks or allows checkout.
3. Open the admin assistants management page, click on any assistant, and verify redirection to `/admin/assistants/[id]`.
4. Update the assistant's standard start time and daily target hours, save, and confirm that the profile page reflects the new values.
