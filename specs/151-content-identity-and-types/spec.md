# Feature Specification: Content Identity and Types

**Feature Branch**: `151-content-identity-and-types`
**Created**: 2026-06-29
**Status**: Complete
**Input**: Approved Phase 1.1 brief for stable internal content codes and administrator-managed video types.

## Clarifications

### Session 2026-06-29

- Q: هل الكود الداخلي فريد داخل نوع المحتوى فقط أم على المنصة كلها؟ → A: فريد على مستوى المنصة كلها.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stable Internal Content Identity (Priority: P1)

As an administrator, I want every lesson, video, and exam to have a visible, unique internal code so that staff can identify content reliably in sales, support, reporting, and later accounting workflows.

**Why this priority**: Stable identity is the dependency for every later Phase 1 workflow. Gifts, discounts, public exams, and teacher accounting must not target ambiguous content.

**Independent Test**: Create a lesson, video, and exam, record each displayed internal code, edit the items, and verify that the codes remain unique and unchanged.

**Acceptance Scenarios**:

1. **Given** an authorized administrator creates a lesson, video, or exam, **When** creation succeeds, **Then** the item receives a non-empty internal code that is unique across all supported content kinds.
2. **Given** an existing item has an internal code, **When** an authorized user edits any mutable field, **Then** the internal code remains unchanged and cannot be submitted as an editable value.
3. **Given** pre-existing lessons, videos, and exams, **When** the feature migration is applied, **Then** every record receives a unique internal code without changing its existing identifier, ownership, access, or availability.
4. **Given** an administrator views a content management list or detail surface, **When** the item is loaded, **Then** its internal code is shown as read-only text.

---

### User Story 2 - Manage Video Types (Priority: P1)

As an administrator, I want to create, rename, order, activate, and deactivate video types so that content classification can evolve without a software release.

**Why this priority**: A controlled classification replaces the current optional free-text tag and provides a dependable target for later purchase rules.

**Independent Test**: Add a type, rename it, change its order, deactivate it, and verify that active and inactive choices behave correctly without deleting existing assignments.

**Acceptance Scenarios**:

1. **Given** the feature is first applied, **When** an administrator opens video type management, **Then** active default types for Explanation, Homework, Review, and Exam are available in Arabic.
2. **Given** an administrator enters a unique valid type name, **When** the type is saved, **Then** it appears in the ordered list and becomes available for new video assignment.
3. **Given** a type is already assigned to videos, **When** an administrator deactivates it, **Then** existing assignments remain visible while the type is excluded from new-video choices.
4. **Given** a type is assigned to one or more videos, **When** an administrator attempts to delete it, **Then** deletion is blocked and the administrator is directed to deactivate it instead.
5. **Given** a non-administrator attempts to manage video types, **When** the request is evaluated, **Then** access is denied without changing type data.

---

### User Story 3 - Require a Valid Type During Video Management (Priority: P2)

As an authorized content manager, I want to select an active video type while creating or editing a video so that every new or changed video has valid classification.

**Why this priority**: Classification must be enforced at the point where content is managed, while preserving old content during rollout.

**Independent Test**: Create and edit videos using an active type, then attempt the same operations with no type and with an inactive type.

**Acceptance Scenarios**:

1. **Given** at least one active video type exists, **When** an authorized content manager opens the video form, **Then** active types are available as a required selection.
2. **Given** a valid title, provider data, and active type, **When** the form is submitted, **Then** the video is saved and displays its selected type and internal code.
3. **Given** no type or an inactive type is submitted for a new video, **When** validation runs, **Then** saving is rejected with a clear field-level message.
4. **Given** an existing video uses a now-inactive type, **When** it is opened for editing, **Then** its current type is displayed, but changing to another type requires selecting an active type.
5. **Given** the active type list cannot be loaded, **When** the form is displayed, **Then** type-dependent submission is disabled and a retryable error state is shown.

### Edge Cases

