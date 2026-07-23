# Feature Specification: Student Academic Scope Enforcement

**Feature Branch**: `159-student-academic-scope-enforcement`  
**Created**: 2026-07-06  
**Status**: Draft  
**Input**: User description: "كل شيء في بوابة الطالب بالكامل لازم يتربط بالمرحلة والصف والمادة. الطالب لا يرى ولا يشتري ولا يفعل إلا المناسب لمرحلته وصفه ومواد صفه، مع وجود محتوى عام صريح للمنصة."

## Clarifications

### Session 2026-07-06

- Q: ما مستويات النطاق العام المسموحة للمحتوى الطلابي؟ → A: يوجد 3 مستويات: عام للمنصة، عام لكل صفوف مرحلة محددة، وعام لكل مواد صف محدد.
- Q: هل يمكن للعنصر الطالب-المواجه أن يستهدف أكثر من تركيبة مرحلة/صف/مادة؟ → A: كل عنصر يمكن أن يملك عدة نطاقات أكاديمية، ويكفي تطابق نطاق واحد للظهور أو السماح بالفعل.
- Q: كيف يحسم النطاق في تسلسل باقة/ترم/قسم/درس/فيديو أو امتحان؟ → A: العنصر يرث نطاق أقرب أب صريح، لكن إذا عرّف نطاقا خاصا به فيجب أن يطابق الطالب أيضا.
- Q: إذا تغير صف الطالب أو خريطة المواد بعد شراء أو منحة، ما مصير الوصول القائم؟ → A: يمنع الوصول فورا عند عدم المطابقة الحالية، مع بقاء سجل الشراء أو المنحة دون حذف.
- Q: متى يتم التحقق الأكاديمي للأكواد والكوبونات والهدايا عند عدم وجود طالب محدد وقت الإنشاء؟ → A: التحقق يتم وقت الإنشاء لضمان نطاق الهدف، ثم يعاد وقت الاستخدام أو التسليم حسب الطالب الفعلي.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filter Every Student Surface (Priority: P1)

As a student, I only see teachers, content, packages, exams, community posts, offers, notifications, shared packages, and any other student portal item that matches my education stage, grade level, and allowed subjects, or is explicitly covered by one of the allowed general scopes: platform-wide, all grades in a specific stage, or all subjects in a specific grade.

**Why this priority**: This is the core business rule. If listing pages leak unrelated items, purchase and access controls become confusing and unsafe.

**Independent Test**: Create two students in different stages/grades, create matching, non-matching, and general-scope items across student portal surfaces, then verify each student only sees matching or applicable general-scope items.

**Acceptance Scenarios**:

1. **Given** a student profile with `Secondary / FirstSecondary`, **When** the student opens packages, teachers, public exams, shared packages, community, notifications, and offers, **Then** each list contains only matching items or items explicitly covered by platform-wide, stage-wide, or grade-all-subjects general scope.
2. **Given** a teacher, community post, package, or offer linked to a subject not allowed for the student's stage/grade, **When** the student opens the relevant page, **Then** the item is absent from the API response and UI.
3. **Given** an item explicitly configured as platform-wide, **When** any student opens the relevant page, **Then** the item is visible regardless of the student's stage, grade, or subjects.
4. **Given** an item explicitly configured as general for all grades in a specific stage or all subjects in a specific grade, **When** a student within that stage or grade opens the relevant page, **Then** the item is visible without requiring a specific subject match.
5. **Given** an item has multiple academic scopes, **When** a student matches at least one scope, **Then** the item is visible and actionable according to all other non-academic rules.
6. **Given** a child item has no explicit academic scope, **When** its nearest scoped parent matches the student, **Then** the child item inherits that parent scope for visibility and access checks.
7. **Given** a child item defines its own academic scope, **When** the parent matches but the child does not match the student, **Then** the child item is hidden or denied.

---

### User Story 2 - Block Invalid Purchase, Code, Coupon, and Gift Flows (Priority: P1)

As a student, I cannot buy, redeem, or receive access to content outside my stage, grade, and allowed subjects unless the target is explicitly covered by platform-wide, stage-wide, or grade-all-subjects general scope.

