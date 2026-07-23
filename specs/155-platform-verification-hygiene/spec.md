# Feature Specification: Platform Verification Hygiene and Phase 1 Closure

**Feature Branch**: `[155-platform-verification-hygiene]`  
**Created**: 2026-06-30  
**Status**: Draft  
**Input**: User description: "اعمل فيز 0 و باقي فيز ١ من full-platform-defects-remediation-phases-2026-06-29.md باستخدام speckit-all؛ اعمل الصح"

## Clarifications

### Session 2026-06-30

- Q: هل يكون Phase 0 تنفيذ كامل بما فيه إزالة generated artifacts و hardcoded secrets؟ → A: تنفيذ كامل بالطريقة الصحيحة.
- Q: هل يشمل باقي Phase 1 إعادة تنفيذ ما اكتمل سابقا أم فقط البنود غير المتحققة؟ → A: فقط البنود غير المتحققة مع الحفاظ على تنفيذ `154-auth-session-permission-safety`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reliable Verification Contract (Priority: P1)

As a developer or release owner, I need one documented verification contract that reflects the real backend, frontend, worker, Docker, and E2E commands so that remediation work can be checked without relying on stale instructions.

**Why this priority**: Phase 0 is the base gate for all later remediation. If the command contract is wrong, every later phase can appear complete while build/test evidence is unreliable.

**Independent Test**: Run the documented root verification command or the documented equivalent commands and confirm each either passes or reports a documented environment blocker with an exact reason.

**Acceptance Scenarios**:

1. **Given** a developer follows the remediation documentation, **When** they run the documented verification command, **Then** it executes backend build/tests, frontend lint/build, worker build, and Docker compose validation in a predictable order.
2. **Given** a required command is unavailable, **When** the verification contract is read, **Then** the documented substitute command and reason are visible.
3. **Given** Playwright is configured for local E2E, **When** E2E smoke is run, **Then** it starts or targets the same frontend port used by the project scripts.

---

### User Story 2 - Repository Hygiene and Secret Safety (Priority: P1)

As a maintainer, I need generated artifacts, build caches, dependency zips, and hardcoded deploy secrets excluded from source tracking so that diffs stay reviewable and sensitive values are not shipped accidentally.

**Why this priority**: Tracked generated output and hardcoded secrets create security, deployment, and review risk before any functional remediation can be trusted.

**Independent Test**: Inspect `.gitignore`, `git status --short`, and tracked file changes after running tests; verify generated reports/cache outputs are ignored or removed from tracking and sensitive deploy values are no longer hardcoded.

**Acceptance Scenarios**:

1. **Given** tests or Playwright generate reports, **When** `git status --short` is inspected, **Then** those generated outputs do not appear as tracked source changes.
2. **Given** deploy tooling is inspected, **When** Makefile/deploy targets are read, **Then** no production SSH password is hardcoded and deploy does not stage/commit arbitrary worktree changes.
3. **Given** Docker compose is used with production-sensitive values, **When** config is validated, **Then** missing required secrets are rejected or explicitly documented for local-only defaults.

---

### User Story 3 - Remaining Phase 1 Browser Readiness (Priority: P1)

As a product owner validating authentication and permissions, I need the remaining Phase 1 browser checks to be either green or blocked by an exact environment/runtime cause so that session and permission safety is not partially accepted.

**Why this priority**: Backend and frontend implementation for Phase 1 exists, but prior Playwright checks exposed localhost cookie/domain blockers. The remaining Phase 1 state must be made observable and repeatable.

**Independent Test**: Run the Phase 1 E2E smoke for auth hydration, admin direct-route denial, cross-surface redirect/session preservation, and parent report token behavior.

**Acceptance Scenarios**:

1. **Given** browser storage is empty and a valid HttpOnly refresh cookie exists for the active surface, **When** the user opens the protected student surface, **Then** auth state is hydrated or a documented cookie-domain blocker explains why it cannot happen locally.
2. **Given** assistant/staff user lacks direct admin route permission, **When** they open protected admin URLs directly, **Then** they see denial/unauthorized behavior and do not see protected content.
3. **Given** parent report links remain token-in-URL by prior user decision, **When** the link is opened, **Then** the token is short-lived, invalid/expired tokens fail safely, and referrer leakage is reduced.