- Two administrators submit the same new type name with different casing or surrounding spaces; only one normalized name may be accepted.
- Concurrent creation across lessons, videos, and exams must not produce duplicate internal codes.
- A failed migration or save must not leave partially classified content or partially seeded defaults.
- Legacy `VideoTag` values that match a known default should map to that type; unmatched or empty values should map to a clearly identified migration fallback type for later review.
- Renaming a type must update its display name without breaking existing video assignments.
- Deactivating every type must not corrupt existing videos; new video creation remains blocked until an active type exists.
- Internal codes must remain usable and visually distinguishable even when titles are duplicated.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: `pending` - sign in as administrator, open content type management, create and rename a type, use it in a new video, and verify the read-only internal code survives editing.
- **Manual QA Migration Flow**: `pending` - inspect representative pre-existing lessons, videos, and exams after migration and verify their playback/access behavior plus generated code/type values.
- **Manual QA Negative Check**: `pending` - verify a non-administrator cannot manage types and that an inactive or missing type cannot be assigned to a new video.
- **Docker Acceptance**: build the backend and frontend images, apply migrations to a disposable database, verify API health, and exercise the admin content surfaces without startup or migration errors.
- **External Dependencies**: no new external API, secret, gateway, or device is required; existing video providers are regression-only dependencies.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST assign every lesson, lesson video, and exam a non-empty internal code.
- **FR-002**: Internal codes MUST be globally unique across lessons, lesson videos, and exams and MUST remain immutable after assignment.
- **FR-003**: Internal codes MUST be generated by the system and MUST NOT be accepted as editable values from content create or update requests.
- **FR-004**: Authorized administrative content views MUST display internal codes as read-only values in relevant lists and details.
- **FR-005**: The system MUST backfill internal codes for all pre-existing lessons, lesson videos, and exams without changing their existing identifiers, ownership, access grants, visibility, or prices.
- **FR-006**: The system MUST provide default Arabic video types equivalent to Explanation, Homework, Review, and Exam.
- **FR-007**: Administrators MUST be able to list, create, rename, reorder, activate, and deactivate video types.
- **FR-008**: Video type names MUST be required, trimmed, length-bounded, and unique under case-insensitive normalized comparison.
- **FR-009**: Only administrators MUST be permitted to create, rename, reorder, activate, deactivate, or delete video types.
- **FR-010**: A video type assigned to any video MUST NOT be deletable; the supported retirement action is deactivation.
- **FR-011**: Deactivated video types MUST remain visible on existing assigned videos and MUST NOT appear as valid choices for new assignments.
- **FR-012**: Every newly created lesson video MUST reference one active video type.
- **FR-013**: Updating an existing video MUST preserve its current inactive type unless the user explicitly selects another active type.
- **FR-014**: Video create and update operations MUST reject unknown, missing, or invalid replacement type references with a clear validation result.
- **FR-015**: The system MUST migrate legacy video tags deterministically: recognized values map to the corresponding default type and unrecognized or empty values map to a named migration fallback type.
- **FR-016**: Type management changes and denied destructive actions MUST be auditable with actor, action, target, timestamp, and relevant before/after values.
- **FR-017**: Existing content playback, exam attempts, purchase grants, code redemption, AI processing, and provider behavior MUST continue to use existing content identifiers and remain unchanged by this feature.
- **FR-018**: If no active types can be loaded, the video management experience MUST show an actionable error and prevent an invalid submission.
- **FR-019**: The system MUST not require teacher or subject duplication on the video record when those values are already deterministically inherited from its lesson hierarchy.

### Key Entities

- **Lesson**: A sellable instructional unit that receives an immutable internal code while retaining its existing hierarchy and identifier.
- **Lesson Video**: A video belonging to a lesson; receives an immutable internal code and references exactly one video type after migration.
- **Exam**: An assessment that receives an immutable internal code; independent public-exam behavior is outside this feature.
- **Video Type**: Administrator-managed classification with a display name, normalized unique name, display order, active state, and assignment history through videos.
- **Audit Entry**: Existing administrative evidence extended to record type lifecycle actions and blocked destructive attempts where the current audit mechanism supports them.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After migration, 100% of existing lessons, lesson videos, and exams have non-empty internal codes, with zero duplicates across all three content kinds.
- **SC-002**: An administrator can create a video type and use it in a newly created video in under 3 minutes without a software deployment.
- **SC-003**: In all edit-path tests, an assigned internal code remains unchanged after mutable content fields are updated.
- **SC-004**: 100% of attempts to create a video without an active type are rejected with a visible validation message.
- **SC-005**: 100% of attempts by non-administrators to manage video types are denied and cause no persisted change.
- **SC-006**: Representative existing video playback, lesson access, exam attempt, and code-redemption regression tests continue to pass after migration.
- **SC-007**: Existing assignments to a deactivated type remain visible in 100% of tested admin detail and edit views.

## Assumptions

- Internal codes are operational identifiers, not student purchase or redemption codes.
- Internal code formatting is an implementation decision as long as codes are readable, unique, stable, and distinguish content kinds.
- Existing roles and content-management authorization remain authoritative; this feature adds no new role.
- Administrators alone manage the type catalog, while any role already authorized to create or edit videos may select active types.
- Teacher and subject ownership continue to be inherited through video to lesson, section, term, package, teacher, and subject.
- Gifts, advanced discounts, public standalone exams, teacher revenue allocation, and printable code templates are separate later Specs.
