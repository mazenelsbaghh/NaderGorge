# Feature Specification: Teacher Accounting Phase 3

**Feature Branch**: `codex/158-teacher-accounting-phase3`  
**Created**: 2026-07-04  
**Status**: Draft  
**Input**: User description: "Phase 3: المدرسين والحسابات كاملة من docs/platform-change-roadmap.md، مع توزيع مالي ديناميكي، باكدج مشترك يدعم النسب والمبالغ الثابتة، ظهور اسم الطالب ورقم الهاتف والكود/المحتوى للمدرس، ومراجعة إدارية قبل الصرف."

## Clarifications

### Session 2026-07-04

- Q: بعد ما الإدارة توافق على مستحقات المدرس، هل النظام يعتبرها مصروفة فورًا ولا جاهزة للصرف فقط؟ → A: الموافقة تجعل المستحقات جاهزة للصرف، ثم الأدمن يسجل "تم الصرف" بعد التحويل الفعلي.
- Q: لو عملية شراء أو كود اتحسبت للمدرس ثم حصل refund/إلغاء بعد كده، نعمل إيه في حساب المدرس؟ → A: قبل الصرف يتم عكسها مباشرة؛ بعد الصرف تسجل كتسوية سالبة/دين على المدرس في الدورة القادمة.
- Q: لو الطالب أخذ وصول مجاني أو كود خصمه 100% لمحتوى مدرس، هل يظهر في حساب المدرس كربح؟ → A: يظهر كعملية متابعة بقيمة صفر، ولا يضيف مستحقات إلا لو الأدمن حدد تعويضا صريحا.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Teacher views daily income and transaction details (Priority: P1)

As a teacher, I need a finance view that shows today's income, total/current balances, commissions, a daily calendar, and the transactions behind each day so I can understand my earnings without asking administration.

**Why this priority**: This is the core Phase 3 outcome and the foundation for payouts, review, and multi-teacher packages.

**Independent Test**: Can be tested by recording a purchase or code activation tied to one teacher, opening the teacher finance surface, selecting the relevant day, and verifying the teacher sees the correct income and transaction details.

**Acceptance Scenarios**:

1. **Given** a student purchases content tied to one teacher, **When** that teacher opens today's finance view, **Then** the teacher sees the transaction in today's income totals with student name, phone number, content or code reference, gross price, discount, teacher share, platform share, and net teacher earning.
2. **Given** a teacher has transactions on multiple days, **When** the teacher opens the calendar, **Then** each day with income shows its total and selecting a day shows only that day's transactions.
3. **Given** a teacher has no transactions for a selected day, **When** the teacher selects that day, **Then** the view shows an empty state without showing unrelated transactions.

---

### User Story 2 - Admin reviews teacher dues before payout (Priority: P1)

As an admin, I need a review queue and payout report for teacher dues so suspicious or incomplete financial records do not get paid before review.

**Why this priority**: Phase 3 requires separation between teacher balances and platform balances, plus explicit approval, rejection, or hold before teacher payout.

**Independent Test**: Can be tested by creating valid and suspicious financial records, opening the admin review/report surface, approving one payout, holding another, and verifying balances and statuses update consistently.

**Acceptance Scenarios**:

1. **Given** a teacher has reviewed payable transactions, **When** an admin approves payout, **Then** the teacher due status becomes ready for payout, remains separated from unreviewed amounts, and does not become paid until an admin records the actual transfer.
2. **Given** a financial transaction has no clear teacher or no clear product, **When** the system evaluates teacher earnings, **Then** the transaction is not counted as direct teacher profit and appears in the admin review queue.
3. **Given** an admin rejects or holds a payout, **When** the teacher and admin view financial summaries, **Then** the rejected or held amount remains traceable and is not counted as paid.

---

### User Story 3 - Admin creates and sells a multi-teacher package (Priority: P1)

As an admin, I need a separate package flow for packages that contain multiple teachers and subjects, with dynamic financial distribution, so the platform can sell shared packages and record each teacher's share.

**Why this priority**: Shared packages are explicitly part of Phase 3 and directly depend on correct teacher/platform accounting.

**Independent Test**: Can be tested by creating a shared package, assigning teachers and subjects, configuring percentage or fixed-amount shares, buying the package as a student, and verifying each teacher sees only their own share while the admin sees the full allocation.

**Acceptance Scenarios**:

1. **Given** an admin creates a shared package, **When** the admin assigns teachers and subjects and chooses percentage or fixed-amount shares, **Then** the package cannot be saved unless the distribution is valid and any undistributed remainder is clearly assigned to the platform.
2. **Given** a student buys a shared package, **When** the purchase succeeds, **Then** the student can access the package content according to its teacher/subject structure and the accounting ledger records each teacher share plus the platform share.
3. **Given** multiple teachers participate in the same package purchase, **When** each teacher opens finance, **Then** each teacher sees only their own share and not other teachers' shares.

---

### User Story 4 - Student browses teacher public profile and community (Priority: P2)

