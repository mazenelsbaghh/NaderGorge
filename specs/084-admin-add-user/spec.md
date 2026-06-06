# Feature Specification: 084 — Admin Add User

## Overview
Replace the dead "دعوة عضو جديد" button on the Admin Users page (`/admin/users`) with a fully functional **إضافة مستخدم** (Add User) slide-in drawer. Admins can create any user type (Admin / Assistant / Student) directly — without going through the public registration flow. For students, the admin can optionally assign one or more packages at creation time.

## User Stories

### US-1: Add Admin or Assistant
- Admin clicks "إضافة مستخدم" → drawer opens
- Selects role: Admin or Assistant
- Fills: Full Name (2+ words), Phone Number, Password
- Submits → new user appears in the list immediately

### US-2: Add Student with package assignment
- Admin selects role: Student
- Fills: Full Name (4 words رباعي), Phone Number, Password
- Optional: selects one or more packages from a multi-select list
- Submits → student created, packages enrolled if selected

### US-3: Validation and error feedback
- Phone duplicate → inline error "رقم الهاتف مسجل بالفعل"
- Password < 6 chars → inline error
- Name too short → inline error
- Network error → toast error

## Functional Requirements

### Backend
1. New endpoint: `POST /admin/users` — `AdminCreateUserCommand`
   - Fields: `FullName`, `PhoneNumber`, `Password`, `Role` (string enum: "Admin"|"Assistant"|"Student"), `PackageIds` (Guid[], optional)
   - Creates `User` with hashed password
   - Assigns `Role` via `UserRole` entity
   - For students: creates `StudentProfile` (minimal), enrols in packages via `StudentPackageAccess`
   - Returns: `{ id: Guid, fullName: string, phoneNumber: string, role: string }`
   - Auth: [Admin] only
2. New endpoint: `GET /admin/packages/list` — returns all packages `[{ id, name }]` for the dropdown
   - Auth: [Admin]

### Frontend
1. Replace the button `NeumorphButton` (currently dead) with one that opens `AddUserDrawer`
2. `AddUserDrawer` — slide-in from right, uses `AdminModal` or a custom drawer
3. Form fields:
   - `الاسم الكامل` — text input
   - `رقم الهاتف` — text input (01x format validation)
   - `كلمة السر` — password input (show/hide toggle)
   - `الدور` — segmented radio: Admin | مساعد | طالب
   - `الباقات` (shown only when role = Student) — multi-select checkboxes from packages list
4. Submit button with loading state
5. On success: close drawer + refresh users list + `toast.success`
6. On error: show inline field errors

## Non-functional
- No DB migration required (uses existing tables: `Users`, `UserRoles`, `StudentProfiles`, `StudentPackageAccess`)
- Validation: server-side + client-side
- Password hashed with BCrypt (same as RegisterCommand)

## Out of scope
- Email, avatar, parent data for admin-created students (can be completed later)
- Sending SMS/WhatsApp welcome message (future feature)

## Checklist
- [ ] US-1: Add Admin/Assistant
- [ ] US-2: Add Student with packages
- [ ] US-3: Validation feedback
