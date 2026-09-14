# Tasks: Platform Phase 0 Audit

**Input**: Design documents from `/specs/150-platform-phase0-audit/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/audit-report.md, quickstart.md

**Tests**: This feature is documentation/audit-only. Required verification is file/content validation, Spec Kit validation, Docker config validation, and manual report review. No production-code tests are required unless product code is accidentally changed.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files or is read-only.
- **[Story]**: Maps to the user story from `spec.md`.
- Every task includes an exact file path.

## Phase 1: Setup (Shared Audit Inputs)

**Purpose**: Confirm the active feature and prepare the report output target.

- [x] T001 Verify `.specify/feature.json` contains `specs/150-platform-phase0-audit` and record the result in `achievements.md`.
- [x] T002 [P] Read `docs/platform-change-roadmap.md` and list all phase/subsection headings that must appear in `docs/platform-phase0-audit-2026-06-27.md`.
- [x] T003 [P] Read `specs/150-platform-phase0-audit/contracts/audit-report.md` and copy the required report section order into the draft report outline.
- [x] T004 Create `docs/platform-phase0-audit-2026-06-27.md` with the required top-level sections from `specs/150-platform-phase0-audit/contracts/audit-report.md`.

---

## Phase 2: Foundational (Evidence Sources and Taxonomy)

**Purpose**: Establish shared status/impact/risk/evidence rules before writing audit rows.

- [x] T005 Add a `Status Legend` subsection under `## Executive Summary` in `docs/platform-phase0-audit-2026-06-27.md` defining `Complete`, `Partial`, `Missing`, `Conflicting`, `Spec incomplete`, `Spec ready / implementation not verified`, and `Needs deeper inspection`.
- [x] T006 Add an `Impact Legend` subsection under `## Executive Summary` in `docs/platform-phase0-audit-2026-06-27.md` defining `Data`, `Permissions`, `Payment/Finance`, `UI`, `Worker/Event`, `Documentation`, and `Needs new spec`.
- [x] T007 Add a `Completion Rule` note under `## Executive Summary` in `docs/platform-phase0-audit-2026-06-27.md` stating that `Complete` requires implemented evidence and that spec-only evidence is not enough.
- [x] T008 [P] Collect spec evidence by listing relevant directories under `specs/` and record the list in `## Verification Notes` in `docs/platform-phase0-audit-2026-06-27.md`.
- [x] T009 [P] Collect source evidence by listing representative backend/frontend/mobile/worker paths and record the list in `## Verification Notes` in `docs/platform-phase0-audit-2026-06-27.md`.
- [x] T010 Add the `Audit Matrix` Markdown table header to `docs/platform-phase0-audit-2026-06-27.md` with columns exactly matching `specs/150-platform-phase0-audit/contracts/audit-report.md`.

**Checkpoint**: The report shell, taxonomy, and evidence-source notes exist before audit classification begins.

---

## Phase 3: User Story 1 - إدارة المنصة ترى حالة الموجود قبل التنفيذ (Priority: P1) MVP

**Goal**: Produce a matrix that covers every roadmap phase and major subsection with status, risk, evidence, and related specs.

**Independent Test**: Open `docs/platform-phase0-audit-2026-06-27.md` and verify that Phase 0 through Phase 6 and their major subsections appear in `## Audit Matrix`.

### Verification for User Story 1

- [x] T011 [P] [US1] Run `rg -n "^## Phase|^### [0-9]|^### الهدف|^### الاعتماديات" docs/platform-change-roadmap.md` and use the output to confirm all major roadmap sections are represented.
- [x] T012 [P] [US1] Run `find specs -maxdepth 2 -type f -name 'plan.md' | sort` and use the output to identify existing spec mappings for roadmap rows.

### Implementation for User Story 1