**Why this priority**: Visibility filtering alone is insufficient because students may submit direct URLs, codes, coupons, or gifts.

**Independent Test**: Attempt direct purchase, code activation, coupon application, and admin gift creation for non-matching targets and verify every flow rejects the action without granting access.

**Acceptance Scenarios**:

1. **Given** a code that opens `SecondSecondary` content, **When** a `FirstSecondary` student redeems it, **Then** redemption is rejected with a clear message and no grant is created.
2. **Given** a coupon targeting a non-matching package or exam, **When** a student previews or applies it, **Then** the coupon is not usable for that student and the price is not discounted.
3. **Given** an admin attempts to gift non-matching content to a student, **When** the gift is saved or delivered, **Then** the action is rejected unless the target is covered by a general scope that includes that student.
4. **Given** a direct content URL for a non-matching lesson, video, exam, teacher profile, or community scope, **When** the student requests it, **Then** the system denies access without exposing protected details.
5. **Given** a student previously received access to content that no longer matches the student's current stage, grade, or allowed subjects, **When** the student tries to open or use that content, **Then** access is denied while the historical purchase or grant record remains unchanged.
6. **Given** an admin creates a code, coupon, or gift for a student-facing target, **When** the target has no valid academic or general scope, **Then** creation is rejected.
7. **Given** a valid scoped code, coupon, or gift exists without a student selected at creation time, **When** a student redeems, applies, or receives it, **Then** the system re-checks that student's current academic eligibility before granting or discounting access.

---

### User Story 3 - Require Academic Targeting at Admin Creation Time (Priority: P2)

As an admin, when I create or publish student-facing items, I must either choose stage, grade, and subject targeting or explicitly choose one of the allowed general scopes so it can be intentionally visible to the intended student population.

**Why this priority**: Future data must be clean. Student filtering will not stay reliable if new student-facing items can be created without explicit scope.

**Independent Test**: Use admin creation forms and APIs for content, public exams, sales, gifts, codes, shared packages, community, offers, and notifications, then verify missing scope is rejected unless one of the allowed general scopes is selected.

**Acceptance Scenarios**:

1. **Given** an admin creates a student-facing item, **When** neither academic scope nor one of the allowed general scopes is provided, **Then** the save or publish action is blocked with a clear validation message.
2. **Given** an admin marks an item platform-wide, stage-wide, or grade-all-subjects general, **When** the item is published, **Then** it becomes visible to the students included by that selected scope according to non-academic rules such as active status and moderation.
3. **Given** an admin targets an item to a subject, **When** the subject is not allowed for the selected stage/grade, **Then** the system rejects the configuration.
4. **Given** an admin assigns multiple academic scopes to one item, **When** each scope is valid, **Then** the item is saved and later matches any student covered by at least one of those scopes.

---

### User Story 4 - Keep Student Experience Clear When Nothing Matches (Priority: P3)

As a student, when no items match my stage, grade, or subjects, I see clear empty states instead of unrelated content or broken pages.

**Why this priority**: Strict filtering can produce empty lists. The portal must remain understandable and navigable.

**Independent Test**: Use a student whose grade has no matching content and verify every student surface loads successfully with an explicit empty state.

**Acceptance Scenarios**:

1. **Given** no matching packages or teachers exist for a student, **When** the student opens those pages, **Then** the page shows an empty state explaining that no content is available for the student's grade yet.
2. **Given** platform-wide, stage-wide, or grade-all-subjects general items exist but grade-specific subject items do not, **When** the student opens a page, **Then** the applicable general items still appear.

---

### Edge Cases

