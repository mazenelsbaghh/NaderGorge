# Quickstart: Student Academic Scope Enforcement

## Prerequisites

- Use the repository root: `/Users/mazenelsbagh/mazen mac/apps/nader gorge`
- Ensure `.env` values are present for normal local/Docker startup.
- This feature includes a database migration.

## Build and Test Commands

```bash
dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AcademicScope"
dotnet test backend/NaderGorge.sln --filter "FullyQualifiedName~AccessCheck|FullyQualifiedName~Purchase|FullyQualifiedName~Gift|FullyQualifiedName~Code|FullyQualifiedName~Sales"
cd frontend && npm run lint && npm run build
make verify
```

## Docker Gate

```bash
docker compose config -q
make up
make migrate
curl -f http://localhost:5245/api/health
curl -f http://localhost:8738
curl -f http://localhost:3001/ui
make ps
```

## Seed/Manual Data Setup

Create or seed:

- Student A: `Secondary / FirstSecondary`.
- Student B: `Secondary / SecondSecondary`.
- Subject 1 allowed for `Secondary / FirstSecondary`.
- Subject 2 allowed for `Secondary / SecondSecondary`.
- Subject 3 not allowed for Student A.
- Package, public exam, teacher, community post, shared package, notification, code/coupon/gift targets for:
  - exact match Student A
  - non-match Student A
  - platform-wide
  - stage-wide Secondary
  - grade-all-subjects Secondary/FirstSecondary

## Manual QA Matrix

### Student Visibility

1. Login as Student A.
2. Open:
   - `/student/packages`
   - `/student/teachers`
   - `/student/community`
   - `/student/public-exams`
   - `/student/shared-packages`
   - `/student/notifications`
3. Expected:
   - exact matching items appear
   - platform-wide items appear
   - stage-wide Secondary items appear
   - grade-all-subjects Secondary/FirstSecondary items appear
   - non-matching items do not appear

### Direct Access Denial

1. Login as Student A.
2. Open a direct URL for Student B package/lesson/video/exam/community target.
3. Expected:
   - API returns `ACADEMIC_SCOPE_DENIED`
   - UI shows clear Arabic unavailable state
   - protected details are not exposed

### Purchase and Discount Denial

1. Login as Student A.
2. Attempt to purchase a non-matching package/public exam.
3. Apply a coupon or printable code for a non-matching target.
4. Expected:
   - purchase fails before balance deduction
   - coupon/printable code is not committed
   - no `StudentAccessGrant`
   - no `SalesFinancialEffect`

### Code Redemption

1. Validate and activate matching code.
2. Validate and activate non-matching code.
3. Validate and activate platform-wide/stage-wide/grade-all-subjects code.
4. Expected:
   - matching/general codes succeed
   - non-matching code fails before consumption

### Gifts

1. Admin issues a matching gift to Student A.
2. Admin issues a non-matching gift to Student A.
3. Expected:
   - matching recipient is granted
   - non-matching recipient has `ACADEMIC_SCOPE_DENIED`
   - no grant is created for denied recipient

### Existing Grant Re-Evaluation

1. Give Student A access to a matching target.
2. Change Student A grade to a non-matching grade or deactivate subject eligibility.
3. Attempt to open the old target.
4. Expected:
   - historical grant remains queryable
   - access is denied immediately

### Admin Validation

1. Try to save/publish package/public exam/coupon/code/gift/shared package/community/notification without academic scopes.
2. Expected: save/publish fails with `ACADEMIC_SCOPE_REQUIRED` or `ACADEMIC_SCOPE_TARGET_UNSCOPED`.
3. Add `PlatformWide`, `StageWide`, `GradeAllSubjects`, and `Exact` scopes in separate records.
4. Expected: save succeeds and student visibility follows selected scope.

## Rollback Notes

- Do not delete historical grants during rollback.
- If migration must be reverted locally, use EF migration rollback after backing up development data.
- Since legacy fields are preserved, admin can reclassify unscoped records after deployment.