- [x] T013 [US1] Add Phase 0 audit rows to `docs/platform-phase0-audit-2026-06-27.md` covering review, entity/migration inspection, status table, impact classification, spec reuse, and migration risks.
- [x] T014 [US1] Add Phase 1 audit rows to `docs/platform-phase0-audit-2026-06-27.md` covering content IDs/types, gifts/free access, coupons/discounts, code templates, and public exams.
- [x] T015 [US1] Add Phase 2 audit rows to `docs/platform-phase0-audit-2026-06-27.md` covering parent app, tracking accuracy, and SMS/payment auto-matcher.
- [x] T016 [US1] Add Phase 3 audit rows to `docs/platform-phase0-audit-2026-06-27.md` covering teacher daily finance, teacher balance/content binding, multi-teacher packages, teacher profile, and community placement.
- [x] T017 [US1] Add Phase 4 audit rows to `docs/platform-phase0-audit-2026-06-27.md` covering HR workflows and complaints/tickets.
- [x] T018 [US1] Add Phase 5 audit rows to `docs/platform-phase0-audit-2026-06-27.md` covering leaderboard, teacher intro video, ratings, and governorate map.
- [x] T019 [US1] Add Phase 6 audit rows to `docs/platform-phase0-audit-2026-06-27.md` covering event notifications, ads, and live video.
- [x] T020 [US1] Add a short `## Executive Summary` status paragraph to `docs/platform-phase0-audit-2026-06-27.md` summarizing the strongest implemented areas and largest gaps.

**Checkpoint**: US1 is complete when the audit matrix covers all roadmap phases and major subsections.

---

## Phase 4: User Story 2 - المطور يعرف أثر كل بند قبل تحويله إلى Spec (Priority: P2)

**Goal**: Ensure every audit row is useful for implementation planning by classifying impact areas, risk, evidence, and next action.

**Independent Test**: Pick any matrix row and verify it has impact areas, risk level, evidence note, manual QA status, and a clear recommendation.

### Verification for User Story 2

- [x] T021 [P] [US2] Run `rg -n "CodeGroup|StudentAccessGrant|TeacherAccount|Payroll|NotificationEvent|CommunityPost|ParentTrackingCode|DigitalWallet|RechargeRequest" backend/src frontend/src mobile worker specs docs` and use the output to strengthen evidence notes for high-risk rows.
- [x] T022 [P] [US2] Run `rg -n "admin/(codes|finance|hr|teachers|community|recharge-verification|wallets)|student/(balance|code-redemption|community|teachers)|teacher/(finance|codes|profile)" frontend/src/app frontend/src/services` and use the output to strengthen UI/surface evidence notes.

### Implementation for User Story 2

- [x] T023 [US2] Fill `## High-Risk Items` in `docs/platform-phase0-audit-2026-06-27.md` with all rows touching payment/finance, access grants, teacher revenue, coupons, permissions, parent data, audit logs, or migrations.
- [x] T024 [US2] Fill `## Conflicts/Overlaps` in `docs/platform-phase0-audit-2026-06-27.md` with spec-overlap or roadmap/code mismatch findings, including at least codes/discounts, teacher finance, parent tracking, HR/payroll, and notifications where applicable.
- [x] T025 [US2] Review every `Complete` status in `docs/platform-phase0-audit-2026-06-27.md` and downgrade any row that cites only a spec path without implemented evidence.
- [x] T026 [US2] Review every high-risk row in `docs/platform-phase0-audit-2026-06-27.md` and ensure it contains a concrete recommendation: extend existing spec, create new spec, defer, or inspect deeper.

**Checkpoint**: US2 is complete when no row is missing impact, risk, evidence, manual QA status, or recommendation.

---

## Phase 5: User Story 3 - صاحب القرار يرى أولويات التنفيذ التالية (Priority: P3)

**Goal**: Provide a clear post-audit ordering for future implementation specs.

**Independent Test**: A reviewer can find the top three next specs in under 5 minutes and understand why each is next.

### Verification for User Story 3

- [x] T027 [P] [US3] Compare `## Recommended Next Specs` against `docs/platform-change-roadmap.md` and `specs/150-platform-phase0-audit/research.md` to ensure recommendations follow dependencies.

### Implementation for User Story 3