- Student has no `StudentProfile`, missing stage, or missing grade: student portal data requests must fail closed and show a profile-completion or unavailable state instead of returning unrestricted data.
- Existing records do not have academic scope: student-facing APIs must not return them unless they are migrated or explicitly assigned one of the allowed general scopes.
- Existing `Package.TargetGrade` values use older aliases such as `1st Secondary`: matching must normalize known aliases so valid legacy content does not disappear after migration.
- Student changes grade after receiving access, or the allowed-subject mapping changes: visibility and access checks must use the current profile and current subject mapping immediately; non-matching existing purchases or grants remain as historical records but no longer permit use.
- General-scope content must still respect unrelated restrictions such as active/published status, moderation status, ownership, expiration, and payment state.
- Multiple teacher/subject relationships may exist: a teacher appears to a student only when at least one teacher subject is allowed for the student's stage/grade or the teacher profile is explicitly covered by a general scope that includes the student.
- A subject can be allowed for multiple stages/grades: matching must use the stage/grade/subject mapping, not subject name text.
- A student-facing item can have multiple academic scopes; matching any one scope is sufficient for visibility, purchase, redemption, gift delivery, or grant creation, provided all unrelated restrictions also pass.
- Hierarchical content such as package, term, section, lesson, video, and exam inherits the nearest explicit parent scope only when the child has no explicit scope; if the child has explicit scope, both the inherited parent eligibility path and the child's own scope must allow the student.
- A child item under a matching parent must still fail closed when it defines a non-matching explicit scope.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Student at `/student/packages`, `/student/teachers`, `/student/community`, `/student/public-exams`, `/student/shared-packages`, and `/student/notifications`; verify only matching or applicable general-scope items appear.
- **Manual QA Role/Flow 2**: Admin creates a package, public exam, sales coupon/code, shared package, community post/scope, notification/offer, and gift with missing scope; verify validation blocks it. Then select platform-wide, stage-wide, or grade-all-subjects scope and verify save succeeds.
- **Manual QA Negative Check**: Student attempts direct URL access, purchase, coupon application, code redemption, and gift delivery for a non-matching target; each must be denied without grant creation.
- **Manual QA Deferred-Student Check**: Admin creates a scoped code, coupon, and gift without selecting a specific student; verify creation requires a valid target scope, then verify redemption/application/delivery is accepted only for a matching student.
- **Docker Acceptance**: Full stack starts with database migrations applied; backend health endpoint is healthy; frontend student/admin surfaces load; seeded or manual data verifies matching, non-matching, platform-wide, stage-wide, and grade-all-subjects cases.
- **External Dependencies**: No new external service is required. Existing payment/video/notification dependencies may be unavailable during local QA; academic-scope validation must still be testable with local data.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST treat student `EducationStage` and `GradeLevel` as the authoritative academic profile for all student portal visibility and access decisions.
- **FR-002**: The system MUST derive allowed student subjects from configured stage/grade-to-subject mappings, not from free-text labels or manual student subject selections.
- **FR-003**: The system MUST provide exactly three explicit general scope levels for student-facing items: platform-wide for all students, stage-wide for all grades in a selected education stage, and grade-all-subjects for all allowed subjects in a selected stage/grade.
- **FR-004**: The system MUST fail closed for student-facing items without academic scope and without one of the explicit general scopes; such items must not appear to students.
- **FR-005**: The system MUST filter every student portal list and detail API by academic scope before returning data to the frontend.
- **FR-006**: The system MUST enforce the same academic scope for direct detail access, direct purchase, coupon preview/application, code activation, gift creation/delivery, and any grant creation path.
- **FR-007**: The system MUST prevent creation or publication of new student-facing admin records unless an academic scope or one of the explicit general scopes is present.
- **FR-008**: The system MUST validate that any selected subject is allowed for the selected stage and grade before saving student-facing scope.
- **FR-009**: The system MUST preserve general-scope item behavior: platform-wide items remain visible to all students, stage-wide items remain visible to students in the selected stage, and grade-all-subjects items remain visible to students in the selected stage/grade while still respecting active, published, moderation, payment, expiration, and role rules.
- **FR-010**: The system MUST allow a student-facing item to have multiple academic scopes, and a student MUST be considered academically eligible when at least one scope matches the student's current stage, grade, and allowed subjects.
- **FR-011**: The system MUST support scope inheritance for hierarchical student content: a child item without explicit scope inherits the nearest explicit parent scope, while a child item with explicit scope must match the student through its own scope as well.
- **FR-012**: The system MUST re-evaluate existing purchases, grants, gifts, and code-based access against the student's current academic profile and current subject eligibility mapping at use time; if no scope matches, use is denied without deleting the historical record.
- **FR-013**: The system MUST validate codes, coupons, and gifts at creation time to ensure their target has valid academic scope or an explicit general scope, even when no student is selected yet.
- **FR-014**: The system MUST re-check codes, coupons, and gifts at redemption, application, or delivery time against the actual student's current academic eligibility before granting access or applying a discount.
- **FR-015**: The system MUST normalize known legacy grade aliases when matching existing package and content data.
- **FR-016**: The system MUST provide clear Arabic error messages when a student action is denied because the target is outside the student's stage, grade, or allowed subjects.
- **FR-017**: The system MUST show clear empty states on student pages when no matching records exist.
- **FR-018**: The system MUST make academic scope visible enough in admin forms and detail screens for admins to understand why an item will appear to students.
- **FR-019**: The system MUST include audit or traceable validation evidence for blocked gift/code/grant attempts where an admin initiated the action.
- **FR-020**: The system MUST ensure frontend filtering is only a presentation aid; backend APIs remain the source of truth for visibility and authorization.

