# Phase 0 Research: Student Academic Scope Enforcement

## Decision: Use normalized scope rows instead of columns on every entity

**Rationale**: The feature applies to many owner types: packages, hierarchy children, videos, exams, teachers, community posts, notifications/offers, sales coupons, printable batches, code groups, gifts, and shared packages. A single `StudentFacingAcademicScope` table with `OwnerType` and `OwnerId` supports multiple scopes per item, explicit general levels, and future owner types without repeated nullable columns.

**Alternatives considered**:

- Add `EducationStage`, `GradeLevel`, `SubjectId`, `IsPlatformWide` to every table. Rejected because it duplicates rules, makes multi-scope support awkward, and increases migration/UI drift.
- Store scopes as JSON on each owner. Rejected because PostgreSQL/EF querying, indexes, validation, and migration backfill become weaker.

## Decision: Add `AcademicSubjectEligibility` mapping

**Rationale**: The spec requires allowed subjects to come from stage/grade setup, not student choices or free-text labels. Existing `Subject`, `TeacherSubject`, `Package.SubjectId`, and `PublicExamProduct.SubjectId` exist, but there is no authoritative stage/grade-to-subject table.

**Alternatives considered**:

- Infer subjects from packages or teacher relationships. Rejected because available content should not define academic eligibility.
- Store subject names on scope rows. Rejected because subject matching must use IDs and avoid free-text drift.

## Decision: Use `IAcademicScopeService` as the only eligibility source

**Rationale**: Current behavior is scattered across `GetPackagesQuery`, `AccessCheckService`, `PurchaseContentCommand`, `ActivateCodeCommand`, sales, gifts, and community queries. A central service reduces drift and makes tests precise.

**Alternatives considered**:

- Inline filters in each handler. Rejected because it already caused inconsistent behavior.
- Frontend filtering. Rejected because the spec requires backend enforcement.

## Decision: Re-evaluate academic scope at use time for existing grants

**Rationale**: The clarified spec requires old purchases/gifts/codes to remain as history but stop permitting use when the student's current profile or subject mapping no longer matches. `AccessCheckService` already mediates lesson/video/exam access and is the correct enforcement point.

**Alternatives considered**:

- Mutate or deactivate grants when profile changes. Rejected because profile or mapping changes can be broad and should not destroy financial/audit history.
- Allow grants until expiry. Rejected by clarification.

## Decision: Scope inheritance applies only to hierarchical content

**Rationale**: The hierarchy `Package → Term → ContentSection → Lesson → LessonVideo/Exam` is constitutionally fixed. Child items without explicit scope inherit the nearest explicit parent scope; child items with explicit scope must match independently. Non-hierarchical items such as coupons, gifts, posts, teachers, and notifications must have explicit target scope or derive scope from their target during validation.

**Alternatives considered**:

- Require explicit scope on every child. Rejected because it creates excessive admin data entry.
- Parent scope always wins. Rejected because it can leak non-matching child content.

## Decision: Code/coupon/gift validation is two-stage

**Rationale**: Some tools are created before a concrete student is known. Creation must verify the target has a valid scope, and use/delivery must re-check the actual student's current academic eligibility before grant, discount, balance deduction, or consumption.

**Alternatives considered**:

- Validate only at creation. Rejected because student-specific eligibility is unknown or can change.
- Validate only at use. Rejected because it allows unscoped tools to be created and distributed.

## Decision: Backfill legacy fields but do not default unscoped records to general

**Rationale**: The spec requires fail closed for missing scope. Existing `Package.TargetGrade`, `PublicExamProduct.GradeLevel`, `IsPlatformWide`, `SubjectId`, and `SharedTeacherPackage.EducationStage/GradeLevel` can be converted into scope rows. Records that cannot be mapped remain hidden from students until explicitly scoped.

**Alternatives considered**:

- Mark all old missing-scope records platform-wide. Rejected because it violates fail-closed behavior.
- Delete or block old records. Rejected because admin may need to classify them after migration.

## Decision: Keep legacy fields during rollout

**Rationale**: Existing DTOs and frontend screens reference fields such as `Package.TargetGrade` and `PublicExamProduct.GradeLevel`. Keeping them during rollout reduces blast radius while normalized scopes become the source of truth.

**Alternatives considered**:

- Remove legacy fields immediately. Rejected because it would force broad UI and migration changes in one step.

## Decision: Add backend tests around service behavior and critical workflows

**Rationale**: The highest risk is silent access leakage. Tests must cover list filtering, direct access, purchases, coupons, codes, gifts, profile changes, inheritance, multiple scopes, and general scope levels.

**Alternatives considered**:

- Rely on build/lint only. Rejected because behavior changed across authorization and financial paths.

## Decision: No worker or external service changes

**Rationale**: The feature is synchronous access and data filtering. Worker jobs may emit notifications after grants, but they should only see grants that backend validation already accepted.

**Alternatives considered**:

- Add background cleanup job for invalid grants. Rejected because use-time re-evaluation is required and preserves history.
