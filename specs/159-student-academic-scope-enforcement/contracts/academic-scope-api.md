# Contract: Academic Scope API and DTO Changes

This contract describes required request/response shape changes for existing APIs. Exact controller routes remain the existing routes unless named below.

## Shared DTOs

### AcademicScopeDto

```json
{
  "id": "guid-or-null-for-new",
  "scopeLevel": "Exact | PlatformWide | StageWide | GradeAllSubjects",
  "educationStage": "Secondary",
  "gradeLevel": "FirstSecondary",
  "subjectId": "guid-or-null",
  "subjectName": "optional display"
}
```

Validation:

- `PlatformWide`: no stage, grade, or subject.
- `StageWide`: stage only.
- `GradeAllSubjects`: stage and grade only.
- `Exact`: stage, grade, and subject.
- At least one scope is required for every student-facing admin save/publish unless the target derives scope from another already-scoped owner.

### AcademicScopeError

```json
{
  "success": false,
  "message": "هذا المحتوى غير متاح لمرحلتك أو صفك الحالي.",
  "errors": ["ACADEMIC_SCOPE_DENIED"]
}
```

Standard error codes:

- `ACADEMIC_SCOPE_REQUIRED`
- `ACADEMIC_SCOPE_DENIED`
- `ACADEMIC_SCOPE_INVALID_SUBJECT`
- `ACADEMIC_SCOPE_TARGET_UNSCOPED`
- `STUDENT_PROFILE_REQUIRED`

## Admin Content Contracts

Existing package/term/section/lesson/video create/update requests must include:

```json
{
  "academicScopes": [
    {
      "scopeLevel": "Exact",
      "educationStage": "Secondary",
      "gradeLevel": "FirstSecondary",
      "subjectId": "..."
    }
  ]
}
```

Responses should include:

```json
{
  "academicScopes": [
    {
      "id": "...",
      "scopeLevel": "Exact",
      "educationStage": "Secondary",
      "gradeLevel": "FirstSecondary",
      "subjectId": "...",
      "subjectName": "..."
    }
  ],
  "effectiveScopeSummary": "الصف الأول الثانوي - المادة"
}
```

## Student List Contracts

Student endpoints must return already-filtered data:

- `GET /api/content/packages`
- `GET /api/content/packages/{packageId}/terms`
- `GET /api/content/terms/{termId}/sections`
- `GET /api/content/sections/{sectionId}/lessons`
- `GET /api/content/lessons/{lessonId}`
- `GET /api/public/teachers`
- `GET /api/community/posts`
- `GET /api/public-exams`
- `GET /api/student/shared-packages`
- `GET /api/student/notifications`

No request parameter should allow the student to override stage, grade, or subjects.

When nothing matches, return success with an empty list:

```json
{
  "success": true,
  "message": "",
  "data": []
}
```

When a direct detail target is outside scope, return a denial:

```json
{
  "success": false,
  "message": "هذا المحتوى غير متاح لمرحلتك أو صفك الحالي.",
  "errors": ["ACADEMIC_SCOPE_DENIED"]
}
```

## Purchase Contract

Existing request:

```http
POST /api/student/balance/purchase
```

Academic validation occurs before:

- coupon/printable code commit
- promotional balance consumption
- student balance deduction
- `StudentAccessGrant`
- `SalesFinancialEffect`
- teacher accounting events

Failure:

```json
{
  "success": false,
  "message": "لا يمكن شراء هذا المحتوى لأنه غير متاح لمرحلتك أو صفك الحالي.",
  "errors": ["ACADEMIC_SCOPE_DENIED"]
}
```

## Code Contracts

### Validate

```http
POST /api/codes/validate
```

Must include current student eligibility in validation. Non-matching target returns:

```json
{
  "success": false,
  "message": "هذا الكود لا يناسب مرحلتك أو صفك الحالي.",
  "errors": ["ACADEMIC_SCOPE_DENIED"]
}
```

### Activate

```http
POST /api/codes/activate
```

Academic validation occurs before code consumption. If denied:

- `AccessCode.IsConsumed` remains false.
- no `StudentAccessGrant` is created.
- audit may record a denied attempt without exposing plaintext code.

## Sales Coupon and Printable Batch Contracts

Create/update requests must validate scoped targets:

- `SalesCouponRequest`
- `PrintableBatchRequest`
- `SalesRuleRequest`

If no concrete student exists at creation time, validate target scope only. At application/redemption time, validate actual student.

Failure at creation:

```json
{
  "success": false,
  "message": "هدف الكوبون أو الكود يجب أن يكون مربوطا بنطاق أكاديمي صالح أو نطاق عام صريح.",
  "errors": ["ACADEMIC_SCOPE_TARGET_UNSCOPED"]
}
```

## Gift Contract

`IssueGiftRequest` remains recipient-based but must validate each recipient.

Recipient result for academic denial:

```json
{
  "studentId": "...",
  "status": "Failed",
  "outcomeCode": "ACADEMIC_SCOPE_DENIED",
  "outcomeMessage": "الهدية غير متاحة لمرحلة أو صف هذا الطالب."
}
```

No `StudentAccessGrant` or `PromotionalBalanceAllocation` is created for denied recipients.

## Frontend Contract

Services must type and send `academicScopes` for admin creation/update:

- `frontend/src/services/admin-service.ts`
- `frontend/src/services/code-service.ts`
- `frontend/src/services/admin-gifts-service.ts`
- `frontend/src/services/public-exams-service.ts`
- `frontend/src/services/shared-package-service.ts`
- `frontend/src/services/community-service.ts`

Student pages must not add client-only filters to compensate for backend leaks. They may show Arabic empty states when data arrays are empty.
