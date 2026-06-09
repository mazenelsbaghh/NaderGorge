# Implementation Plan: Audit Trail, KPI Dashboards, and Reports

**Branch**: `097-audit-and-reports` | **Date**: 2026-06-09 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/097-audit-and-reports/spec.md)
**Input**: Feature specification from `/specs/097-audit-and-reports/spec.md`

## Summary

This feature expands the central security audit trail to cover all state-changing actions in HR, Tasks, CRM, Payments, Media, Payroll, and Teacher Finance. It also creates a unified Reports Cockpit with date range, role, and user filters.

---

## Technical Context

**Language/Version**: C# (.NET 9), TypeScript (Next.js 16.2.1 / React 19)  
**Primary Dependencies**: MediatR, Entity Framework Core 9, framer-motion, lucide-react  
**Storage**: PostgreSQL (AuditLogs, AttendanceLogs, TaskItems, CrmCallLogs, MediaProductionPipelines, PayrollRecords tables)  
**Testing**: pytest (tests/test_audit_and_reports.py) and backend unit tests  
**Target Platform**: Linux Docker / macOS  
**Project Type**: Web Service / Frontend dashboard  
**Performance Goals**: Reports dashboard API aggregates 10,000 logs in under 1 second  
**Constraints**: Zero secret credentials (passwords, tokens) recorded in audit logs  

---

## Constitution Check

- **Backend**: Update state-changing handlers to inject `IAuditRepository` or write directly to `_db.AuditLogs`. Create MediatR queries for Admin Audit logs and Admin KPI reports. Add reports endpoints in `AdminReportsController.cs`.
- **Frontend**: Create `report-service.ts` client. Create the reports cockpit view `/admin/reports/page.tsx` with tabs for central audit trail table and visual KPI charts.
- **Worker**: No modifications required.
- **Database**: Mapped entities exist; no new database migrations are required (since `AuditLog` table and other tables already exist).
- **Docker**: Run `make up` and test suite in clean container.

---

## Project Structure

### Documentation

```text
specs/097-audit-and-reports/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── tasks.md             # Phase 2 output
```

### Source Code

```text
backend/
├── src/NaderGorge.API/Controllers/AdminReportsController.cs
└── src/NaderGorge.Application/Features/Admin/Reports/
    ├── Queries/
    │   ├── GetAdminAuditLogsQuery.cs
    │   └── GetAdminKpiDashboardQuery.cs
    └── Dtos/
        ├── AuditLogDetailDto.cs
        └── KpiDashboardDto.cs

frontend/
├── src/services/report-service.ts
└── src/app/admin/reports/page.tsx
```

---

## Phase Closure & Verification Plan

**Automated Tests Required**:
- Backend tests verifying Audit log writes on Task, CRM, and Media commands.
- Python integration tests in `tests/test_audit_and_reports.py` checking the reports endpoint returns correct data and denies access to non-admin roles.

**Docker Gate Required**:
- `make up`
- `node scripts/generate-endpoint-inventory.mjs --check`
- `tests/venv/bin/python -m pytest tests/test_audit_and_reports.py -v`

**Manual QA Required**:
- Verify that editing employee profiles logs to the Audit Trail.
- Verify that filtering the visual KPI charts works cleanly without console errors.
- Verify that passwords and tokens are redacted from old/new logged values.
