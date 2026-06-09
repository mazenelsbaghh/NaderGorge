# Feature Specification: Audit Trail, KPI Dashboards, and Operational Reports

**Feature Branch**: `097-audit-and-reports`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 10 - Audit Trail, KPI Dashboards, and Operational Reports"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Central Audit Trail (Priority: P1)

Admins and Supervisors need a secure, central audit ledger to monitor sensitive actions across the platform. Every state-changing operation in HR (attendance modifications, vacations), Tasks (status changes, assignments), CRM (calls logged, outcomes), Payments (matching, overrides), Media (pipeline transitions), Payroll (adjustment changes, month approvals), and Teacher Finance (payout approvals) must write an immutable trace. 

**Why this priority**: Required for regulatory compliance, security audits, and diagnosing operational errors or unauthorized modifications.

**Independent Test**: An Admin performs a payroll adjustment or changes an employee profile. The Admin opens the Audit Trail dashboard and verifies a new entry appears detailing who performed the change, what was changed, the old/new values in readable JSON format, and confirms that no passwords or security tokens are exposed in the logged values.

**Acceptance Scenarios**:

1. **Given** an Admin is authenticated with proper permissions, **When** they make a modification to an employee's salary in Payroll, **Then** an audit entry is created logging "ModifyPayroll", entity "PayrollRecord", with the old/new salaries, the admin's user identifier, and the current timestamp.
2. **Given** any system user performs an action that updates passwords, secrets, or balance codes, **When** the audit log is recorded, **Then** the payload values for those specific fields are automatically scrubbed or marked as `[REDACTED]`.

---

### User Story 2 - Operational KPI Dashboard (Priority: P1)

Admins and Supervisors need a unified cockpit showing key performance indicators (KPIs) of the business operations to optimize resources and track execution speeds.

**Why this priority**: Empowers management to identify process bottlenecks (e.g., late attendance, CRM failures, media editing delays) and track payment/financial health in real-time.

**Independent Test**: The Admin opens the Reports Cockpit. They can view summary statistics and charts for Attendance rates, Task completion rates vs Overdue items, Call Center outcome distributions, Media production average delay times, Payment matching percentages, and Payroll monthly statuses. They apply filters by a Date Range and confirm the KPIs update dynamically to reflect only records within that date range.

**Acceptance Scenarios**:

1. **Given** the operational database has logged CRM calls, employee attendance logs, and media pipeline states, **When** the Admin views the reports dashboard, **Then** they see visual summary metrics (e.g., % Present, % Tasks Completed, Average Days in Production, % Auto-matched payments).
2. **Given** the Admin applies a date filter of the last 7 days, **When** the dashboard loads, **Then** the KPIs are recalculated using only transactions completed within the last 7 days.

---

### Edge Cases

- **Secrets and Credentials Handling**: If a user updates their password or a package code contains a coupon token, writing these to the audit ledger would violate security compliance. The system must intercept and filter out these sensitive fields.
- **Orphaned Entities**: If a task or media pipeline item is deleted, the audit log entries relating to it must not cause foreign key crashes or fail to load. The UI must display the log using the last recorded name or a "Deleted Entity" placeholder alongside the raw UUID.
- **Massive Date Ranges**: If an Admin queries a report spanning multiple years, the system must optimize query aggregation to prevent timeouts, fallback to pagination, or restrict query bounds to a maximum of 12 months per query.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Flow 1**: Admin updates an employee profile. Admin opens `/admin/reports` (Audit Trail tab), searches for the user, and verifies that the change is logged with IP address, user name, and the specific fields changed.
- **Manual QA Flow 2**: Admin goes to `/admin/reports` (KPI tab) and applies a date filter. Verifies that the metrics charts dynamically refresh.
- **Manual QA Negative Check**: A Student or Teaching Assistant attempts to access the reports API (`/api/admin/reports` or `/api/admin/audit`) or view the page, and receives a 403 Forbidden.
- **Docker Acceptance**: Verify that running `make up` followed by database migrations works cleanly without table conflicts and `python3 -m pytest tests/test_audit_and_reports.py -q` completes successfully.
- **External Dependencies**: None. Uses local PostgreSQL database store.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST write an audit entry for all state changes in HR, Tasks, CRM, Payments, Media, Payroll, and Teacher Finance.
- **FR-002**: System MUST sanitize sensitive fields (e.g., passwords, authorization tokens, reset tokens) in audit values.
- **FR-003**: System MUST expose a paginated and filterable endpoint for audit logs accessible only to Admin and Supervisor roles.
- **FR-004**: System MUST expose a report API aggregating operational metrics (Attendance, Tasks, CRM, Media, Payments, Payroll).
- **FR-005**: Reports API MUST support query parameters for Date Range (startDate, endDate), Role, and Employee/Teacher ID.
- **FR-006**: Frontend reports page MUST display clean visual dashboards using unified charts and data tables.

### Key Entities *(include if feature involves data)*

- **AuditLog**:
  - `Action` (string): Type of action performed.
  - `EntityType` (string): Affected entity name.
  - `EntityId` (UUID): Identifier of the affected record.
  - `PerformedByUserId` (UUID): Reference to the user who ran the action.
  - `OldValues` (JSON/string): Redacted pre-state representation.
  - `NewValues` (JSON/string): Redacted post-state representation.
  - `IpAddress` (string): Client IP address.
  - `CreatedAt` (DateTime): Log generation date.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of state-modifying endpoints in the target subsystems produce an AuditLog entry.
- **SC-002**: Reports cockpit dashboard loads and renders charts in under 1.5 seconds under a test set of 10,000 logs.
- **SC-003**: 0% leakage of password hashes or access tokens into the AuditLog table.
- **SC-004**: 100% block rate (HTTP 403) for non-admin accounts attempting to hit report endpoints.

## Assumptions

- Audit logs are written synchronously/asynchronously inside database transactions to ensure database consistency.
- No historical data reconstruction is needed for actions completed before Phase 10's release.
- Frontend rendering will use the existing dashboard styles, colors, and layouts defined in the design system.
