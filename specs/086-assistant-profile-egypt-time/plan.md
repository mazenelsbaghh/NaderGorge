# Technical Implementation Plan: Assistant Profile & Egypt Timezone Localization

**Branch**: `086-assistant-profile-egypt-time` | **Date**: 2026-06-06 | **Spec**: [specs/086-assistant-profile-egypt-time/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/086-assistant-profile-egypt-time/spec.md)
**Input**: Feature specification from `/specs/086-assistant-profile-egypt-time/spec.md`

## Summary

This plan addresses:
1. Fixing the assistant user classification on the frontend so custom assistant accounts are treated as staff instead of defaulting to students.
2. Implementing an API endpoint to retrieve audit logs for a given user and a frontend timeline view (Assistant Profile Modal) to display actions performed by the assistant.
3. Localizing the entire application's date-time formatting to Egypt time (`Africa/Cairo`) by overriding global formatting constructors in the client bundle and setting container timezones (`TZ=Africa/Cairo`) in Docker.

---

## Technical Context

**Language/Version**: C# 13 (.NET 9) Backend, TypeScript 5.x (Next.js 16.2.1 / React 19) Frontend  
**Primary Dependencies**: MediatR, EF Core, Framer Motion, `@tailwindcss/postcss`  
**Storage**: PostgreSQL (EF Core `AuditLogs` table)  
**Testing**: Playwright E2E tests, native build checks  
**Target Platform**: Linux server, Docker containers  
**Project Type**: Web service & application  
**Performance Goals**: Audit timeline endpoint latency < 200ms  
**Constraints**: Absolute adherence to Egypt Time (`Africa/Cairo`), RTL layout, custom CSS styling tokens

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Clean Architecture**: Handled. Backend query will be placed inside `NaderGorge.Application` and exposed in `AdminController.cs`. Service layer calls in frontend will be isolated inside `admin-service.ts`.
- **Security & Access Control**: The new audit endpoint will require the user to have Admin or Assistant roles.
- **Curated Archive Design**: The modal will leverage CSS design variables (`--admin-card`, `--admin-border`, etc.) with Cairo font and Arabic localization.

---

## Project Structure

### Documentation (this feature)

```text
specs/086-assistant-profile-egypt-time/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Research and architectural details
├── data-model.md        # API contracts & DTOs
└── quickstart.md        # Verification guide
```

### Source Code Modifications

#### Backend
- `backend/src/NaderGorge.Application/Features/Admin/Queries/GetUserAuditLogsQuery.cs` [NEW]
- `backend/src/NaderGorge.API/Controllers/AdminController.cs` [MODIFY]

#### Frontend
- `frontend/src/lib/timezone-bootstrap.ts` [NEW]
- `frontend/src/app/layout.tsx` [MODIFY]
- `frontend/src/components/forms/LoginForm.tsx` [MODIFY]
- `frontend/src/app/(public)/login/page.tsx` [MODIFY]
- `frontend/src/components/layout/MaintenanceGuard.tsx` [MODIFY]
- `frontend/src/services/admin-service.ts` [MODIFY]
- `frontend/src/app/admin/users/page.tsx` [MODIFY]
- `frontend/src/app/admin/users/components/AssistantProfileModal.tsx` [NEW]

#### Docker Configuration
- `docker-compose.yml` [MODIFY]

---

## Proposed Changes

### 1. Timezone Override Bootstrap (Frontend)
Create [timezone-bootstrap.ts](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/lib/timezone-bootstrap.ts) to intercept `Intl.DateTimeFormat` and `Date.prototype.toLocaleString` methods, inserting `timeZone: 'Africa/Cairo'` when `timeZone` is omitted.
Import this file at the top of [layout.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/layout.tsx).

### 2. User Roles Fix (Frontend)
- Update `LoginForm.tsx` line 52 to check `const isStaff = !user.roles.includes('Student');`.
- Update `login/page.tsx` line 56 to check `const hasAdmin = user?.roles?.length && !user.roles.includes('Student');`.
- Update `MaintenanceGuard.tsx` line 38 to check `const isStaff = user?.roles?.length && !user.roles.includes('Student');`.
- Update `normalizeRole` in `users/page.tsx`:
  ```typescript
  function normalizeRole(user: AdminUserListDto): UserRole {
    if (user.roles.includes('Admin')) return 'Admin';
    if (user.roles.includes('Student')) return 'Student';
    return 'Assistant';
  }
  ```

### 3. Retrieve Audit Logs Query (Backend)
Implement `GetUserAuditLogsQuery` and handler returning audit entries performed by the target user ID, filtered by `PerformedByUserId`. Add `[HttpGet("users/{id:guid}/audit-logs")]` in `AdminController.cs`.

### 4. Assistant Profile Drawer/Modal (Frontend)
Create `AssistantProfileModal.tsx` to display the timeline of activities. Add a button in the row actions or row click in `users/page.tsx` to toggle the modal open.
Update `admin-service.ts` to include the `getUserAuditLogs` service call.

### 5. Timezone Alignment (Docker)
Set `TZ: Africa/Cairo` environment variable on all containers inside `docker-compose.yml`.

---

## Verification Plan

See [quickstart.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/086-assistant-profile-egypt-time/quickstart.md) for detailed validation guidelines.
