# Quickstart: Registration, Code System & Content Hierarchy Overhaul

## Prerequisites

- .NET 8 SDK
- Node.js 18+
- PostgreSQL (via Supabase)
- Redis

## Implementation Order

### Step 1: Backend — Enums & Entities

1. Create enums: `EducationStage`, `GradeLevel`, `StudyTrack`, `Gender`, `CodeType`
2. Modify `StudentProfile.cs` — add 8 new fields, remove old ones
3. Create `Term.cs` entity
4. Modify `ContentEntities.cs` — Package gets Terms nav, ContentSection gets TermId
5. Modify `CodeEntities.cs` — CodeGroup gets CodeType, new FKs, discount, expiration
6. Create `StudentBalance.cs` and `BalanceTransaction.cs`
7. Create `CodeVideoTarget.cs` join table

### Step 2: Backend — Migration

1. Update `ApplicationDbContext.cs` with new DbSets and configurations
2. Generate EF Core migration
3. Write data migration: create default Terms, re-point ContentSections, backfill CodeType

### Step 3: Backend — Services

1. Update `RegistrationService` — single-flow with validation matrix
2. Update `CodeService` — 6 code types, QR generation, discount
3. Create `BalanceService` — balance operations with atomic transactions
4. Create `QrCodeService` — QR image generation using QRCoder

### Step 4: Backend — API

1. Update `AuthController.Register` — accept all new fields
2. Update `CodesController` — code type selection, QR endpoints
3. Create `BalanceController` — balance inquiry, purchase
4. Update `ContentController` — term CRUD

### Step 5: Frontend — Registration

1. Rebuild `RegistrationForm.tsx` with all fields
2. Create `AcademicFields.tsx` — conditional stage/grade/track
3. Update registration service

### Step 6: Frontend — Code System

1. Create `CodeTypeSelector.tsx` — 6 types
2. Create `QrScanner.tsx` — camera-based scanner
3. Create `QrDisplay.tsx` — printable QR
4. Update admin codes page

### Step 7: Frontend — Content Hierarchy

1. Create `TermManager.tsx` — term CRUD
2. Update admin content page — term level
3. Update student navigation — Package > Term > Section > Lesson
4. Add quick-access shortcuts to student dashboard

### Step 8: Frontend — Balance

1. Create balance display component
2. Create purchase flow
3. Add "recharge your balance" prompt

## Verification

```bash
# Backend tests
cd backend && dotnet test

# Frontend tests
cd frontend && npm test

# Migration test
dotnet ef database update --project src/NaderGorge.Infrastructure

# Manual verification
# 1. Register a new student with all fields
# 2. Create each of the 6 code types
# 3. Redeem codes via manual entry and QR scan
# 4. Verify content hierarchy displays correctly
# 5. Test balance operations
```
