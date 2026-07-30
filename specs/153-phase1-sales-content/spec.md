# Feature Specification: Phase 1 Sales and Content Completion

**Feature Branch**: `153-phase1-sales-content`
**Created**: 2026-06-29
**Status**: Draft
**Input**: User-approved Arabic Feature Brief for completing Phase 1: sales/content purchase rules, advanced coupons, printable sales codes, simple code-template designer, and standalone public exams.

## Clarifications

### Session 2026-06-29

- Q: هل مسموح بتجميع أكثر من خصم/كود على نفس عملية الشراء؟ → A: الأدمن يحدد سياسة التجميع من داخل الإدارة لكل حالة، مع منع أي خصم يتجاوز حدود الكود أو يجعل المبلغ النهائي سالباً.
- Q: ماذا يحدث عند تعطيل امتحان عام بعد أن اشتراه طلاب؟ → A: تعطيل الامتحان يمنع شراء أو بدء محاولات جديدة، لكنه يحافظ على المحاولات والنتائج السابقة للطلاب الذين بدأوا أو أكملوا الامتحان.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sell by Content Type Rules (Priority: P1)

As an administrator, I want content that is sold or unlocked by codes to have dependable teacher, subject, and video-type associations so that purchases, discounts, and future accounting cannot target ambiguous content.

**Why this priority**: Coupons, printed codes, and teacher/platform revenue splits depend on a clear sale target. If content can be sold without the required ownership/classification, later financial records become unreliable.

**Independent Test**: Configure a purchase rule for a specific video type, attempt to sell eligible and ineligible videos, and verify only correctly classified content can be purchased or unlocked.

**Acceptance Scenarios**:

1. **Given** an administrator creates or edits sellable video content, **When** the content is marked as available for sales or code-based unlocking, **Then** the system requires a valid teacher, subject, and active video type before it can be sold.
2. **Given** a purchase or code targets a video type, **When** a student uses it, **Then** only content matching that active type and the configured teacher/platform scope is eligible.
3. **Given** content lacks a required teacher, subject, or active type, **When** an administrator attempts to publish it for sales, **Then** the action is blocked with a clear reason and no sale rule is activated.

---

### User Story 2 - Manage Digital Discount Coupons (Priority: P1)

As an authorized admin or sales employee, I want to create digital coupons with scope, percentage/fixed value, limits, expiry, and teacher/platform ownership so students can receive controlled discounts without manual balance edits.

**Why this priority**: Digital coupons are the core financial workflow for discounts and must be auditable before printable sales codes or public exam sales rely on the same rules.

**Independent Test**: Create one percentage coupon and one fixed-value coupon with different scopes and limits, apply them to valid and invalid purchases, and verify discount amount, limit usage, expiry, and audit evidence.

**Acceptance Scenarios**:

1. **Given** an authorized employee creates a coupon, **When** they select target scope, discount type, value, expiry, global limit, per-student limit, and owner, **Then** the coupon becomes available only within those constraints.
2. **Given** a student applies a valid coupon to an eligible package, lesson, general exam, teacher, or platform purchase, **When** checkout is calculated, **Then** the payable amount is reduced by the allowed percentage or fixed value.
3. **Given** the coupon is expired, disabled, over limit, already consumed by the student, or outside target scope, **When** the student applies it, **Then** the coupon is rejected and no purchase, access, or usage counter is changed.
4. **Given** a coupon discount applies to teacher-owned content, **When** the purchase completes, **Then** the discount impact is split between teacher and platform according to the configured revenue distribution.

---

### User Story 3 - Create Printable QR/Serial Sales Codes (Priority: P2)

As an administrator, I want to generate printable sales codes with QR, code text, serial number, owner, target, expiry, and usage rules so offline sales can be tracked and redeemed safely.

**Why this priority**: Printed sales codes support field sales and require the same audit, limit, and financial controls as coupons while adding batch/print evidence.

