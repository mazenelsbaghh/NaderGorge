# Feature Specification: Teacher Profile & Content Visibility

**Feature Branch**: `161-teacher-profile-visibility`  
**Created**: 2026-07-13  
**Status**: Draft  
**Input**: User-approved request: allow Admin-only full teacher data editing and independent teacher/content hiding from students and visitors.

## Clarifications

### Session 2026-07-13

- Q: هل إخفاء المدرس وإخفاء المحتوى مستقلان؟ → A: نعم، لكل منهما زر وحالة مستقلة، ولا يغيّر أحدهما الآخر تلقائياً.
- Q: هل يختفي المحتوى المخفي من الطلاب الذين اشتروه سابقاً؟ → A: نعم، يتوقف الوصول مؤقتاً مع حفظ سجل الشراء، ويعود عند الإظهار.
- Q: هل يشمل الإخفاء الزوار والروابط المباشرة؟ → A: نعم، الإخفاء شامل للزوار والطلاب والروابط المباشرة وكل الأسطح المنشورة.
- Q: من يملك صلاحية التعديل والإخفاء؟ → A: الـ Admin فقط.
- Q: ماذا يعني تعديل كل بيانات المدرس؟ → A: جميع البيانات المدعومة، بما فيها بيانات الدخول والملف الشخصي، مع بقاء كلمات المرور write-only.

## User Scenarios & Testing

### User Story 1 - Edit a teacher completely (Priority: P1)

As an Admin, I need to edit all teacher account and profile data from one management flow so that teacher information and login details remain accurate.

**Why this priority**: Incorrect teacher data affects account access, public identity, content ownership, and support operations.

**Independent Test**: An Admin edits a teacher profile and login field, saves it, reloads the record, and verifies each changed value persists while the current password remains undisclosed.

**Acceptance Scenarios**:

1. **Given** an Admin opens an existing teacher, **When** the Admin changes valid profile and account fields and saves, **Then** the saved record contains the new values and the teacher can use the updated login data.
2. **Given** an Admin requests a password change, **When** the Admin submits a new valid password, **Then** the new password is stored securely and no existing password value is returned or displayed.
3. **Given** invalid or conflicting teacher data, **When** the Admin submits the form, **Then** the system reports field-level validation and does not partially save the update.

### User Story 2 - Hide or show a teacher independently (Priority: P1)

As an Admin, I need an independent teacher visibility control so that a teacher can be removed from all student and visitor discovery surfaces without deleting the account.

**Why this priority**: Visibility is a business control that must be reversible and must not destroy operational records.

**Independent Test**: An Admin hides a teacher, verifies that student and visitor teacher lists, search, recommendations, and direct profile access exclude the teacher, then shows the teacher and verifies they return.

**Acceptance Scenarios**:

1. **Given** a visible teacher, **When** an Admin activates teacher hiding, **Then** the teacher is excluded from every student- and visitor-facing teacher surface and direct public profile access is denied or not found.
2. **Given** a hidden teacher, **When** an Admin shows the teacher, **Then** the teacher becomes eligible for student and visitor discovery again without restoring unrelated hidden content automatically.
3. **Given** a non-Admin attempts to change teacher visibility, **When** the request is submitted, **Then** the backend rejects it and the visibility state is unchanged.

### User Story 3 - Hide or show teacher content independently (Priority: P1)

As an Admin, I need an independent content visibility control so that content can be removed completely from students and visitors, including students who purchased it previously, while preserving all records.

**Why this priority**: Content availability may need to be suspended without deleting purchase, financial, or academic history.

**Independent Test**: An Admin hides teacher content, verifies it is absent from public/student course views and inaccessible through a direct URL for both a visitor and a previous purchaser, then shows it and verifies access returns.

**Acceptance Scenarios**:

1. **Given** visible teacher content, **When** an Admin activates content hiding, **Then** the content is absent from public/student courses, packages, search, recommendations, and direct access is denied for visitors and previous purchasers.
2. **Given** hidden content with existing purchase records, **When** the system evaluates access, **Then** it preserves the purchase record but denies current access while hidden.
3. **Given** hidden content, **When** an Admin shows it, **Then** eligible students regain visibility and access without recreating purchase records.
4. **Given** teacher hiding is active but content hiding is inactive, **When** a student-facing surface is evaluated, **Then** the independent visibility rules are applied consistently and the teacher/content controls do not silently toggle each other.

## Edge Cases

