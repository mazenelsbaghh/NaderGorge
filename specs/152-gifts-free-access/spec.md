# Feature Specification: Gifts and Free Access

**Feature Branch**: `152-gifts-free-access`
**Created**: 2026-06-29
**Status**: Draft
**Input**: Approved feature brief for administrator-issued content gifts and promotional balances outside payment.

## Clarifications

### Session 2026-06-29

- Q: كيف يُحسب حد الاستخدام حسب نوع الهدية؟ → A: يُحسب على مشاهدة الفيديو، محاولة الامتحان، أو عملية شراء برصيد الهدية؛ وتعتمد هدايا الحصة والباكدج على الانتهاء فقط.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Issue Direct Content Gifts (Priority: P1)

As an authorized gift manager, I want to grant selected students free access to a package, lesson, video, or exam so that support and promotional commitments can be fulfilled without recording a payment.

**Why this priority**: Direct access is the core gift outcome and reuses content that already has stable identity from Spec 151.

**Independent Test**: Select one or more students, choose a content target, enter a reason, issue the gift, and verify each valid recipient can access the target without a balance deduction.

**Acceptance Scenarios**:

1. **Given** an authorized gift manager selects active students and one valid content target, **When** the gift is issued, **Then** every valid recipient receives free access and a recipient result is recorded.
2. **Given** a recipient already has equivalent active access, **When** the same target is gifted, **Then** the system does not create conflicting duplicate access and reports that recipient as already entitled.
3. **Given** a video-only gift, **When** the recipient opens that video through its lesson context, **Then** the selected video is accessible while unrelated paid content remains locked.
4. **Given** a gift has an expiration date or per-recipient use limit, **When** the limit is reached, **Then** unused future access is denied without reversing completed activity.

---

### User Story 2 - Issue Promotional Balance Gifts (Priority: P1)

As an authorized gift manager, I want to grant general or teacher-restricted promotional balance so that students can choose eligible content later without converting the gift into teacher revenue.

**Why this priority**: The owner explicitly requires selectable credit gifts in addition to direct content access.

**Independent Test**: Grant general and teacher-restricted balances, buy eligible and ineligible content, and verify promotional funds, paid funds, restrictions, expiration, and revenue treatment.

**Acceptance Scenarios**:

1. **Given** a student receives general promotional balance, **When** the student purchases eligible content, **Then** available promotional balance is consumed before paid balance and no teacher income is created from the promotional portion.
2. **Given** a student receives teacher-restricted promotional balance, **When** the student buys that teacher's eligible content, **Then** the restricted balance may be used.
3. **Given** teacher-restricted promotional balance, **When** the student attempts to buy another teacher's content, **Then** the restricted balance is not eligible and the purchase follows normal paid-balance rules.
4. **Given** multiple promotional balances are eligible, **When** a purchase is made, **Then** the balance expiring first is consumed first, followed by unrestricted paid balance when needed.
5. **Given** promotional balance expires, **When** the student views balance or attempts a purchase, **Then** expired value is unavailable while paid balance remains unchanged.

---

### User Story 3 - Track and Revoke Remaining Gifts (Priority: P2)

As an authorized gift manager, I want to inspect gift history and revoke only the unused remainder so that mistakes can be corrected without erasing completed student activity.

**Why this priority**: Gifts affect access and value; safe correction and administrative evidence are mandatory controls.

**Independent Test**: Issue a gift, partially use it, revoke it with a reason, and verify the unused remainder is unavailable while completed use and evidence remain.

**Acceptance Scenarios**:

1. **Given** an active unused gift, **When** an authorized manager revokes it with a reason, **Then** future access or unspent promotional value is removed and the gift becomes revoked.
2. **Given** a partially used gift, **When** it is revoked, **Then** only the unused remainder is removed and completed purchases, views, or attempts remain valid.
3. **Given** a completed, expired, or already revoked gift, **When** revocation is requested again, **Then** no value is removed twice and a clear non-success result is returned.
4. **Given** a gift batch contains invalid or ineligible recipients, **When** issuance finishes, **Then** successful recipients remain issued and each failed recipient has a visible reason suitable for retry.

---

### User Story 4 - Discover Gifts Through Shell and Permissions (Priority: P2)

As an administrator, I want gift management and video-type management to be visible in the correct shell and permission surfaces so that features are discoverable only to eligible staff.

**Why this priority**: The owner explicitly requested shell and permissions verification before continuing to Spec 152.

**Independent Test**: Compare Admin, gift-manager, content-manager, and unauthorized staff navigation and direct-route behavior.

**Acceptance Scenarios**:

