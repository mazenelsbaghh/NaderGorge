# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Assistant Profile & Egypt Timezone Localization

**Input**: Design documents from `/specs/086-assistant-profile-egypt-time/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md

## Phase 1: Setup & Environment Configuration

- [ ] T001 Configure Docker service timezones by adding `TZ: Africa/Cairo` environment variables under the `environment` section of each service in `docker-compose.yml` (specifically for `landing`, `student`, `admin`, `backend`, `worker`, `db`).

---

## Phase 2: User Story 1 - Assistant Classification & Login Routing (Priority: P1)

**Goal**: Fix the frontend routing, display, and maintenance check to classify users as staff/assistant if they do not hold the "Student" role.

- [ ] T002 Update `frontend/src/components/forms/LoginForm.tsx`:
  - Locate `const isStaff = ['Admin', 'Teacher', 'Assistant'].some((r) => user.roles.includes(r));` (around lines 52-54).
  - Replace with: `const isStaff = !user.roles.includes('Student');`.
- [ ] T003 Update `frontend/src/app/(public)/login/page.tsx`:
  - Locate `const hasAdmin = user?.roles?.some((role) => ['Admin', 'Teacher', 'Assistant'].includes(role));` (around lines 56-58).
  - Replace with: `const hasAdmin = user?.roles?.length && !user.roles.includes('Student');`.
- [ ] T004 Update `frontend/src/components/layout/MaintenanceGuard.tsx`:
  - Locate `const isStaff = user?.roles?.some((role) => ['Admin', 'Teacher'].includes(role));` (around line 38).
  - Replace with: `const isStaff = user?.roles?.length && !user.roles.includes('Student');`.
- [ ] T005 Update `frontend/src/app/admin/users/page.tsx`:
  - Locate `normalizeRole` function (around lines 47-51).
  - Modify to check if `user.roles` includes `Admin`, then `Student`, otherwise return `Assistant` to handle custom roles.
  - Expected code:
    ```typescript
    function normalizeRole(user: AdminUserListDto): UserRole {
      if (user.roles.includes('Admin')) return 'Admin';
      if (user.roles.includes('Student')) return 'Student';
      return 'Assistant';
    }
    ```

**Checkpoint**: Custom assistant users (roles without "Student") can log in, redirect to `/admin`, bypass maintenance page, and display as "تعليمي مساعد" (Assistant) in the admin user list.

---

## Phase 3: User Story 2 - Assistant Profile & Audit Logs (Priority: P2)

**Goal**: Build a backend query to retrieve audit logs for a specific assistant and display them inside a timeline view in a frontend profile drawer/modal.

- [ ] T006 Create `backend/src/NaderGorge.Application/Features/Admin/Queries/GetUserAuditLogsQuery.cs`:
  - Implement a query `GetUserAuditLogsQuery(Guid UserId) : IRequest<ApiResponse<List<UserAuditLogDto>>>;`
  - Implement the DTO `UserAuditLogDto(Guid Id, string Action, string EntityType, Guid? EntityId, string? OldValues, string? NewValues, string? IpAddress, DateTime CreatedAt);`
  - Implement `GetUserAuditLogsQueryHandler` querying `_db.AuditLogs` where `PerformedByUserId == request.UserId`, ordered by `CreatedAt DESC`.
- [ ] T007 Modify `backend/src/NaderGorge.API/Controllers/AdminController.cs`:
  - Add `GetUserAuditLogs` action:
    ```csharp
    [HttpGet("users/{id:guid}/audit-logs")]
    public async Task<IActionResult> GetUserAuditLogs(Guid id)
        => Ok(await _mediator.Send(new GetUserAuditLogsQuery(id)));
    ```
- [ ] T008 Update `frontend/src/services/admin-service.ts`:
  - Add API interface for `UserAuditLogDto`:
    ```typescript
    export interface UserAuditLogDto {
      id: string;
      action: string;
      entityType: string;
      entityId?: string;
      oldValues?: string;
      newValues?: string;
      ipAddress?: string;
      createdAt: string;
    }
    ```
  - Add method `getUserAuditLogs(userId: string): Promise<UserAuditLogDto[]>` fetching `/admin/users/${userId}/audit-logs`.
- [ ] T009 Create `frontend/src/app/admin/users/components/AssistantProfileModal.tsx`:
  - Design a popup modal using `framer-motion` for entry/exit animations.
  - Fetch logs on open using `adminService.getUserAuditLogs`.
  - Render assistant metadata (Name, Phone, custom roles).
  - Render an activity log timeline. Include friendly Arabic translations for action names and render date strings formatted to Egypt Timezone.
- [ ] T010 Integrate `AssistantProfileModal` in `frontend/src/app/admin/users/page.tsx`:
  - Add state `const [selectedAssistant, setSelectedAssistant] = useState<AdminUserListDto | null>(null);`
  - Render `<AssistantProfileModal open={!!selectedAssistant} onClose={() => setSelectedAssistant(null)} assistant={selectedAssistant} />` inside layout.
  - Modify user list row click: if `normalizeRole(u) !== 'Student'`, set `selectedAssistant(u)`.

**Checkpoint**: Clicking an Assistant row opens the profile modal, queries the backend `/audit-logs` endpoint, and displays their actions in Arabic.

---

## Phase 4: User Story 3 - Egypt Timezone Localization (Priority: P3)

**Goal**: Force date-time formatting to use Cairo time for all standard Intl/locale functions to ensure consistent displays.

- [ ] T011 Create `frontend/src/lib/timezone-bootstrap.ts`:
  - Override `Intl.DateTimeFormat` constructor to default `timeZone` to `'Africa/Cairo'` if not specified in options.
  - Override `Date.prototype.toLocaleString`, `Date.prototype.toLocaleDateString`, and `Date.prototype.toLocaleTimeString` to inject `{ timeZone: 'Africa/Cairo' }` when options do not specify it.
- [ ] T012 Update `frontend/src/app/layout.tsx`:
  - Import `@/lib/timezone-bootstrap` at the very top of layout.tsx to ensure it bootstraps both server-side (node) and client-side rendering context.

**Checkpoint**: All date and time elements render aligned with Egypt timezone.

---

## Phase 5: Polish, Build & Verification

- [ ] T013 Verify the build compilation: run `npm run build` inside `frontend/` and build backend project. Fix any warnings or type check errors.
- [ ] T014 Run Quickstart tests manually and document results.
