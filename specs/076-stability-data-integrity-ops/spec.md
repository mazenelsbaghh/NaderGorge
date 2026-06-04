# Feature Specification: Stability, Data Integrity, CI, and Operations

**Feature Branch**: `076-stability-data-integrity-ops`  
**Created**: 2026-06-04  
**Status**: Draft  
**Input**: Full implementation of `docs/audit-remediation-phase-2-stability-data-integrity.md`.

## User Scenarios & Testing

### User Story 1 - Reliable CI and Dependency Visibility (Priority: P1)

As an operator, I need CI to build and test the actual .NET 9 / Next 16 / worker stack and to report dependency risk accurately.

**Independent Test**: Run CI-equivalent commands locally and verify backend tests, frontend build/lint, and worker build execute against the right ports and SDK versions.

### User Story 2 - Atomic Job and Financial State (Priority: P1)

As an admin, when triggering AI analysis or changing a student's balance, concurrent requests must not create duplicate jobs or lose balance updates.

**Independent Test**: Trigger two simultaneous AI-analysis requests for one video and verify only one processing lock is acquired.

### User Story 3 - Role Updates Cannot Lock Out Admins (Priority: P1)

As the platform owner, role edits must not remove every role from a user or remove the last admin.

**Independent Test**: Attempt to submit an empty role list and attempt to remove the only admin role; both must fail.

### User Story 4 - Public Endpoints Resist Abuse (Priority: P2)

As an operator, public WhatsApp checks and form submissions need limits and payload validation so anonymous clients cannot enumerate or spam.

**Independent Test**: Submit oversized form payloads and repeated public checks; invalid requests must fail before reaching expensive work.

### User Story 5 - Runtime Storage and Logs Avoid Sensitive Data (Priority: P2)

As an operator, worker runtime files and logs must not expose student data or depend on source-tree paths.

**Independent Test**: Run worker build and inspect code paths: subtitles use configured storage, legacy code generation hashes codes, and logs avoid raw payload output.

## Requirements

- **FR-001**: CI workflow MUST use .NET 9 and frontend port 8738.
- **FR-002**: Backend package versions MUST avoid MSB3277 EF Core conflicts.
- **FR-003**: Redis configuration MUST use `ConnectionStrings:Redis` and must not silently fall back in production.
- **FR-004**: API MUST use forwarded headers behind configured proxies and add production security headers.
- **FR-005**: Public WhatsApp and form endpoints MUST have rate limits and form payload validation.
- **FR-006**: AI analysis and mindmap processing locks MUST be acquired atomically in the database.
- **FR-007**: Role updates MUST reject empty roles and protect the last admin.
- **FR-008**: Balance adjustments MUST validate amount/reason and execute inside a transaction or atomic database update.
- **FR-009**: Worker legacy code generation MUST not store plaintext codes as the code hash.
- **FR-010**: Worker subtitle output path MUST be configured through environment variables and not write into backend source by default.
- **FR-011**: Worker logs MUST avoid raw Redis payloads and obvious PII.
- **FR-012**: Frontend API fallback MUST use backend port 5245 and old brand/domain strings must be removed from active source.
- **FR-013**: Next middleware deprecation MUST be resolved by migrating to the supported proxy convention.
- **FR-014**: Controllers MUST fail when user id claims are missing instead of using `Guid.Empty`.

## Edge Cases

- If `npm audit` cannot reach zero without a risky major upgrade, the remaining vulnerabilities must be documented with package name, severity, and plan.
- Dev Docker may expose DB/Redis via an override, but production compose must not publish internal services.
- Atomic locks must release the processing flag if job enqueue fails.
- Public form validation must allow legitimate saved form schema fields while rejecting excessive keys or values.

## Success Criteria

- **SC-001**: `dotnet test` passes with no EF package conflict warnings.
- **SC-002**: `frontend npm run build` no longer emits middleware deprecation warning.
- **SC-003**: `worker npm run build` passes.
- **SC-004**: Security/config searches show no old brand strings in active frontend source.
- **SC-005**: Docker production config does not publish DB/Redis/worker ports.
