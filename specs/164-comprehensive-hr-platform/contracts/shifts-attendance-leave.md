# Contract: Shifts, Attendance and Leave

## Shifts

- `GET/POST/PATCH /api/hr/admin/shifts/templates`
- `POST /api/hr/admin/shifts/assignments/validate` — returns overlap details without write.
- `POST /api/hr/admin/shifts/assignments/publish`
- `POST /api/hr/self/shift-swaps`; manager then HR approval.

Template carries timezone, weekly pattern, segments, grace, break and overtime settings plus attendance policy. Overnight segment explicitly returns `workDateRule`.

## Attendance self-service

- `GET /api/hr/self/attendance/today`
- `POST /api/hr/self/attendance/clock-in`
- `POST /api/hr/self/attendance/breaks/start`
- `POST /api/hr/self/attendance/breaks/{id}/end`
- `POST /api/hr/self/attendance/clock-out`
- `GET /api/hr/self/attendance?from=&to=`
- `POST /api/hr/self/attendance/corrections`

Clock request carries available `{latitude,longitude,accuracy,deviceToken}`. Server resolves policy. Rejected attempts return `403` stable reason and are audited but do not create session.

Errors: `OUTSIDE_GEOFENCE`, `LOCATION_ACCURACY_LOW`, `DEVICE_NOT_TRUSTED`, `NO_SCHEDULE`, `SESSION_ALREADY_OPEN`, `NO_OPEN_SESSION`, `BREAK_ALREADY_OPEN`, `ATTENDANCE_EVENT_REPLAYED`.

## Attendance administration

- `GET /api/hr/admin/attendance/sessions` — scoped and paged.
- `GET /api/hr/admin/attendance/attempts` — permission-gated evidence.
- `GET /api/hr/manager/attendance/corrections` and HR inbox via common approvals.
- `POST /api/hr/admin/attendance/recalculate` — explicit period/employee, dry-run by default.

No endpoint directly edits a session. Approved correction produces a new version and audit before/after.

## Leave

- `GET /api/hr/self/leave/balances`
- `GET/POST /api/hr/self/leave/requests`
- `POST /api/hr/self/leave/requests/{id}/withdraw`
- `GET/POST/PATCH /api/hr/admin/leave/types|policies`
- `GET /api/hr/admin/leave/requests`

Submit response returns computed workdays, reserved amount and approval status. Final approval converts reservation to ledger debit and adds workday classifications; it never creates attendance sessions.

Errors: `LEAVE_BALANCE_INSUFFICIENT`, `LEAVE_OVERLAP`, `ATTACHMENT_REQUIRED`, `LEAVE_POLICY_MISSING`, `LEAVE_ALREADY_REVIEWED`, `LEAVE_WITHDRAWAL_FORBIDDEN`.

## Compatibility

Existing assistant/admin attendance and vacation routes mount shared UI and adapters until each module becomes `NewActive`. Legacy writes are disabled at cutover, and legacy reads remain audit-only during rollback window.
