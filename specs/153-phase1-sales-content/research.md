# Research: Phase 1 Sales and Content Completion

## Decision 1: Build a New Sales Module, Keep Legacy CodeGroup Compatible

**Decision**: Add `SalesCoupon`, `PrintableCodeBatch`, `PrintableSalesCode`, `DiscountStackingPolicy`, `SalesRule`, and `SalesFinancialEffect` instead of expanding legacy `CodeGroup` into all advanced behavior.

**Rationale**: Existing `CodeGroup` already powers access-code generation/redemption and teacher pages. Overloading it with coupon stacking, templates, public-exam sales, and financial-effect evidence would risk regressions and unclear state transitions. A Sales module can read/bridge legacy targets while keeping old endpoints operational.

**Alternatives considered**:

- Extend `CodeGroup` only: rejected because it has single-use `AccessCode` assumptions and old commission side effects.
- Replace `CodeGroup` entirely: rejected because migration and UI regression risk are high.

## Decision 2: Server-Side Target Resolver Is Authoritative

**Decision**: Implement a resolver that maps Package/Term/Section/Lesson/Video/VideoType/PublicExam/Teacher/Platform targets to teacher, subject, grade, price, and sale eligibility.

**Rationale**: The frontend can provide IDs, but financial and access decisions must use database authority. Video type and internal-code work from Spec 151 provides stable identifiers; gift work from Spec 152 already established server-side teacher resolution for funding.

**Alternatives considered**:

- Trust frontend price/teacher values: rejected because discounts and revenue effects are financial.
- Store teacher/subject redundantly on every sales entity: rejected except for public exam metadata because content hierarchy already provides ownership.

## Decision 3: Admin-Controlled Discount Stacking Policy

**Decision**: Add administrator-managed stacking policies. Default is `SingleOnly`, but Admin can allow coupon plus printed code or multiple discounts with a cap/priority.

**Rationale**: The product owner explicitly said the Admin should determine stacking. A persisted policy makes this testable and auditable while retaining a safe default.

**Alternatives considered**:

- Hard-code one discount per purchase: rejected because it contradicts clarified product behavior.
- Always allow all discounts: rejected because it can produce negative/irrational financial effects.

## Decision 4: Usage Consumes Only on Successful Purchase/Redemption

**Decision**: Coupon and printable-code usage rows are persisted inside the same transaction that creates access/deducts balance. Preview validates without consuming.

**Rationale**: Failed checkout must not burn a coupon/code. Serializable transactions and unique request/usage keys prevent concurrent double-spend.

**Alternatives considered**:

- Reserve usage at preview time: rejected because abandoned checkouts would need cleanup.
- Consume before balance deduction: rejected because failure would require compensating rollback logic.

## Decision 5: Financial Effect Is Evidence, Not Payout Accounting

**Decision**: Persist `SalesFinancialEffect` with gross price, discount, promotional, paid, teacher share impact, and platform share impact. Do not update teacher daily accounts in this spec.

**Rationale**: Phase 3 owns teacher calendar/payout. This feature needs reliable evidence now without prematurely implementing payout workflows.

**Alternatives considered**:

- Update `TeacherAccount` immediately: rejected because the roadmap defers daily accounting and split review.
- Store only paid amount on balance transaction: rejected because later audits need source-level discount evidence.

## Decision 6: Public Exams Are Product Metadata Linked to Existing Exam

**Decision**: Add a one-to-one `PublicExamProduct` linked to `Exam`. The existing `Exam`, `ExamQuestion`, and `StudentExamAttempt` engine remains the assessment engine.

**Rationale**: Public exams need product lifecycle, price, classification, and reports separate from lesson/video exams. Reusing attempts avoids duplicating assessment logic and question grading.

**Alternatives considered**:

- Add flags directly only on `Exam`: rejected because product lifecycle fields and reports would clutter existing lesson/video exam semantics.
- Create a wholly separate exam engine: rejected because question bank, attempts, grading, and student UI are already mature.

## Decision 7: Disable Public Exam Blocks New Starts Only

**Decision**: Disabled public exams block new purchases and new attempts, but preserve previous attempts/results.

**Rationale**: Confirmed by the product owner. This protects student history while giving Admin control over future availability.

**Alternatives considered**:

- Block everyone immediately: rejected by user choice and harms paid students/history.
- Admin-select behavior per disable action: deferred because user selected the simpler B behavior.

## Decision 8: Template Layout Stored as Validated JSON

**Decision**: Store template layout as JSON containing fixed field keys and normalized position/size values, with backend validation for required fields and bounds.

**Rationale**: It supports a simple in-admin designer without a full graphics engine. It is portable for preview/export and keeps v1 local with no external print service.

**Alternatives considered**:

- Upload-only background image: rejected by user choice for an in-admin simple designer.
- Full freeform design editor: out of scope and too large for Phase 1.

## Decision 9: API Surface Uses New Controllers

**Decision**: Add `AdminSalesController`, `AdminPublicExamsController`, and `PublicExamsController` rather than placing all endpoints in the large `AdminController`.

**Rationale**: Existing `AdminController` is already large. Dedicated controllers keep permissions and contracts focused and make tests more direct.

**Alternatives considered**:

- Continue adding to `AdminController`: rejected for maintainability.
- Use frontend-only API routes: rejected because financial rules must be backend authoritative.

## Decision 10: Verification Strategy

**Decision**: Use application tests for transaction/domain behavior and mocked Playwright E2E for Admin/Student workflows, plus Docker/PostgreSQL checks when Docker is available.

**Rationale**: Financial/state transitions need deterministic backend tests. UI work needs route and payload smoke. PostgreSQL constraints must be checked in live Docker when the daemon is available.

**Alternatives considered**:

- Only build/lint: rejected because behavior changes are financial and access-related.
- Only full E2E: rejected because checkout edge cases/concurrency need focused backend tests.