### Edge Cases

- Existing user or generated worktree changes must not be reverted or deleted unless the artifact is confirmed generated and safe to untrack.
- Secrets that may already have been exposed cannot be considered rotated by code changes alone; the documentation must record rotation requirements.
- E2E failures caused by localhost host/cookie mismatch must be recorded separately from product-code failures.
- Verification must remain useful even when full Docker startup or destructive E2E database reset is unavailable.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Developer runs the documented verification command from repo root and sees backend, frontend, worker, and compose checks execute or fail with documented blockers.
- **Manual QA Negative Check**: Inspect deploy tooling and confirm it cannot publish arbitrary dirty worktree content or use a hardcoded SSH password.
- **Manual QA Auth Check**: Validate student/admin/teacher/staff login surfaces and direct admin-route denial in a browser where cookies match the configured local domains.
- **Docker Acceptance**: `docker compose config -q` must pass; any production-sensitive secret default must be removed, required, or documented as local-only.
- **External Dependencies**: Real secret rotation, production SSH key provisioning, and production CI credential updates require credential owner access and are recorded as operational follow-up when not performable locally.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an accurate repository-level verification command or documented command sequence covering backend restore/build/test, frontend lint/build, worker build, Docker compose validation, and E2E smoke where available.
- **FR-002**: The project documentation MUST identify unavailable or intentionally substituted commands, including absent frontend `npm test` or `npm run typecheck` scripts.
- **FR-003**: Playwright configuration and frontend dev scripts MUST be aligned so local E2E targets an available frontend server without manual port guessing.
- **FR-004**: Generated test reports, build caches, Python caches, Next build output, mobile build/cache output, and downloaded dependency archives MUST be ignored or removed from source tracking when they are not source artifacts.
- **FR-005**: Deploy tooling MUST NOT contain hardcoded production SSH passwords and MUST NOT stage/commit arbitrary worktree changes as part of deploy.
- **FR-006**: Docker compose production-sensitive secrets MUST avoid weak silent defaults or must be clearly constrained to local development.
- **FR-007**: The remediation plan document MUST mark completed Phase 0 and remaining Phase 1 tasks accurately without marking blocked browser/manual checks as complete.
- **FR-008**: Remaining Phase 1 E2E smoke MUST be made repeatable and must distinguish product failures from localhost cookie/domain environment blockers.
- **FR-009**: Phase 1 parent-report hardening evidence MUST remain documented: short-lived URL token, safe invalid/expired behavior, and referrer policy.
- **FR-010**: All changes MUST preserve existing Phase 1 backend/session protections and must not revert the completed `154-auth-session-permission-safety` implementation.

### Key Entities

- **Verification Contract**: The documented set of commands and expected outcomes used to accept remediation phases.
- **Generated Artifact**: A report, cache, compiled output, or dependency archive that can be recreated and should not be reviewed as source.
- **Secret Rotation Note**: Documentation that a previously exposed secret must be rotated by the credential owner outside code.
- **E2E Browser Gate**: A Playwright smoke check that validates auth/session/permission behavior on local surfaces.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The documented backend build/test, frontend lint/build, worker build, and `docker compose config -q` commands can be discovered and started in under 10 minutes from the quickstart.
- **SC-002**: Running the selected verification commands produces 0 items of tracked generated Playwright report/cache output.
- **SC-003**: `npm run build` in `frontend` passes without requiring live Google Fonts network fetches.
- **SC-004**: 100% of completed Phase 0 checklist items in the remediation document have implementation or documented evidence.
- **SC-005**: 100% of remaining Phase 1 E2E/manual checklist items are either verified or have an exact blocker documented.
- **SC-006**: `speckit-all` final validation passes for this feature.

## Assumptions

- Phase 0 is allowed to update `.gitignore`, Makefile/package scripts, docs, Playwright config, and tracking state for generated artifacts.
- Removing source tracking for generated artifacts uses `git rm --cached` or equivalent and does not delete the local files unless they are disposable outputs.
- Real credential rotation cannot be performed locally by the coding agent; code removes hardcoded values and records required operational rotation.
- Existing dirty changes outside this feature are treated as user-owned and are not reverted.