### Key Entities *(include if feature involves data)*

- **Student Academic Profile**: The student's authoritative stage and grade, currently stored on the student profile and used by every student-facing scope check.
- **Academic Subject Eligibility**: A mapping that defines which subjects are allowed for each stage and grade.
- **Student-Facing Academic Scope**: A reusable concept on content, sales, community, notification/offer, and gift/code targets describing one eligibility rule for an item: exact stage/grade/subject or one explicit general scope level: platform-wide, stage-wide, or grade-all-subjects. A scoped item can have multiple academic scopes, and matching any one scope is sufficient.
- **Scoped Student-Facing Item**: Any object shown or actionable in the student portal, including packages, terms, sections, lessons, videos, exams, teachers, posts, coupons, access codes, gifts, shared packages, offers, and notifications.
- **Hierarchical Scope Inheritance**: The rule that child content without explicit scope uses the nearest explicit parent scope, while child content with explicit scope must independently match the student.
- **Access/Grant Attempt**: Any purchase, code activation, coupon application, gift delivery, or grant creation request that must be accepted only when the target is academically eligible or covered by a general scope that includes the student.
- **Deferred-Student Scope Tool**: Any code, coupon, or gift that can be created before a concrete student is known; it must have a valid scoped target at creation and must be re-validated against the concrete student at use or delivery time.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In seeded tests with at least two stages/grades and three subjects, 100% of student list APIs return only matching or applicable general-scope items.
- **SC-002**: 100% of direct access, purchase, coupon, code, and gift attempts for non-matching targets are rejected without creating access grants or financial side effects.
- **SC-003**: 100% of new student-facing admin creation flows either capture academic scope or explicitly select platform-wide, stage-wide, or grade-all-subjects scope before publish/save succeeds.
- **SC-004**: Student pages with no matching content load successfully and show clear empty states instead of errors or unrelated content.
- **SC-005**: Known legacy package grade aliases continue to match the equivalent canonical grade after migration or normalization.
- **SC-006**: In tests where a student's grade or allowed-subject mapping changes after a purchase or grant, 100% of now-non-matching access attempts are denied while the original purchase or grant record remains queryable for audit/history.
- **SC-007**: 100% of code, coupon, and gift creation attempts for unscoped targets are rejected, and 100% of redemption/application/delivery attempts by non-matching students are rejected without grants, discounts, or financial side effects.
- **SC-008**: Student-facing list and detail APIs with academic-scope filtering continue to respond within 500 ms p95 for standard seeded test datasets.

## Assumptions

- The existing registration/profile data provides enough stage and grade information for enrolled students.
- Existing subject records are reusable, but a stage/grade-to-subject eligibility mapping may need to be added or formalized.
- Every general scope level is an intentional admin choice, not the default for records missing scope.
- Existing admin/staff/teacher surfaces are not restricted by student academic scope unless they preview student-facing visibility.
- Existing hidden or inactive records remain hidden regardless of academic scope.