**Independent Test**: Generate a batch of printable codes for a target, redeem one as a student, try to redeem it again beyond allowed limits, and verify the batch, serial, QR payload, and audit trail.

**Acceptance Scenarios**:

1. **Given** an authorized employee generates a printable code batch, **When** they select target, owner, value/access behavior, limits, expiry, and quantity, **Then** every code has a unique code and serial number.
2. **Given** a student scans or enters an unused valid code, **When** the code is within scope and limits, **Then** the configured discount, access, or credit is applied exactly once or according to the configured usage rule.
3. **Given** a code is disabled, expired, already consumed, or outside the student's eligible purchase, **When** redemption is attempted, **Then** the system rejects it with a clear reason and records the attempt where appropriate.
4. **Given** a printed code belongs to a teacher or the platform, **When** it is redeemed into a purchase, **Then** ownership and discount/revenue effects remain traceable to that source.

---

### User Story 4 - Design Simple Code Templates (Priority: P2)

As an administrator, I want a simple in-admin template designer for printed code cards so I can place fixed elements such as QR, code, serial, owner, price, and expiry on a reusable template.

**Why this priority**: Printable codes need a repeatable output that can be inspected before printing without introducing a full graphic-design system.

**Independent Test**: Create a template, drag/drop fixed fields, save it, generate a preview for a code batch, and verify the rendered card includes QR, code, serial, and selected metadata.

**Acceptance Scenarios**:

1. **Given** an administrator opens the template designer, **When** they add allowed fixed elements and arrange them, **Then** the template can be saved and reused for future batches.
2. **Given** a saved template and a code batch, **When** the administrator previews or exports cards, **Then** each card displays the unique QR, code, serial, and configured metadata.
3. **Given** a template is missing a required redemption identifier, **When** the administrator tries to use it for printable codes, **Then** the system blocks usage until a QR or code element is present.

---

### User Story 5 - Sell Standalone Public Exams (Priority: P1)

As an administrator, I want to publish general exams as independent free or paid products with selected teacher, subject, grade, or platform scope so students can buy or enter exams that are not tied to lesson videos or packages.

**Why this priority**: Public exams are a direct Phase 1 sales product and must not be confused with lesson-video exams or the question bank itself.

**Independent Test**: Create a paid public exam with selected classifications, buy it as a student, take it independently of a lesson/package, and verify its results appear in a separate public-exam report.

**Acceptance Scenarios**:

1. **Given** an administrator creates a public exam, **When** they choose pricing and classifications such as teacher, subject, grade, or platform-wide scope, **Then** the exam becomes a standalone product outside lesson/video/package exam access.
2. **Given** a public exam is free, **When** an eligible student opens it, **Then** the student can start it without a payment while the attempt remains reported as a public exam.
3. **Given** a public exam is paid, **When** a student purchases it successfully, **Then** the student receives access to that exam only and the purchase is auditable.
4. **Given** a public exam uses questions from the question bank, **When** results are reported, **Then** the report treats the exam as an independent product and does not mutate the question bank into a product.

### Edge Cases