1. **Given** an Admin or staff member with gift-management permission, **When** the admin shell loads, **Then** the gifts entry is visible and opens the gifts workspace.
2. **Given** staff without gift-management permission, **When** shell navigation and the direct gifts route are evaluated, **Then** the entry is hidden and access is denied.
3. **Given** an Admin, **When** the content shell loads, **Then** a direct video-types entry is visible.
4. **Given** a non-Admin content manager, **When** shell navigation and the direct video-types route are evaluated, **Then** video-type catalog management remains unavailable while ordinary permitted content work continues.

### Edge Cases

- The same student appears more than once in a bulk selection.
- A recipient is inactive, deleted, or not a student when issuance executes.
- A target is deleted, inactive, moved, or no longer purchasable between selection and submission.
- A batch is retried after a network timeout or repeated submission.
- A package gift overlaps existing lesson/video gifts or purchased access.
- A direct gift expires while a student is actively viewing a video or taking an exam; the started activity is not interrupted, but a new activity is denied.
- A promotional gift covers only part of a purchase; the remainder may use other eligible promotional buckets and then paid balance.
- Concurrent purchases try to spend the same promotional remainder.
- Revocation and purchase happen concurrently.
- A teacher-restricted gift points to a teacher whose content is later reassigned; eligibility follows the teacher ownership at purchase time.
- Amount, expiration, or use-limit values are zero, negative, past-dated, or outside supported limits.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Flow**: `pending` - Admin at the gifts workspace selects students, issues each target type, inspects recipient outcomes, and revokes unused remainder.
- **Manual QA Staff Permission Flow**: `pending` - staff with and without gift-management permission compare shell visibility, direct route, issuance, and revocation behavior.
- **Manual QA Student Flow**: `pending` - recipient verifies direct access, general balance, teacher restriction, expiration, partial purchase, and unchanged paid balance.
- **Manual QA 151 Shell Closure**: `pending` - Admin sees direct video-types navigation; non-Admin content manager cannot manage the catalog.
- **Docker Acceptance**: build changed services, apply the migration to PostgreSQL, verify backend/admin/student health, run gift E2E journeys, and run null/duplicate/conservation queries.
- **External Dependencies**: no new gateway, provider credential, SMS, WhatsApp, or device is required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let an authorized gift manager create one gift issuance for one or more selected students.
- **FR-002**: Every issuance MUST require a non-empty administrative reason and record its issuing actor and issue time.
- **FR-003**: The manager MUST be able to choose exactly one target kind per issuance: package, lesson, video, exam, general promotional balance, or teacher-restricted promotional balance.
- **FR-004**: A content gift MUST reference one existing target and MUST use its stable authoritative identity.
- **FR-005**: A teacher-restricted balance gift MUST reference one existing teacher; a general balance gift MUST NOT require a teacher.
- **FR-006**: The manager MUST be able to select one student or a deduplicated group of students and review the recipient count and target summary before confirmation.
- **FR-007**: The system MUST validate the actor, recipients, target, amount, expiration, and limits again when issuance is submitted.
- **FR-008**: Bulk issuance MUST record an outcome for every requested recipient and MUST allow valid recipients to succeed when another recipient is invalid.
- **FR-009**: Repeating the same issuance request MUST NOT duplicate recipient gifts, access, or promotional value.
- **FR-010**: Direct content gifts MUST provide access without deducting paid student balance or recording a purchase payment.
- **FR-011**: Video-only gifts MUST unlock only the selected video in its lesson context and MUST NOT unlock sibling videos or the full lesson.
- **FR-012**: Package, lesson, video, and exam gifts MUST preserve existing entitlement precedence and MUST NOT remove or weaken previously purchased access.
- **FR-013**: Promotional balance MUST be tracked separately from paid balance and MUST display available, used, expired, and revoked amounts.
- **FR-014**: General promotional balance MUST be eligible for supported content purchases regardless of teacher.
- **FR-015**: Teacher-restricted promotional balance MUST be eligible only for content owned by the selected teacher at purchase time.
- **FR-016**: Promotional balance MUST be consumed by earliest expiration first and before unrestricted paid balance when it is eligible.
- **FR-017**: A purchase MAY combine multiple eligible promotional balances and paid balance, but MUST remain atomic and MUST never produce a negative balance.
- **FR-018**: No direct gift or promotional-balance-funded amount MUST create teacher revenue, commission, payout balance, or platform sales revenue.
- **FR-019**: The manager MUST be able to set an optional future expiration; an optional positive per-recipient use limit MUST be available for video views, exam attempts, and promotional-balance purchases only.
- **FR-020**: Expired gifts MUST stop providing new access or spendable value without deleting the gift record or completed usage evidence.
- **FR-021**: An authorized manager MUST be able to revoke an active gift only with a non-empty revocation reason.
- **FR-022**: Revocation MUST remove only unused future access or unspent promotional value and MUST NOT reverse completed activity or paid balance.
- **FR-023**: Revocation, expiration, retry, and concurrent purchase behavior MUST be idempotent and MUST NOT remove or grant value more than once.
- **FR-024**: The system MUST expose an ordered, searchable gift ledger with status, target, issuer, recipient totals, value/usage totals, expiration, and recipient outcomes.
- **FR-025**: The system MUST expose gift details sufficient to explain every recipient's issued, already-entitled, failed, partially used, expired, completed, or revoked state.
- **FR-026**: Gift creation, recipient outcome, consumption, expiration, revocation, and denied destructive actions MUST be auditable with actor, target, reason, timestamp, and relevant before/after values.
- **FR-027**: Gift management MUST require a dedicated gift-management permission; built-in Admin users MUST retain access regardless of delegated staff assignments.
- **FR-028**: Staff without gift-management permission MUST be denied direct route and API access without persisted changes.
- **FR-029**: The Admin Shell MUST show a gifts entry only to Admin users or staff with gift-management permission.
- **FR-030**: The permission-management surface MUST describe and allow assignment of gift-management permission to eligible staff roles.
- **FR-031**: The Admin Shell MUST include a direct video-type management entry visible only to built-in Admin users.
- **FR-032**: Video-type route and mutation protection MUST remain Admin-only while type listing for ordinary video forms continues to follow existing content permission.
- **FR-033**: The student experience MUST distinguish paid balance from promotional balance and explain teacher or expiration restrictions without exposing administrative notes.
- **FR-034**: Gift state MUST not depend on external messaging delivery; notifications outside the existing in-app behavior are out of scope.

