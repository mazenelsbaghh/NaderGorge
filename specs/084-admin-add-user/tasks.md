# Tasks: 084 — Admin Add User

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification
- [x] Phase 2: Technical Planning
- [x] Phase 3: Detailed Task Breakdown

---

## Backend Tasks

### T1: AdminCreateUserCommand
- [ ] Create `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminCreateUserCommand.cs`
  - Record: `AdminCreateUserCommand(FullName, PhoneNumber, Password, Role, List<Guid> PackageIds)`
  - Result: `AdminCreateUserResult(Id, FullName, PhoneNumber, Role)`
  - Handler: validate unique phone → hash password → create User → assign Role → create StudentProfile if Student → create StudentAccessGrant per packageId → SaveChanges

### T2: GetAdminPackagesListQuery
- [ ] Create `backend/src/NaderGorge.Application/Features/Admin/Queries/GetAdminPackagesListQuery.cs`
  - Query: `GetAdminPackagesListQuery`
  - Result: `AdminPackageListItemDto(Guid Id, string Name)`
  - Handler: select all packages from `_context.Packages` ordered by Name

### T3: AdminController endpoints
- [ ] In `AdminController.cs` add:
  - `POST /admin/users` → `AdminCreateUserCommand`
  - `GET /admin/packages/list` → `GetAdminPackagesListQuery`

---

## Frontend Tasks

### T4: admin-service.ts additions
- [ ] Add `AdminCreateUserPayload` interface
- [ ] Add `AdminPackageListItemDto` interface
- [ ] Add `adminService.createUser(payload)` → `POST /admin/users`
- [ ] Add `adminService.listAllPackages()` → `GET /admin/packages/list`

### T5: AddUserDrawer component
- [ ] Create `frontend/src/app/admin/users/components/AddUserDrawer.tsx`
  - Role segmented selector (Admin | مساعد | طالب)
  - Full Name input
  - Phone input
  - Password input with show/hide toggle
  - Packages multi-select (show when role=Student, fetch from listAllPackages)
  - Submit with loading state
  - On success: call onSuccess() + toast.success

### T6: Wire up in page.tsx
- [ ] Add `showAddUser` state
- [ ] Replace dead NeumorphButton with one that opens AddUserDrawer
- [ ] After success: re-fetch users list

---

## Verification
- [ ] `dotnet build` passes clean
- [ ] Add Admin user → appears in list
- [ ] Add Student with packages → packages shown in student profile
- [ ] Duplicate phone → inline error shown
