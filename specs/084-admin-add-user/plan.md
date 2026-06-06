# Technical Implementation Plan: 084 — Admin Add User

## Architecture Overview

### Backend (C# / .NET 9 / MediatR / EF Core)
```
AdminController  →  AdminCreateUserCommand  →  IAppDbContext
AdminController  →  GetAdminPackagesListQuery  →  IAppDbContext
```

### Frontend (Next.js / TypeScript / React 19)
```
/admin/users/page.tsx
  └─ AddUserDrawer (new component)
       ├─ AdminModal (reuse existing)
       ├─ PackageMultiSelect (inline, only for Student role)
       └─ adminService.createUser() / adminService.listAllPackages()
```

---

## Backend Changes

### [NEW] `AdminCreateUserCommand.cs`
Path: `backend/src/NaderGorge.Application/Features/Admin/Commands/AdminCreateUserCommand.cs`

```csharp
public record AdminCreateUserCommand(
    string FullName,
    string PhoneNumber,
    string Password,
    string Role,           // "Admin" | "Assistant" | "Student"
    List<Guid> PackageIds  // optional, for Student
) : IRequest<ApiResponse<AdminCreateUserResult>>;

public record AdminCreateUserResult(Guid Id, string FullName, string PhoneNumber, string Role);
```

Handler logic:
1. Validate phone uniqueness
2. Hash password via `BCrypt.Net.BCrypt.HashPassword`
3. Create `User` entity
4. Find `Role` entity by name, create `UserRole`
5. If Student: create minimal `StudentProfile`
6. If PackageIds provided: create `StudentPackageAccess` records (check PackageAccess entity structure)
7. SaveChanges
8. Return result

### [NEW] `GetAdminPackagesListQuery.cs`
Path: `backend/src/NaderGorge.Application/Features/Admin/Queries/GetAdminPackagesListQuery.cs`

```csharp
public record GetAdminPackagesListQuery : IRequest<ApiResponse<List<AdminPackageListItem>>>;
public record AdminPackageListItem(Guid Id, string Name);
```

Handler: Select all packages with Id + Name from `Packages` table.

### [MODIFY] `AdminController.cs`
- Add `POST /admin/users` → `AdminCreateUserCommand`
- Add `GET /admin/packages/list` → `GetAdminPackagesListQuery`

---

## Frontend Changes

### [MODIFY] `admin-service.ts`
Add:
```ts
createUser(payload: AdminCreateUserPayload): Promise<AdminCreateUserResult>
listAllPackages(): Promise<AdminPackageListItem[]>
```

### [NEW] `AddUserDrawer.tsx`
Path: `frontend/src/app/admin/users/components/AddUserDrawer.tsx`

UI Design:
- Slides in from the right (uses `AdminModal` with custom width or a side panel)
- Header: "إضافة مستخدم جديد" + close button
- Form:
  - Segmented role selector (3 pills: Admin | مساعد | طالب)
  - Full Name input (right-to-left)
  - Phone input with Egyptian flag prefix
  - Password input with show/hide eye toggle
  - Packages multi-select (animated show/hide based on role = Student)
  - Submit button with spinner
- Inline validation errors below each field
- On success: `onSuccess()` callback + toast

### [MODIFY] `page.tsx` (`/admin/users`)
- Add `showAddUser` state + `AddUserDrawer` usage
- Replace the dead `NeumorphButton` with one that sets `showAddUser(true)`
- After success: refresh users list

---

## Data Flow for Package Enrollment

After checking the entities, the admin-created student package access will use:
```csharp
// Check StudentPackageAccess entity structure
// If it matches RegisterCommand's grant mechanism, reuse it
```

We'll look up the exact entity name during implementation.

---

## UI/UX Standards (impeccable + ui-ux-pro-max)

- Drawer width: `max-w-[480px]` on desktop, full-width on mobile
- Smooth slide animation: `framer-motion` x translate
- Segmented role pills: primary highlight on selected
- Package multi-select: checkboxes with package name badges
- All labels right-to-left (`dir="rtl"`)
- Input focus: `ring-2 ring-[var(--admin-primary)]`
- Error state: red ring + error text below field
- Loading state: spinner on button, disabled inputs

---

## Verification Plan

1. `dotnet build` — no errors
2. Manual: Add Admin → verify in users list
3. Manual: Add Student with packages → verify packages in student profile
4. Manual: Duplicate phone → verify 400 error shown inline
5. Manual: Short password → verify validation error
