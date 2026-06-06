ـ# Tasks: Admin Roles and Settings / الأدوار وإعدادات المنصة

**Input**: Design documents from `/specs/085-admin-roles-settings/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Database schema extension and core authorization filter infrastructure

- [ ] T001 Backend: Add `PermissionsJson` property to `Role` entity in `backend/src/NaderGorge.Domain/Entities/Role.cs`.
- [ ] T002 Backend: Configure `PermissionsJson` mapping in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs` model configuration.
- [ ] T003 Backend: Generate and apply EF Core database migration `AddPermissionsToRole`.
- [ ] T004 Backend: Add new platform settings keys (`PlatformName`, `SupportPhoneNumber`, `SupportWhatsAppUrl`, `YouTubeChannelUrl`, `TelegramChannelUrl`, `MaxActiveDevicesPerStudent`, `EnableWatermark`, `WatermarkOpacity`, `MaintenanceMode`, `MaintenanceMessage`) to `PlatformSettingKeys.cs` and `CachedPlatformSettings.cs`.
- [ ] T005 Backend: Update `GetPlatformSettingsQuery.cs` to ensure defaults for all new settings.
- [ ] T006 Backend: Implement custom `HasPermissionAttribute` and `PermissionFilter` in `backend/src/NaderGorge.API/Extensions/HasPermissionAttribute.cs` for endpoint authorization.
- [ ] T007 Backend: Update `TokenService.cs` to read role permissions and append them as `permission` claims in the JWT token.

**Checkpoint**: Foundation ready - DB model and token-based claims are ready.

---

## Phase 2: User Story 1 - Platform Settings Management

**Goal**: Full-scale system settings management UI and endpoints, including student maintenance page redirection.

**Independent Test**: Admin modifies settings in `/admin/settings`, clicks save, and sees success. When Maintenance Mode is enabled, student access to `/student/*` is blocked.

- [ ] T008 Backend: In `LoginCommand.cs`, replace the configuration-based device limit check with `ICachedPlatformSettingsReader` dynamic `MaxActiveDevicesPerStudent` check.
- [ ] T009 Backend: Add a public settings endpoint `GET api/public/settings` in `PublicController.cs` to expose platform info and maintenance status.
- [ ] T010 Frontend: Add settings and roles endpoints (GET, PUT, POST, DELETE) in `frontend/src/services/admin-service.ts`.
- [ ] T011 Frontend: Refactor `frontend/src/app/admin/settings/page.tsx` to support a Tab-based interface: "إعدادات المنصة" (Platform Settings) and "الأدوار والصلاحيات" (Roles & Permissions).
- [ ] T012 Frontend: Implement all settings inputs (Platform Info, Support Links, Device Limits, Watermark, Maintenance Mode) on the settings tab with a premium layout matching the Cairo design system.
- [ ] T013 Frontend: Implement `MaintenanceGuard.tsx` and integrate it into `StudentGuard.tsx` or student layout to block student access with a beautiful pharaonic-themed layout when maintenance mode is active.

**Checkpoint**: Platform Settings are fully operational.

---

## Phase 3: User Story 2 - Roles & Permissions Creation

**Goal**: Dynamic Roles CRUD operations and authorization checks.

**Independent Test**: Admin creates custom role "مساعد مراجعة" with specific permissions, saves it, and can see it in the roles table.

- [ ] T014 Backend: Create MediatR queries/commands for roles:
  - `ListRolesQuery` in `Features/Admin/Queries/ListRolesQuery.cs`
  - `CreateRoleCommand` in `Features/Admin/Commands/CreateRoleCommand.cs`
  - `UpdateRoleCommand` in `Features/Admin/Commands/UpdateRoleCommand.cs`
  - `DeleteRoleCommand` in `Features/Admin/Commands/DeleteRoleCommand.cs`
- [ ] T015 Backend: Add dynamic roles CRUD endpoints in `AdminController.cs` (`GET /admin/roles`, `POST /admin/roles`, `PUT /admin/roles/{id}`, `DELETE /admin/roles/{id}`).
- [ ] T016 Backend: Add `[HasPermission]` checks across all admin controller endpoints to secure them using granular permission claims.
- [ ] T017 Frontend: Implement the "الأدوار والصلاحيات" tab list view in `settings/page.tsx` displaying all existing roles and their permissions.
- [ ] T018 Frontend: Build a sliding drawer/modal for adding/editing a role with a checklist of the 8 predefined permissions.

**Checkpoint**: Roles and permissions can be managed dynamically.

---

## Phase 4: User Story 3 - Select Custom Role when Adding User

**Goal**: Bind custom roles to users during creation/editing.

**Independent Test**: Admin adds a new assistant user and selects a custom role from the dynamic list.

- [ ] T019 Backend: In `AdminCreateUserCommand.cs`, remove the hardcoded roles array check and load valid roles dynamically from the database `Roles` table. If the role's Type is Student, run student profile creation, else ignore.
- [ ] T020 Frontend: Update `AddUserDrawer.tsx` to fetch dynamic roles from `GET /admin/roles` when adding Admin/Assistant accounts.

**Checkpoint**: Users can be created with dynamic custom roles.

---

## Phase 5: Polish & Verification

**Purpose**: Code quality, compilation, and system-wide verification

- [ ] T021 Backend: Build and check for any warnings or compilation errors.
- [ ] T022 Frontend: Check linting and build outputs.
- [ ] T023 Run manual verification plan as specified in `quickstart.md`.
