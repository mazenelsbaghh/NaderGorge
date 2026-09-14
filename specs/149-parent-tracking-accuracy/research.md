# Research: Parent Tracking Accuracy

## Decision: Resolve parent-visible teachers from purchased lessons, not creators

**Rationale**: The confirmed product rule is that the parent chooses the teacher of the lesson/package the student purchased. Existing code only reads active `PackageId` grants and currently maps exam attempts to `Exam.CreatedByTeacherId`, which can show items under the wrong teacher.

**Alternatives considered**:
- Use exam/homework creator teacher: rejected because the user explicitly rejected creator-based association.
- Use `CodeGroup.TeacherId`: rejected as a primary source because it does not cover all direct grants reliably once the content hierarchy is available.

## Decision: Build one purchased lesson map from all active entitlement targets

**Rationale**: `StudentAccessGrant` can point to `PackageId`, `TermId`, `ContentSectionId`, `LessonId`, `LessonVideoId`, or `ExamId`. The current package-only lookup omits direct lesson/video/section purchases, causing missing or wrong teacher lists. The feature should collect lessons from package, term, section, lesson, and video grants, filter out inactive/expired/cancelled grants, then use lesson hierarchy to derive teacher. The implementation should reuse or mirror the existing cascade rules in `AccessCheckService` so parent visibility matches student access semantics.

**Alternatives considered**:
- Keep package-only access: rejected because the reported issue likely comes from granular purchases.
- Resolve teacher separately per feature surface: rejected because it risks mismatched teachers between watch logs, exams, and homework.

## Decision: Use lesson-level watch aggregation

**Rationale**: The parent requested "سجل مشاهدات لكل الحصص", and the spec clarified lesson-level aggregation. Watch data comes from `VideoWatchEvent` per `LessonVideo`; the parent screen should aggregate distinct watched videos, total watch count, total watched seconds, completion status, and latest timestamp per purchased lesson.

**Alternatives considered**:
- Per-video rows: rejected because the requested surface is lessons/classes and would make the teacher overview noisy.
- Last watch only: rejected because it hides total activity.

## Decision: Show exams through lesson/video attachment when available

**Rationale**: Exams can be attached directly through `Lesson.ExamId` or `LessonVideo.ExamId`/`Exam.LessonVideoId`. Parent filtering must use the purchased lesson teacher. Attempt data should enrich visible exams with score, pass/fail, and mistakes. Unattempted exams should appear with an unsolved status and empty mistake list when attached to a purchased lesson/video.

**Alternatives considered**:
- Only show `StudentExamAttempt` rows: rejected because Phase 2 clarification recorded unsolved visibility.
- Show all exams by creator: rejected by product rule and would leak unrelated teacher content.

## Decision: Homework visibility follows purchased lessons

**Rationale**: `Homework` has `LessonId`, making teacher resolution straightforward from the purchased lesson map. Submissions are optional; missing submission should render pending/unsubmitted state. Submitted answers provide mistake/correction details.

**Alternatives considered**:
- Only show submitted homework: rejected because the parent must see pending work.

## Decision: Balance uses existing ledger with current balance snapshot

**Rationale**: `StudentBalance.CurrentBalance` is the authoritative balance, and `BalanceTransaction` stores `Amount`, `BalanceAfter`, `TransactionType`, `Description`, and `CreatedAt`. The parent screen should not recompute balance from a partial transaction list.

**Alternatives considered**:
- Recalculate from transactions: rejected because the ledger may be paginated and current balance is already stored.
- Add financial schema changes: rejected because existing entities satisfy the requested data.

## Decision: No schema migration

**Rationale**: Required relationships already exist in access grants, content hierarchy, exams, homework, watch events, lesson progress, balances, and transactions. Changes are query/DTO/UI behavior only.

**Alternatives considered**:
- Add parent-specific tracking tables: rejected as unnecessary and higher risk.

## Decision: Android remains tolerant of old payloads

**Rationale**: The app may be installed before backend deployment or hit older environments. New payload fields should have defaults/nullability so watch/exam/homework/balance screens do not crash on partial data.

**Alternatives considered**:
- Require lockstep backend/app deployment: rejected because it caused reported crash risk.

## Decision: Keep parent endpoint shape and evolve child DTO fields

**Rationale**: Android already calls `GET /api/parent/student-details` and existing parent authorization is enforced in `ParentController`. Keeping the endpoint avoids app routing churn while allowing the DTO to add safer identifiers, nullable attempt fields, and explicit state strings.

**Alternatives considered**:
- Add multiple endpoints per section: rejected because it is broader than the requested fix and would require more Android navigation/data-layer changes.
