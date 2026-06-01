# Research: Restore Critical Learning Workflows

## Decision 1: Add explicit moderation state to community comments

- **Decision**: Introduce a dedicated moderation status for community comments with the states `Pending`, `Approved`, and `Rejected`, plus an optional rejection reason stored with the comment record.
- **Rationale**: The current comment flow publishes immediately and student queries return every comment for an approved post. A first-class moderation state is the smallest design change that fixes visibility rules, powers an admin queue, and keeps rejection decisions auditable.
- **Alternatives considered**:
  - Reuse a boolean `IsApproved` flag: rejected vs pending becomes ambiguous and cannot support a moderation queue cleanly.
  - Keep comments public and add reporting only: does not satisfy the requirement that unsafe content never appears by default.
  - Store moderation outcome in a separate log table only: increases join complexity for a simple publish-state problem without adding user value.

## Decision 2: Standardize find-the-mistake answer evaluation around a canonical answer payload

- **Decision**: Treat find-the-mistake as its own answer shape with explicit support for a selected mistake index and, when required by the existing question design, a selected text fallback. Evaluation uses question-type-specific matching, and the result builder emits review data derived from that answer rather than from selected option records.
- **Rationale**: The current submission flow only models option-based and essay-style answers, so find-the-mistake falls through multiple-choice behavior. Making the answer contract explicit removes ambiguity, allows deterministic grading, and keeps result rendering aligned with how the student actually answered.
- **Alternatives considered**:
  - Reuse `SelectedOptionId`: only works if every find-the-mistake question is remodelled as options, which the current feature request does not assume.
  - Compare free-text only: too fragile for scoring and introduces normalization problems when a stable index already exists.
  - Grade through ad hoc branching in the result builder alone: leaves submission persistence inconsistent and does not fix scoring integrity.

## Decision 3: Normalize essay grading into four explicit statuses and align API-facing naming

- **Decision**: Use four explicit essay-grading statuses across backend and frontend: `WaitAI`, `AIScored`, `WaitTeacher`, and `TeacherGraded`. The AI callback persists score and feedback at `AIScored`, the application flow then promotes teacher-ready submissions to `WaitTeacher`, and only `WaitTeacher` submissions may be manually finalized by a teacher. Public grading-status responses may surface both the current item state and an aggregate attempt state such as `Pending`, `PartiallyGraded`, or `Completed`.
- **Rationale**: The current code and UI already disagree on status names and collapse too many stages into `WaitTeacher` and `Graded`. Separating AI completion from teacher readiness makes the callback path observable, protects teacher grading from racing ahead of AI output, and gives result consumers a clear way to distinguish incomplete from final outcomes.
- **Alternatives considered**:
  - Keep three statuses (`WaitAI`, `WaitTeacher`, `Graded`): does not expose the AI-scored handoff clearly and makes reconciliation/debugging harder.
  - Move directly from `WaitAI` to `TeacherGraded`: removes necessary teacher control and breaks the intended semi-automated workflow.
  - Treat `WaitTeacher` as a derived UI-only label with no persisted state: weakens auditability and makes concurrency handling less explicit.

## Decision 4: Expose focused contracts rather than broad exam or community rewrites

- **Decision**: Add narrowly scoped contracts for pending community comment moderation and exam grading status retrieval, while extending existing create/comment/grade flows only where the broken behavior requires it.
- **Rationale**: The feature is a repair pass, not a redesign. Focused contracts reduce regression risk, keep tasks bounded, and align with the constitution's MVP discipline.
- **Alternatives considered**:
  - Redesign full community admin and exam result APIs together: too broad for a critical-fix branch.
  - Hide all changes behind existing endpoints without new contracts: makes the grading lifecycle opaque and blocks the admin moderation queue requirement.