As a student, I need a public teacher profile that shows the teacher's subjects, packages, lessons, intro video, ratings, and teacher-specific community so I can evaluate and follow the right teacher.

**Why this priority**: Teacher profile and community are part of Phase 3, but they depend on stable ownership and teacher/content relationships.

**Independent Test**: Can be tested by opening a teacher profile from a student account before and after purchase and verifying visible content, package links, teacher information, ratings, and moderated community posts.

**Acceptance Scenarios**:

1. **Given** a teacher has public profile data and published content, **When** a student opens the teacher profile, **Then** the student sees subjects, packages, lessons or available previews, intro video, ratings, and teacher community areas allowed for that student.
2. **Given** a student has not purchased paid content, **When** the student opens a teacher profile, **Then** paid content remains gated while public teacher information remains visible.
3. **Given** a teacher community post or comment requires moderation, **When** it appears under the teacher profile, **Then** existing moderation rules still apply.

---

### User Story 5 - Financial ownership is preserved across codes, purchases, and public exams (Priority: P2)

As an admin and platform operator, I need purchases, code activations, packages, and public exams to create consistent teacher/platform financial records so every earning can be audited later.

**Why this priority**: The finance views and payouts are only reliable if all earning sources record consistent ownership and allocation.

**Independent Test**: Can be tested by running each supported monetization path and verifying the resulting financial record has a clear product, teacher/platform ownership, distribution, review status, and no duplicate or final record on failed operations.

**Acceptance Scenarios**:

1. **Given** a code activation grants access to teacher-owned content, **When** the code is consumed successfully, **Then** the teacher/platform financial effect is recorded once and tied to the consumed code and product.
2. **Given** a purchase or activation fails, **When** the failure is returned to the user, **Then** no final teacher earning is recorded.
3. **Given** a monetized item is not tied to a teacher or product, **When** the system processes it, **Then** it is blocked from direct teacher earning and routed to review or rejected according to the business rule.

### Edge Cases

- A shared package distribution mixes percentage and fixed-amount teacher shares.
- A shared package distribution exceeds the package price.
- A shared package distribution leaves a remainder that must be clearly assigned to the platform share.
- A teacher-owned transaction is refunded, cancelled, rejected, or held before payout.
- A teacher-owned transaction is refunded or cancelled after payout has already been recorded.
- A teacher has no income for a day, month, or entire period.
- A student purchase succeeds but later access grant creation fails.
- A code is activated more than once or retried after a partial failure.
- A teacher profile has no published content, no ratings, no intro video, or no community posts.
- A student tries to access paid teacher content from a profile without entitlement.
- A teacher attempts to view another teacher's transaction details.
- A student receives teacher-owned access for free or through a 100% discount.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Teacher Finance**: Teacher role opens `/teacher/finance` or the equivalent teacher finance surface, views today's totals, selects a calendar day, and sees only that teacher's transaction details with student name, phone number, content/code reference, pricing, and shares.
- **Manual QA Admin Review**: Admin role opens the teacher dues/review surface, filters a teacher, sees payable, held, rejected, and suspicious records, and can approve, reject, or hold a payout.
- **Manual QA Shared Package**: Admin creates a multi-teacher package with percentage and fixed-amount shares; student buys it; each teacher sees only their own share; admin sees the full distribution.
- **Manual QA Teacher Profile**: Student opens a teacher profile before and after purchase and verifies public profile information, gated paid content, ratings, and moderated community content.
- **Manual QA Negative Check**: Teacher cannot view another teacher's transactions, hidden student data beyond the confirmed allowed fields, or other teachers' shared-package shares.
- **Docker Acceptance**: Production-like Docker stack builds, database migrations apply, all containers become healthy, backend APIs and frontend surfaces load, and finance-related smoke flows do not produce server errors.
- **External Dependencies**: No new external payment provider is assumed. Existing payment, code activation, moderation, and authentication flows remain the source of truth for their respective events.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST maintain a financial record for every successful monetized teacher/platform event that can affect teacher dues, including purchases, code activations, packages, shared packages, and public exams when they are monetized.
- **FR-002**: Each teacher-affecting financial record MUST include enough business information for audit: student name, student phone number, content or code reference, product type, gross price, discount, teacher share, platform share, net teacher earning, event date, and review/payout status.
- **FR-003**: The system MUST prevent direct teacher earning from records that do not have a clear teacher or a clear product, and MUST route those records to review or reject them with an observable reason.
- **FR-004**: Teachers MUST be able to view today's income, total/current balances, commissions, calendar totals, and day-level transaction details for their own earnings only.
- **FR-005**: Teacher finance views MUST NOT show another teacher's earnings or shared-package shares.
- **FR-006**: Admins MUST be able to view teacher dues, suspicious records, platform share totals, and payout-review status separately from teacher-visible summaries.
- **FR-007**: Admins MUST be able to approve, reject, or hold teacher payout records before they become payable or paid.
- **FR-007a**: Approval MUST mark teacher dues as ready for payout only; a separate admin action MUST record the actual transfer and mark the dues as paid.
- **FR-008**: Payout review actions MUST preserve an audit trail showing who performed the action, when it happened, the status transition, the amount, and any required note or reason.
- **FR-009**: The system MUST separate teacher balances from platform balances in reporting and settlement views.
- **FR-010**: Admins MUST be able to create a shared package that is distinct from the existing single-owner package flow.
- **FR-011**: A shared package MUST allow an admin to set the package price, choose participating teachers, choose subjects/content included for each teacher, and configure each teacher's financial share.
- **FR-012**: Shared package distribution MUST support both percentage shares and fixed-amount shares for teachers.
- **FR-013**: Shared package distribution MUST reject invalid allocations where teacher shares exceed the package price or leave an ambiguous remainder.
- **FR-014**: Any valid remainder after teacher shares MUST be recorded explicitly as platform share.
- **FR-015**: When a student buys a shared package, the system MUST grant student access to the package content according to the package's teacher/subject/content structure.
- **FR-016**: When a student buys a shared package, the system MUST record each participating teacher's share and the platform share from the same purchase event.
- **FR-017**: A teacher participating in a shared package MUST see only their own share and relevant transaction details, while admins can see the full distribution.
- **FR-018**: Students MUST be able to open a public teacher profile showing the teacher's public information, subjects, packages, lessons or previews, intro video, ratings, and teacher-specific community area.
- **FR-019**: Teacher profile paid content MUST remain gated according to the student's existing entitlements.
- **FR-020**: Teacher community posts and comments shown on the teacher profile MUST continue to follow existing moderation rules.
- **FR-021**: Failed, cancelled, or duplicate purchase/code operations MUST NOT create duplicate final teacher earnings.
- **FR-021a**: Refunds or cancellations before payout MUST reverse the unpaid teacher earning directly; refunds or cancellations after payout MUST create a negative adjustment or debt carried into a later payout cycle.
- **FR-021b**: Free access and 100% discount events for teacher-owned content MUST be visible as zero-value tracking events and MUST NOT create teacher dues unless an admin explicitly records a compensation amount.
- **FR-022**: Empty, loading, failed, held, rejected, approved, and paid states MUST be visible and understandable on teacher and admin finance surfaces.
- **FR-023**: The roadmap Phase 3 checklist in `docs/platform-change-roadmap.md` MUST be updated only after the implemented behavior is verified.

