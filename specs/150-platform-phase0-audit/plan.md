# Implementation Plan: Platform Phase 0 Audit

**Branch**: `150-platform-phase0-audit` | **Date**: 2026-06-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/150-platform-phase0-audit/spec.md`

## Summary

Produce a documentation-only Phase 0 audit report for the platform roadmap. The report will review `docs/platform-change-roadmap.md`, existing `specs/`, representative backend/frontend/mobile/worker surfaces, and project docs to classify each major roadmap item by implementation evidence, related specs, risk level, impact area, manual QA state, conflicts, and next-spec recommendation. No production code, database schema, runtime behavior, or UI behavior will be changed.

## Technical Context

**Language/Version**: Markdown documentation; repository context includes C# 13/.NET 9 backend, TypeScript/Next.js 16 frontend, Node.js worker, Android/iOS mobile apps
**Primary Dependencies**: Existing repository files only: `docs/`, `specs/`, `AGENTS.md`, backend/frontend/mobile/worker source trees, Spec Kit scripts
**Storage**: N/A for application storage; output is Markdown under `docs/` plus Spec Kit artifacts under `specs/150-platform-phase0-audit/`
**Testing**: Markdown/file presence checks, content coverage checks with `rg`, Spec Kit validators, `docker compose config -q`; no app behavior tests required unless later tasks choose optional smoke evidence
**Target Platform**: Developer/product-owner documentation inside the repository
**Project Type**: Documentation/audit workflow for a full-stack web/mobile platform
**Performance Goals**: A reviewer can identify the first three recommended post-audit specs in under 5 minutes; report should stay scannable with matrix-first structure
**Constraints**: No production-code changes, no migrations, no endpoint/UI implementation, no runtime config changes; do not mark an item `Complete` unless implemented evidence is cited
**Scale/Scope**: Covers Phase 0 through Phase 6 and every major subsection in `docs/platform-change-roadmap.md`, expanding child checklist items when they touch data, finance/payment, permissions, or dependencies

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**:
  - Backend: Read-only inspection of representative files only; no intended source changes.
  - Frontend: Read-only inspection of route/service/component presence only; no intended source changes.
  - Worker: Read-only inspection if needed for event/notification/AI-related roadmap items; no intended source changes.
  - Database: No schema changes, migrations, seed updates, or data writes.
  - Docker: No compose/runtime config changes; `docker compose config -q` is the required safety gate.
- **Automated tests required**:
  - Spec Kit quality validation for spec/plan artifacts.
  - Report coverage checks using `rg` against required report sections and key roadmap phases.
  - `git diff --name-only` or equivalent verification to confirm no production-code files were intentionally edited for Phase 0 implementation.
- **Manual QA required**:
  - Product owner/admin reads the report and verifies that major roadmap items are represented.
  - Negative manual check: report does not claim `Complete` without cited implemented evidence and records missing manual QA as `pending` or `blocked`.
- **Docker gate**:
  - `docker compose config -q`.
  - `make migrate` is explicitly not required because no database schema changes exist.
  - `make up` is optional for Phase 0 and should be recorded as skipped unless needed for optional manual QA.
- **Next phase rule**:
  - Phase 1 implementation work must not start until the Phase 0 report exists, required sections are present, and no product-code changes are introduced by this feature.

**Post-design re-check**: PASS. The design remains documentation-only, preserves all architecture boundaries, and uses repository evidence instead of runtime mutation.

## Project Structure

### Documentation (this feature)

```text
specs/150-platform-phase0-audit/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── audit-report.md
└── tasks.md
```

### Source Code (repository root)

```text
docs/
├── platform-change-roadmap.md                    # READ: roadmap source
└── platform-phase0-audit-2026-06-27.md           # NEW: audit report output

specs/
├── 014-registration-codes-hierarchy/
├── 020-lesson-content-management/
├── 058-student-community/
├── 060-package-code-profiles/
├── 090-hr-core-employees-attendance-vacations/
├── 092-multi-teacher-multi-subject-architecture/
├── 093-internal-chat-notifications/
├── 096-payroll-accounting/
├── 130-granular-content-purchase/
├── 147-parent-tracking-app/
├── 148-sms-payment-auto-matcher/
└── 149-parent-tracking-accuracy/

backend/
├── src/NaderGorge.Domain/Entities/               # READ: entity evidence
├── src/NaderGorge.Domain/Interfaces/IAppDbContext.cs
├── src/NaderGorge.Application/Features/          # READ: feature-module evidence
└── src/NaderGorge.API/Controllers/               # READ: surface evidence

frontend/
└── src/                                          # READ: route/service evidence

mobile/
└── parent-android/, parent-ios/, payment-listener-android/ # READ where roadmap references apps

worker/
└── src/                                          # READ where roadmap references events/notifications
```

**Structure Decision**: Standard Spec Kit documentation feature. The only expected deliverable outside `specs/150-platform-phase0-audit/` is one Markdown audit report under `docs/`.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `python3 .agents/skills/speckit-all/scripts/validate_spec_plan_quality.py --spec-dir specs/150-platform-phase0-audit`
- `rg -n "^## (Executive Summary|Audit Matrix|High-Risk Items|Conflicts/Overlaps|Recommended Next Specs|Manual QA Status|Verification Notes)" docs/platform-phase0-audit-2026-06-27.md`
- `rg -n "Phase 0|Phase 1|Phase 2|Phase 3|Phase 4|Phase 5|Phase 6" docs/platform-phase0-audit-2026-06-27.md`
- `git diff --name-only` reviewed to confirm no intentional production-code edits from this feature.

**Docker Gate Required**:

- `docker compose config -q`
- `make migrate` is not required and must be recorded as skipped because no schema changed.

**Manual QA Required**:

- Product owner/admin opens `docs/platform-phase0-audit-2026-06-27.md`.
- Verify all roadmap phases appear.
- Verify high-risk finance/access/permission items cite evidence or `pending/not found`.
- Verify the report's first three recommended next specs are clear.
- Verify manual QA statuses are `pending` or `blocked` where no interactive manual run was performed.

**End-of-Phase Report Format**:

- Implemented scope: files created/updated.
- Audit report path.
- Evidence gathering summary.
- Commands run and results.
- Docker gate result.
- Manual QA checklist status.
- Known uncertainties and go/no-go for Phase 1 implementation specs.

## Complexity Tracking

No constitution violations. No additional services, abstractions, migrations, or runtime components are introduced.
