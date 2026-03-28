# Quickstart: تحديث نموذج تسجيل الطالب

**Feature**: 016-registration-form-updates
**Branch**: `016-registration-form-updates`

## Prerequisites

- Docker running with PostgreSQL container
- .NET 8 SDK
- Node.js 18+
- `dotnet-ef` tool installed (`dotnet tool install -g dotnet-ef`)

## Implementation Order

### Step 1: Backend — Domain Entity

Update `StudentProfile.cs` to add new fields and make `StudentCode` optional.

### Step 2: Backend — EF Core Configuration

Update `AppDbContext.cs` to reflect the new field constraints.

### Step 3: Backend — EF Migration

```bash
cd backend/src/NaderGorge.Infrastructure
dotnet ef migrations add AddRegistrationFieldUpdates \
  --startup-project ../NaderGorge.API
dotnet ef database update \
  --startup-project ../NaderGorge.API
```

### Step 4: Backend — RegisterCommand

Update command record, validator, and handler to:
- Remove `StudentCode` from required fields
- Add `District`, `SecondaryPhone`, `SecondaryParentPhone`

### Step 5: Backend — Admin DTOs

Update `AdminUserListDto` and `ListUsersQuery` to include new fields.

### Step 6: Frontend — Governorate Districts Data

Create `frontend/src/data/governorate-districts.ts` with all 27 governorates and their districts.

### Step 7: Frontend — Auth Service

Update `RegisterData` interface in `auth-service.ts`.

### Step 8: Frontend — Registration Form

Update `RegistrationForm.tsx`:
- Remove `studentCode` field and Zod validation
- Add cascading district dropdown
- Add secondary phone fields for student and parent
- Update preview panel

### Step 9: Frontend — Admin Users Page

Update `page.tsx` to show district instead of studentCode where applicable.

## Verification

```bash
# Backend builds
cd backend && dotnet build

# Frontend builds
cd frontend && npm run build

# Dev server
cd frontend && npm run dev
# Navigate to /register and verify:
# 1. No student code field
# 2. District dropdown appears after selecting governorate
# 3. Two phone fields for student
# 4. Two phone fields for parent
# 5. Form submits successfully
```

## Rollback

If issues arise:
```bash
cd backend/src/NaderGorge.Infrastructure
dotnet ef database update AddPhase3TermsAndCodes \
  --startup-project ../NaderGorge.API
dotnet ef migrations remove \
  --startup-project ../NaderGorge.API
```
