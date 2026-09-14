# Research: Platform Phase 0 Audit

## Decision: Use one evidence-based Markdown audit report

**Rationale**: The user requested a phase-based planning artifact that is understandable before converting large items into separate specs. A single report under `docs/` keeps the decision surface easy to review and avoids scattering audit findings across multiple files.

**Alternatives considered**:

- Multiple files per phase: rejected for Phase 0 because it increases navigation cost before the audit is accepted.
- Updating only `docs/platform-change-roadmap.md`: rejected because the roadmap should remain the source plan, while the audit report should capture evidence and status separately.

## Decision: `Complete` requires implemented evidence, not a spec alone

**Rationale**: The user confirmed that `Completed = implemented evidence`: code, UI, tests, or another visible artifact. This prevents spec-only work from being presented as shipped functionality.

**Alternatives considered**:

- `Complete = spec exists`: rejected because it hides implementation gaps.
- `Complete = manual QA passed`: rejected for Phase 0 because many flows may not be manually executable during a documentation audit; manual QA should remain a visible evidence status.

## Decision: Manual QA can be `pending` or `blocked`

**Rationale**: Phase 0 is audit/documentation-only. If local data, credentials, screenshots, or running services are unavailable, the report should state that clearly instead of inventing evidence or blocking the documentation deliverable.

**Alternatives considered**:

- Require screenshots for every completed item: rejected because it turns Phase 0 into a full manual regression pass.
- Ignore manual QA entirely: rejected because the roadmap explicitly asks for things the owner can try after each phase.

## Decision: Cover every phase and major subsection, expanding high-risk child items

**Rationale**: Full checkbox-by-checkbox coverage would be expensive and noisy, while phase-only coverage would miss important finance, access, and permission details. Major subsections plus high-risk child items strikes the right balance.

**Alternatives considered**:

- Every checkbox row: rejected because the report would become too long for fast decision-making.
- Phase summaries only: rejected because it would not meet the requirement to identify missing/conflicting work.

## Decision: Classify impact areas explicitly

**Rationale**: Later specs need to know whether a roadmap item touches data, payment/finance, permissions, UI, worker/events, documentation, or needs a new spec. This classification is the main bridge from audit to implementation planning.

**Alternatives considered**:

- Free-form notes only: rejected because they are hard to filter and compare.
- Implementation task estimates: rejected for Phase 0 because estimates belong after specs and technical planning.

## Decision: Treat existing specs as evidence only when checked against code/module presence

**Rationale**: The repository contains many specs, some complete and some template-like. Mapping a roadmap item to a spec is necessary, but the report must still check for implementation signs in relevant modules.

**Evidence patterns to inspect**:

- Domain entities and DbSets in `backend/src/NaderGorge.Domain/`.
- Application feature folders in `backend/src/NaderGorge.Application/Features/`.
- API controllers in `backend/src/NaderGorge.API/Controllers/`.
- Frontend route and service presence under `frontend/src/app/` and `frontend/src/services/`.
- Mobile app surfaces under `mobile/`.
- Worker jobs and queues under `worker/src/`.
- Related specs and tasks under `specs/`.

**Alternatives considered**:

- Trust spec status alone: rejected by the user's completion definition.
- Full runtime verification for every item: rejected as too broad for this phase.

## Decision: No API/UI contracts are introduced; report contract documents structure

**Rationale**: This feature exposes no runtime API. The only contract needed is the required Markdown report shape so implementation tasks can produce a consistent artifact.

**Alternatives considered**:

- OpenAPI/endpoint contract: not applicable.
- JSON schema for the report: rejected because the requested artifact is human-readable Markdown.

## Decision: Verification emphasizes scope control

**Rationale**: The largest Phase 0 risk is accidental product-code modification or overclaiming status. Verification must prove required sections exist, the roadmap phases are represented, Docker config remains valid, and no product-code changes are part of this feature's intended output.

**Alternatives considered**:

- Full backend/frontend builds: optional but not required for documentation-only changes.
- `make migrate`: explicitly not required because no migrations are introduced.