- A teacher with no content, no profile image, or optional profile fields remains editable and can be hidden or shown.
- An attempted change to a duplicate login identifier is rejected without changing the existing account.
- A hidden teacher/content item is not leaked through cached responses, search, recommendations, related-content widgets, community/public teacher pages, or direct identifiers.
- Concurrent Admin edits use the existing conflict/version approach where available and must not silently overwrite newer data.
- Hiding content during an active session causes the next protected data/access check to deny access; stored purchase and audit records remain intact.
- Repeated hide/show requests are idempotent and do not create duplicate audit or visibility transitions.

## Manual QA & Docker Acceptance

- **Manual QA Role/Flow 1**: Admin opens the teacher management flow, edits profile/login fields, saves, reloads, and verifies persistence without seeing an old password.
- **Manual QA Role/Flow 2**: Admin independently hides/shows the teacher and content, then checks student and visitor surfaces plus a direct content URL.
- **Manual QA Negative Check**: Student, visitor, and non-Admin staff requests cannot mutate teacher data or visibility; previous purchasers cannot access hidden content.
- **Docker Acceptance**: Apply the database migration, start the compose stack, verify all relevant services are healthy, and run backend/frontend feature tests.
- **External Dependencies**: Existing PostgreSQL, Redis, authentication, storage, and production Docker environment; no new external provider required.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST allow Admin users to view and edit every supported teacher account, profile, contact, public identity, media, and content-visibility field from the teacher management workflow.
- **FR-002**: The system MUST allow Admin users to set a new teacher password without returning or displaying the current password.
- **FR-003**: The system MUST validate all editable teacher fields and reject invalid, duplicate, incomplete, or inconsistent submissions atomically.
- **FR-004**: The system MUST restrict teacher data and visibility mutations to Admin authorization on the backend.
- **FR-005**: The system MUST expose independent teacher visibility and content visibility states, each with explicit hide and show actions.
- **FR-006**: When teacher visibility is hidden, the system MUST exclude the teacher from all student- and visitor-facing teacher lists, details, search, recommendations, and public teacher-related surfaces.
- **FR-007**: When content visibility is hidden, the system MUST exclude the affected teacher content from student- and visitor-facing courses, packages, search, recommendations, related content, and public content surfaces.
- **FR-008**: When content visibility is hidden, the system MUST deny direct content access for visitors, students, and previous purchasers while preserving purchase, grant, financial, and academic records.
- **FR-009**: Showing a teacher or content item MUST restore its eligible public/student visibility and access without recreating or deleting historical records.
- **FR-010**: The system MUST prevent hidden teacher/content data from being returned through cached, paginated, nested, related, or direct-identifier responses.
- **FR-011**: The system MUST record every successful teacher data or visibility mutation in the existing audit mechanism with actor, target, old state, and new state.
- **FR-012**: Repeating an already-applied hide/show state MUST be safe and must not create duplicate state transitions or inconsistent records.
- **FR-013**: The management UI MUST display current teacher and content visibility states, loading state, validation errors, save errors, and successful completion feedback.

### Key Entities

- **Teacher account/profile**: The teacher's login identity, personal/contact fields, public profile fields, media references, and operational settings.
- **Teacher visibility state**: The reversible state controlling whether the teacher is discoverable to students and visitors.
- **Teacher content visibility state**: The reversible state controlling whether content owned or published by the teacher is discoverable and accessible.
- **Historical access/purchase records**: Existing purchase, grant, financial, academic, and audit records that remain preserved while hidden.
- **Audit record**: The actor, target, previous values, new values, timestamp, and correlation information for each successful mutation.

## Success Criteria

### Measurable Outcomes

- **SC-001**: An Admin can edit and save all supported teacher fields in one management workflow in under 3 minutes for a normal record.
- **SC-002**: 100% of unauthorized mutation attempts in automated permission tests return a denial and leave teacher data/visibility unchanged.
- **SC-003**: Within 2 seconds of a successful hide operation, refreshed student and visitor queries exclude the teacher/content and direct protected access is denied.
- **SC-004**: 100% of tested historical purchase records remain present after hiding and become usable again after showing.
- **SC-005**: 100% of successful data and visibility mutations create an auditable before/after record.
- **SC-006**: Repeated hide/show requests produce no duplicate state transitions in idempotency tests.

## Assumptions

- The existing teacher management, authentication, content ownership, access-grant, cache, and audit patterns will be reused where they satisfy this feature.
- “All data” means all supported teacher account/profile/media/contact/public/content-related fields exposed by the existing domain, while secret password values remain write-only.
- Teacher visibility and content visibility are independent and reversible states.
- Hidden content is unavailable to previous purchasers until an Admin shows it again.
- Existing student and visitor surfaces are within scope even when they use different queries or cached projections.
- Mobile-specific layout changes are limited to preserving the existing responsive management workflow; no separate mobile app is required.