### Key Entities

- **Gift Issuance**: Administrative instruction defining target kind, target, optional teacher, optional amount, expiration, use limit, reason, issuer, status, and idempotency identity.
- **Gift Recipient**: Per-student outcome and lifecycle for an issuance, including entitlement/value granted, usage, failure reason, and revocation/expiration state.
- **Promotional Balance Allocation**: Restricted or general value belonging to one student, with original, available, consumed, expired, and revoked amounts.
- **Promotional Balance Usage**: Immutable evidence linking a purchase to the promotional allocations that funded it.
- **Access Grant**: Existing student entitlement used by direct content gifts without changing paid purchase ownership.
- **Audit Entry**: Administrative and value-movement evidence for issuance, consumption, expiration, revocation, and denied actions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An authorized manager can issue a valid single-target gift to up to 100 selected students in under 3 minutes and see an outcome for every recipient.
- **SC-002**: 100% of successful content-gift recipients can access the selected target without paid-balance deduction, while unrelated content remains governed by existing access rules.
- **SC-003**: 100% of teacher-restricted promotional-balance tests reject ineligible teachers and permit the selected teacher without creating teacher revenue.
- **SC-004**: Across concurrent purchase and revocation tests, promotional available value never becomes negative and original value always equals consumed plus available plus expired plus revoked value.
- **SC-005**: 100% of repeated issuance and revocation requests produce no duplicate value or duplicate access.
- **SC-006**: Every issuance, recipient failure, consumption, expiration, and revocation tested is explainable from the administrative ledger and audit evidence.
- **SC-007**: Admin, delegated gift manager, ordinary content manager, and unauthorized staff each see only the shell entries and actions permitted to their role.
- **SC-008**: Existing paid purchases, code activation, content playback, exams, and teacher accounting regression tests continue to pass unchanged.

## Assumptions

- Cancellation approval means revoking only the unused remainder; completed student activity remains valid.
- Promotional value is not cash, cannot be withdrawn or transferred, and never creates teacher/platform sales revenue.
- Eligible promotional value is consumed before paid balance to minimize accidental expiration.
- Direct package, lesson, and exam gifts use existing access semantics; video gifts use the existing targeted-video access capability.
- Per-recipient use limits count successful video sessions, exam attempts, or promotional-balance-funded purchases; lesson/package gifts do not expose a use limit, and failed attempts do not consume one.
- Started video sessions or exam attempts may finish if the gift expires during that activity; subsequent starts require active entitlement.
- Group selection is an explicit list of students in this Spec; saved dynamic audience rules are out of scope.
- Existing authentication, student status, content ownership, purchase, and audit systems remain authoritative.
- Discounts, coupons, printable codes, public standalone exams, external notifications, and gift-funded teacher revenue are separate work.
