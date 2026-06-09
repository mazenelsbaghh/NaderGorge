# Feature Specification: Graceful HR Warnings & KPI Fixes

**Feature Branch**: `103-graceful-hr-and-kpi-fixes`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Diagnose and resolve the duplicate employee profile warning toasts on loading the admin dashboard page, and resolve the EF Core LINQ group-by translation error causing the KPI loading failure."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Graceful HR Employee Profile Warning (Priority: P1)

As an administrator or staff member without an onboarded employee profile (such as the default root admin user `20000000000`), when I load the Admin Dashboard (`/admin`), I want to see a friendly, inline warning explaining that my employee profile is not configured rather than being bombarded with duplicate red error toasts. Furthermore, I want any clock-in/out and vacation actions to be disabled or hidden to prevent useless error requests.

**Why this priority**: P1 because it directly fixes the broken user experience on the main dashboard load, which currently generates spam error popups for root administrators.

**Independent Test**: Can be tested by logging in as the root admin `20000000000` (who has no employee profile) and verifying that the main dashboard page `/admin` and the personal attendance page `/admin/hr/my-attendance` load without triggering any red toast warnings.

**Acceptance Scenarios**:

1. **Given** I am logged in as an administrator without an employee profile, **When** I navigate to the Admin Dashboard `/admin`, **Then** no toast notifications with error "No employee profile found" are displayed, and the Clock In/Out widget displays a friendly inline alert explaining that my profile is not configured.
2. **Given** I am logged in as an administrator without an employee profile, **When** I navigate to `/admin/hr/my-attendance`, **Then** I see an inline warning banner explaining that my profile is not configured, and the "طلب إجازة" (Request Vacation) button is hidden or disabled.
3. **Given** I am logged in as an employee WITH a configured employee profile, **When** I navigate to `/admin`, **Then** the Clock In/Out widget renders the regular clock-in/out buttons, allowing me to log my shift successfully.

---

### User Story 2 - Translatable Media Production and CRM KPIs (Priority: P1)

As an administrator or manager, when I view the Media Production dashboard or the CRM performance report, I want the system to successfully calculate and load the KPIs (including the editor leaderboard and agent calls performance) without crashing or displaying database query translation errors on the screen.

**Why this priority**: P1 because it fixes a hard crash (500 Bad Request/Internal Server Error) on the media management and CRM pages, restoring essential analytics capabilities.

**Independent Test**: Can be fully tested by opening the Media dashboard `/admin/media` and CRM performance reports, verifying that the indicators load successfully and show editor statistics and call breakdowns correctly.

**Acceptance Scenarios**:

1. **Given** there are active media production pipelines in the database, **When** I request the Media KPI endpoint `/api/admin/media/reports/kpis`, **Then** the backend returns a successful response containing the editor leaderboard stats without throwing a LINQ translation exception.
2. **Given** there are active call logs in the database, **When** I request the CRM Performance Report endpoint `/api/crm/reports/performance`, **Then** the backend returns a successful response with agent call metrics.

---

### Edge Cases

- **No Pipelines/Logs in Database**: If the database has no media production pipelines or CRM call logs, the KPI endpoints must return empty stats gracefully with a 200 OK status instead of crashing.
- **Unauthenticated Requests**: If an unauthenticated user or student tries to access these staff-only endpoints, they must receive a 401 Unauthorized or 403 Forbidden status as normal.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: Log in as Admin (`20000000000` / `password`), load `/admin`. No toast errors should appear. The attendance widget should show a clean, inline Arabic warning message.
- **Manual QA Media Flow**: Navigate to the Media section `/admin/media`. Click the KPIs button (مؤشرات الأداء والتقارير). The KPIs must load successfully, showing the average editing days and editor leaderboard without errors.
- **Docker Acceptance**: Run `docker compose ps` to verify all containers are healthy. Run `docker compose logs backend` after page load to verify no database queries failed compilation or translation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support querying my attendance status and returning a structured object containing `HasProfile` (boolean) and `Logs` (list of logs) instead of just a raw list.
- **FR-002**: The frontend Clock In/Out widget MUST disable action buttons and show an inline alert if `HasProfile` is false.
- **FR-003**: The frontend MUST prevent displaying duplicate error toasts for failed API actions (such as clock-in/out or vacation submissions) by ensuring only the global Axios interceptor displays errors.
- **FR-004**: The system MUST successfully translate and execute all database KPI queries including the Editor Leaderboard and CRM Agent performance queries under EF Core 9.0 without generating runtime LINQ translation exceptions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero (0) duplicate toast notifications displayed on page load or clicking action buttons.
- **SC-002**: 100% of staff KPI dashboard requests load successfully within 500ms under standard local load.
- **SC-003**: All C# and Python automated tests pass without regression.

## Assumptions

- **Existing Authentication**: The existing token-based authentication system will be used.
- **Role Permissions**: Staff permissions (`hr.manage`, `reports.manage`) are configured and checked as normal.