- Coupon or code value is greater than purchase price; payable amount must not become negative.
- Percentage coupon and fixed coupon target the same purchase; the administrator-configured stacking policy must decide whether they can combine, and the final payable amount must never become negative.
- Coupon/code is applied during checkout but the purchase fails or is cancelled; usage counters and redemption state must not be consumed.
- A teacher-owned coupon targets content whose teacher cannot be resolved; the purchase must be blocked until ownership is clear.
- Public exam is deactivated after a student purchased it; new purchases and new attempts are blocked, while previous attempts and results remain visible and auditable.
- Public exam has no questions, expired availability, or missing classification; publishing must be blocked with a clear reason.
- Printed code batch generation partially fails; no duplicate or partially redeemable codes may be exposed.
- A template element is moved outside the printable area; preview/export must prevent unreadable cards.
- Unauthorized staff attempts to create, disable, export, or redeem-management records; access must be denied without data changes.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Admin Coupon Flow**: `pending` - sign in as an authorized admin, create a percentage coupon and a fixed-value coupon, apply them to eligible and ineligible purchases, and verify limits, expiry, and audit evidence.
- **Manual QA Printable Code Flow**: `pending` - generate a QR/Serial batch, preview it with a saved template, redeem a code as a student, and verify duplicate/expired redemption is rejected.
- **Manual QA Public Exam Flow**: `pending` - publish a free and a paid public exam, enter/buy them as a student, submit attempts, and verify standalone result reporting.
- **Manual QA Negative Check**: `pending` - verify non-authorized users cannot manage coupons, printable codes, templates, or public-exam publishing.
- **Docker Acceptance**: `pending` - `docker compose config -q`, `make up`, `make migrate`, backend health, frontend admin/student surfaces, and representative checkout/redemption/public-exam smoke checks must pass on a disposable local stack.
- **External Dependencies**: No new external payment provider, SMS, WhatsApp, or printing service is required for v1; QR generation/export should work locally.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow authorized administrators to define sales eligibility rules that target packages, lessons, specific videos, video types, standalone public exams, teachers, or platform-wide purchases.
- **FR-002**: The system MUST prevent sellable content or sales rules from activating when required teacher, subject, grade, or active video-type classification is missing for the selected target.
- **FR-003**: The system MUST allow authorized users to create, list, search, update, disable, and audit digital coupons.
- **FR-004**: A digital coupon MUST support percentage discount and fixed-value discount types.
- **FR-005**: A coupon MUST support target scope for package, lesson, public exam, teacher, platform-wide purchase, and where applicable video type.
- **FR-006**: A coupon MUST support owner/source as teacher or platform.
- **FR-007**: A coupon MUST support optional global usage limit, per-student usage limit, start date, expiry date, active/disabled status, and administrative reason for disabling.
- **FR-008**: Coupon validation MUST reject expired, disabled, over-limit, outside-scope, duplicate per-student, or invalid-value coupons before purchase completion.
- **FR-009**: Coupon usage MUST be committed only when the associated purchase or redemption succeeds.
- **FR-010**: Coupon discounts MUST never reduce payable amount below zero.
- **FR-010a**: The system MUST allow administrators to configure whether coupons and printable-code discounts may be combined for a purchase, and MUST enforce that policy during checkout and redemption.
- **FR-011**: The system MUST record audit evidence for coupon creation, update, disablement, successful use, failed use where operationally significant, and financial effect.
- **FR-012**: The financial effect of a coupon discount on teacher-owned content MUST be split between teacher and platform according to the configured revenue distribution for that purchase.
- **FR-013**: The system MUST allow authorized users to generate printable sales-code batches with unique code text and serial number for every code.
- **FR-014**: Printable sales codes MUST support QR representation, target scope, owner/source, value/access behavior, expiry, active/disabled status, and usage limits.
- **FR-015**: Redeeming a printable sales code MUST apply only the configured discount, access, or credit and MUST not grant anything outside the selected scope.
- **FR-016**: Printable sales-code redemption MUST be idempotent for retry-safe requests and MUST not double-spend a single-use code.
- **FR-017**: The system MUST record audit evidence for sales-code batch creation, export/preview, disablement, redemption, failed redemption where operationally significant, and financial effect.
- **FR-018**: The system MUST provide a simple admin template designer for printable code cards with fixed elements for QR, code text, serial, owner/platform/teacher label, target label, price/value label, and expiry.
- **FR-019**: A printable template MUST NOT be usable for code batches unless it includes at least one redemption identifier element: QR or code text.
- **FR-020**: The system MUST allow administrators to save, update, list, preview, disable, and reuse printable templates.
- **FR-021**: The system MUST allow administrators to create standalone public exams that are independent products and are not tied to lesson videos or packages.
- **FR-022**: A public exam MUST support free or paid access.
- **FR-023**: A public exam MUST support administrator-selected classifications such as teacher, subject, grade, and platform-wide scope.
- **FR-024**: Paid public exams MUST be purchasable by eligible students and grant access only to the selected public exam.
- **FR-025**: Free public exams MUST be enterable by eligible students without payment while still enforcing publication, availability, and attempt rules.
- **FR-026**: Public exam attempts and results MUST be reported separately from lesson/video/package exam reports.
- **FR-027**: Public exams MAY use question bank questions as source material, but the question bank MUST remain a reusable source and not become the sold product itself.
- **FR-027a**: Disabling a public exam MUST block new purchases and new attempts, but MUST preserve already started or completed attempts and their results.
- **FR-028**: The system MUST deny coupon, code, template, and public-exam management actions to unauthorized users without persisting changes.
- **FR-029**: The system MUST expose clear student-facing rejection messages for invalid, expired, disabled, over-limit, or out-of-scope coupons/codes.
- **FR-030**: The system MUST expose admin-facing status and usage evidence for every coupon, sales-code batch, individual printed code, template, and public exam.