### Key Entities *(include if feature involves data)*

- **Teacher Financial Event**: A business record representing a monetized event that may create teacher and platform shares.
- **Teacher Share Allocation**: The teacher-specific portion of a financial event, expressed as either a percentage or fixed amount and resolved to an actual amount.
- **Platform Share Allocation**: The platform portion of a financial event after discounts and teacher shares are applied.
- **Teacher Payout Review**: A reviewable status and decision history for teacher dues before payout.
- **Teacher Payout Adjustment**: A reversal or negative adjustment created by refund, cancellation, or correction events after the original earning was recorded.
- **Shared Teacher Package**: A purchasable package composed of multiple teachers, subjects, and content groupings with a defined financial distribution.
- **Teacher Profile**: Public teacher-facing information and student-visible content/community aggregation.
- **Teacher Community Scope**: The relationship that groups community posts and comments under a teacher profile while preserving moderation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In automated and manual tests, 100% of successful teacher-owned purchase/code/public-exam/shared-package operations create exactly one canonical teacher financial event with no duplicate final earning rows.
- **SC-002**: In at least 5 items across representative finance test cases, the relevant teacher can see the transaction in the correct day view with student, content/code, gross, discount, teacher share, platform share, and net earning matching backend records to 0.01 EGP.
- **SC-003**: For every shared package purchase test, the sum of all participating teacher shares plus platform share equals the final paid package amount to 0.01 EGP.
- **SC-004**: Authorization tests cover at least 3 requests for cross-teacher access attempts and all return forbidden/not found without exposing another teacher's financial data.
- **SC-005**: Admin review tests cover approve, reject, hold/pending-review, approve-to-ready, and mark-paid transitions while preserving one immutable audit trail per transition.
- **SC-006**: 100% of financial events without a clear teacher or product in tests are routed to review or rejected and do not increase payable teacher balance.
- **SC-007**: Profile tests cover before-purchase and after-purchase views for at least one teacher, with paid content gated before entitlement and accessible/marked after entitlement.
- **SC-008**: Roadmap Phase 3 checkboxes are updated only after required automated commands pass and the manual QA checklist records pass/blocker status for each Phase 3.1-3.4 flow.

## Assumptions

- Existing authentication, roles, student purchase, code activation, moderation, and entitlement systems remain the source of truth.
- Existing teacher/content relationships from prior multi-teacher specs are reused and not redefined unless planning finds a blocking inconsistency.
- Dynamic financial allocation is configured by admins per product, code, or package instead of relying on one global fixed teacher percentage.
- External payment provider behavior is not changed by this feature; this feature consumes successful/failed business events from existing flows.
- Teacher visibility intentionally includes student name and phone number because the user confirmed that requirement.