- [x] T028 [US3] Fill `## Recommended Next Specs` in `docs/platform-phase0-audit-2026-06-27.md` with an ordered table containing rank, proposed spec name, reason, dependencies, suggested scope, and risk.
- [x] T029 [US3] Ensure the first three recommendations in `docs/platform-phase0-audit-2026-06-27.md` are explicitly marked as the next practical implementation specs after Phase 0.
- [x] T030 [US3] Fill `## Manual QA Status` in `docs/platform-phase0-audit-2026-06-27.md` with admin, teacher, assistant, student, parent, purchase/code, permission, and finance flows marked `pending`, `blocked`, `passed`, or `failed`.
- [x] T031 [US3] Fill `## Verification Notes` in `docs/platform-phase0-audit-2026-06-27.md` with inspected files, commands run, skipped commands, and no-production-code-change statement.

**Checkpoint**: US3 is complete when the report can drive the next implementation decision without reading the whole repository.

---

## Phase 6: Deep Critique, Guards, and Final Verification

**Purpose**: Required speckit-all quality gates after implementation.

- [x] T032 Run deep critique of `docs/platform-phase0-audit-2026-06-27.md` against `specs/150-platform-phase0-audit/spec.md`, `plan.md`, and `contracts/audit-report.md`; record and fix every finding in `achievements.md`.
- [x] T033 Run clean-code-guard in guard-pass mode for changed production-code files; if no production-code files were changed for this feature, record that there is no production-code surface to audit.
- [x] T034 Run test-guard for changed test files; if no test files were changed for this feature, record that there is no test-code surface to audit.
- [x] T035 Run feature tests by executing `python3 .agents/skills/speckit-all/scripts/extract_test_commands.py --spec-dir specs/150-platform-phase0-audit` and record the output plus selected documentation checks in `achievements.md`.
- [x] T036 Run `rg -n "^## (Executive Summary|Audit Matrix|High-Risk Items|Conflicts/Overlaps|Recommended Next Specs|Manual QA Status|Verification Notes)" docs/platform-phase0-audit-2026-06-27.md` and record the result in `achievements.md`.
- [x] T037 Run `rg -n "Phase 0|Phase 1|Phase 2|Phase 3|Phase 4|Phase 5|Phase 6" docs/platform-phase0-audit-2026-06-27.md` and record the result in `achievements.md`.
- [x] T038 Run `rg -n "Payment/Finance|Permissions|Data|High" docs/platform-phase0-audit-2026-06-27.md` and record the result in `achievements.md`.
- [x] T039 Run `docker compose config -q` and record the result in `achievements.md`.
- [x] T040 Run `git diff --name-only` and record whether any production-code files were intentionally changed for this feature.
- [x] T041 Run `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/150-platform-phase0-audit` after Phase 9 evidence is complete.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup; blocks report row writing.
- **US1 (Phase 3)**: Depends on Foundational; produces the complete audit matrix coverage.
- **US2 (Phase 4)**: Depends on US1; strengthens row classification and risk quality.
- **US3 (Phase 5)**: Depends on US1 and US2; produces decision-ready recommendations and QA status.
- **Final Verification (Phase 6)**: Depends on all report sections being complete.

### Parallel Opportunities

- T002 and T003 can run in parallel.
- T008 and T009 can run in parallel.
- T011 and T012 can run in parallel.
- T021 and T022 can run in parallel.
- T027 can run while US2 review is underway if the audit matrix is already complete.

## Implementation Strategy

### MVP First

1. Complete Setup and Foundational tasks.
2. Complete US1 audit matrix coverage.
3. Stop and verify every roadmap phase appears.

### Full Phase 0 Completion

1. Complete US1 for coverage.
2. Complete US2 for impact/risk/evidence quality.
3. Complete US3 for next-spec decisions and manual QA status.
4. Complete deep critique, clean-code-guard, test-guard, and feature verification in the required order.

## Notes

- Do not modify production code while implementing these tasks.
- If a row cannot be proven, use `Needs deeper inspection` or `pending`, not `Complete`.
- `make migrate` is not required because Phase 0 does not change schema.
