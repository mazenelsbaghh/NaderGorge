# Feature Specification: Package Partial Enrollment Display

**Feature Branch**: `134-package-partial-enrollment`  
**Created**: 2026-06-16  
**Status**: Draft  
**Input**: User description: "Show a package under enrolled packages if a student has activated a term, section, or lesson inside that package."

## Clarifications
- **Expired/Inactive Grants**: Any access grant that is expired (`ExpiresAt <= UTC Now`) or deactivated (`IsActive = false`) must NOT be considered for package enrollment.
- **Grants to other resources**: Video grants (`CodeType.Video`) and Exam grants (`CodeType.Exam`) are not considered part of package-level listing enrollment, as they are too granular and do not represent a purchase of a course unit (like a lesson, section, or term).
- **Frontend Filter Behavior**: The frontend will automatically display the package under "Activated Packages" because the backend DTO property `IsEnrolled` is set to `true`. No changes to the frontend rendering or filtering logic are required.

## User Scenarios & Testing

### User Story 1 - Show Package as Enrolled via Term Grant (Priority: P1)
As a student who has purchased a specific Term inside a Package, I want the Package to appear in my "Activated Packages" (الباقات المفعّلة) section on the dashboard so that I can easily enter the Package to access that Term.

**Why this priority**: Primary way students buy content on a monthly basis.

**Independent Test**: Can be verified by granting Term access to a student and asserting that `/api/content/packages` returns `isEnrolled = true` for that Package.

---

### User Story 2 - Show Package as Enrolled via Section/Lesson Grant (Priority: P2)
As a student who has purchased a single Section or Lesson inside a Package, I want the Package to appear in my "Activated Packages" list.

**Why this priority**: Ensures even single lesson purchases surface the parent package.

**Independent Test**: Can be verified by granting Lesson/Section access and asserting the parent package's `IsEnrolled` is `true`.

---

### Edge Cases
- **Expired Grants**: If a student had access to a lesson but it expired, `IsEnrolled` should remain `false` for the package (unless they have another active grant).

### Manual QA & Docker Acceptance
- Verify that a student with a lesson grant sees the parent package in "الباقات المفعّلة".

## Requirements

### Functional Requirements
- **FR-001**: `GetPackagesQuery` MUST return `isEnrolled = true` if the user has direct package access.
- **FR-002**: `GetPackagesQuery` MUST return `isEnrolled = true` if the user has access to any term inside the package.
- **FR-003**: `GetPackagesQuery` MUST return `isEnrolled = true` if the user has access to any section inside the package.
- **FR-004**: `GetPackagesQuery` MUST return `isEnrolled = true` if the user has access to any lesson inside the package.
- **FR-005**: All checks MUST be optimized into batched database queries.

## Success Criteria

### Success Criteria
- **SC-001**: Accessing `/api/content/packages` returns `IsEnrolled = true` for packages where a child Term/Section/Lesson has an active grant in 100% of cases.
- **SC-002**: Verification via Pytest E2E runs successfully in under 15 seconds.

## Assumptions
- Access grants are tracked in `StudentAccessGrants` table.