### Key Entities *(include if feature involves data)*

- **Sales Eligibility Rule**: Defines what targets may be purchased or unlocked, including target type, content identity, video type, teacher/platform scope, and active status.
- **Digital Coupon**: A reusable discount rule with code, discount type/value, target scope, owner/source, limits, expiry, status, and audit history.
- **Discount Stacking Policy**: Administrator-controlled rule that decides whether one or more coupon/code discounts can apply to the same purchase and what cap or priority applies.
- **Coupon Usage**: A successful or significant attempted coupon application tied to student, purchase target, discount amount, and financial effect.
- **Printable Sales Code Batch**: A generated group of offline sales codes sharing target, owner, value/access behavior, expiry, limits, and template.
- **Printable Sales Code**: A unique redeemable code with serial number, QR payload, status, usage counters, and redemption history.
- **Printable Code Template**: A reusable card layout containing fixed fields and their positions for preview/export.
- **Standalone Public Exam**: A free or paid exam product with classifications, publication status, availability, pricing, access rules, attempts, and independent reporting.
- **Financial Effect Record**: Evidence of how a coupon or code changed the paid amount and how that effect was allocated between teacher and platform.
- **Audit Entry**: Administrative and financial evidence for all sensitive changes and redemptions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An authorized admin can create and activate a scoped digital coupon in under 3 minutes and use it successfully in a valid student purchase.
- **SC-002**: 100% of invalid coupon/code attempts in the test matrix are rejected before changing access, balance, usage counters, or purchase state.
- **SC-003**: 100% of completed discounted teacher-owned purchases in the test matrix include a recorded teacher/platform discount split.
- **SC-004**: An authorized admin can generate a printable batch, preview a card with QR/code/serial, and redeem one code as a student without manual database changes.
- **SC-005**: A public exam can be published, bought or entered, attempted, and reported without requiring a lesson, video, or package purchase.
- **SC-006**: Unauthorized management attempts for coupons, printable codes, templates, and public exams are denied with zero persisted changes in all permission tests.
- **SC-007**: Existing package, lesson, gift, and regular exam purchase flows continue to pass regression tests after the feature is enabled.

## Assumptions

- Existing student checkout and balance flows remain the authoritative purchase path.
- Existing internal codes and video types from `specs/151-content-identity-and-types/` are available and should be reused rather than rebuilt.
- Existing gift/free-access behavior from `specs/152-gifts-free-access/` remains separate from coupon and printable-code financial behavior.
- Full teacher daily accounting and payout approval are deferred to Phase 3, but this feature must persist enough financial effect evidence for that later phase.
- SMS, WhatsApp, external print vendors, live notifications, ads, and live video are out of scope for this feature.
- Manual QA is not considered complete until the product owner runs and records the manual scenarios.
