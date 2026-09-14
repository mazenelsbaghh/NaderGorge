# Data Model: Student Academic Scope Enforcement

## AcademicSubjectEligibility

Defines which subjects are allowed for each student stage and grade.

Fields:

- `Id: Guid`
- `EducationStage: EducationStage`
- `GradeLevel: GradeLevel`
- `SubjectId: Guid`
- `Subject: Subject`
- `IsActive: bool`
- `CreatedAt: DateTime`
- `UpdatedAt: DateTime?`

Validation:

- `EducationStage` and `GradeLevel` must be a valid pair according to `AcademicValidationService.IsGradeValidForStage`.
- `(EducationStage, GradeLevel, SubjectId)` must be unique.
- Inactive rows are ignored for student eligibility.

Indexes:

- Unique `(EducationStage, GradeLevel, SubjectId)`.
- `(SubjectId)`.

## StudentFacingAcademicScope

Represents one eligibility rule attached to a student-facing owner. Owners can have multiple rows.

Fields:

- `Id: Guid`
- `OwnerType: StudentFacingScopeOwnerType`
- `OwnerId: Guid`
- `ScopeLevel: AcademicScopeLevel`
- `EducationStage: EducationStage?`
- `GradeLevel: GradeLevel?`
- `SubjectId: Guid?`
- `Subject: Subject?`
- `CreatedByUserId: Guid?`
- `CreatedByUser: User?`
- `CreatedAt: DateTime`
- `UpdatedAt: DateTime?`

Validation by `ScopeLevel`:

- `PlatformWide`: `EducationStage`, `GradeLevel`, and `SubjectId` must be null.
- `StageWide`: `EducationStage` required; `GradeLevel` and `SubjectId` must be null.
- `GradeAllSubjects`: `EducationStage` and `GradeLevel` required; `SubjectId` must be null; grade must belong to stage.
- `Exact`: `EducationStage`, `GradeLevel`, and `SubjectId` required; grade must belong to stage; subject must be allowed by `AcademicSubjectEligibility`.

Matching:

- Multiple rows are OR conditions.
- An owner matches when any row matches the student's current `StudentProfile.EducationStage`, `StudentProfile.GradeLevel`, and allowed subject IDs.
- Missing owner rows fail closed unless a hierarchical parent supplies an inherited scope.

Indexes:

- `(OwnerType, OwnerId)`.
- `(ScopeLevel, EducationStage, GradeLevel, SubjectId)`.
- `(SubjectId)`.

## Enums

`AcademicScopeLevel`:

- `Exact = 0`
- `PlatformWide = 1`
- `StageWide = 2`
- `GradeAllSubjects = 3`

`StudentFacingScopeOwnerType`:

- `Package`
- `Term`
- `ContentSection`
- `Lesson`
- `LessonVideo`
- `Exam`
- `PublicExamProduct`
- `Teacher`
- `CommunityPost`
- `NotificationEvent`
- `SalesCoupon`
- `PrintableCodeBatch`
- `CodeGroup`
- `GiftIssuance`
- `SharedTeacherPackage`
- `SharedTeacherPackageItem`

## Existing Entity Relationships

### StudentProfile

Existing source of truth:

- `EducationStage`
- `GradeLevel`

No student-selected subject list is added.

### Subject

Existing `Subject` remains canonical. `AcademicSubjectEligibility.SubjectId` references it.

### Package and Content Hierarchy

Existing hierarchy:

- `Package.SubjectId`, `Package.TargetGrade`, `Package.TeacherId`
- `Term.PackageId`
- `ContentSection.TermId`
- `Lesson.ContentSectionId`
- `LessonVideo.LessonId`
- `Exam` via `Lesson.ExamId`, `LessonVideo.ExamId`, or `Exam.LessonVideoId`

Effective scope:

- `Package` can have explicit scope rows.
- `Term`, `ContentSection`, `Lesson`, `LessonVideo`, and linked `Exam` can have explicit rows.
- If a child has no rows, use nearest parent rows.
- If a child has rows, the child rows must match. Parent path must also be eligible for the route being accessed.

### PublicExamProduct

Existing fields:

- `SubjectId`
- `GradeLevel`
- `IsPlatformWide`

Migration:

- `IsPlatformWide = true` → `PlatformWide`.
- `GradeLevel` plus `SubjectId` → `Exact` after resolving `EducationStage` from grade aliases.
- `GradeLevel` without subject → `GradeAllSubjects` after resolving stage.
- Missing scope remains hidden until admin fixes it.

### SharedTeacherPackage

Existing fields:

- `EducationStage`
- `GradeLevel`
- teacher/item `SubjectId`

Migration:

- `EducationStage` and `GradeLevel` plus item subject → exact or grade-all-subjects depending on item data.
- Missing stage/grade remains unscoped.

### CodeGroup, SalesCoupon, PrintableCodeBatch, GiftIssuance

These are deferred-student scope tools.

Rules:

- Creation validates that the target resolves to valid academic/general scope.
- Use/delivery re-validates against concrete student.
- They may store their own explicit scopes if their target is broad, but target-specific tools should derive scope from the target where possible.

### StudentAccessGrant

Existing grant rows remain historical entitlement records.

Rules:

- `IsActive`, expiry, use limits, and target hierarchy still apply.
- A grant only permits use when the current academic profile is eligible for the target.
- No migration deletes old grants.

## State Transitions

Grant creation:

1. Resolve target.
2. Validate target has valid scope.
3. Validate concrete student when known.
4. Create grant, financial effect, coupon usage, or gift recipient only after validation passes.

Grant use:

1. Check role bypass for admin/teacher where existing rules allow.
2. Check active grant and expiry/use limits.
3. Re-evaluate current academic eligibility.
4. Allow or deny.

Student profile/subject mapping change:

1. Historical grants remain unchanged.
2. Future use checks use updated profile/mapping.
3. Non-matching access returns denial with Arabic message.

## Migration Notes

- Add tables and indexes in one EF migration.
- Seed or backfill `AcademicSubjectEligibility` from existing package/teacher/subject data only when stage/grade can be determined safely.
- Backfill scope rows from legacy fields conservatively.
- Do not default null legacy scope to platform-wide.
- Preserve `Package.TargetGrade`, `PublicExamProduct.GradeLevel`, and `IsPlatformWide` for compatibility until a later cleanup feature.
