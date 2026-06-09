# Implementation Plan: Full Frontend API Contract Audit

**Branch**: `100-endpoint-alignment-and-sidebar-sync` | **Date**: 2026-06-09 | **Spec**: [specs/101-full-frontend-api-contract-audit/spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/101-full-frontend-api-contract-audit/spec.md)
**Input**: Feature specification from `/specs/101-full-frontend-api-contract-audit/spec.md`

## Summary

Create a repeatable frontend-driven API contract audit. The frontend service layer and current Next.js frontend API handlers become the source of truth for required backend routes. The existing backend endpoint inventory script will be extended to parse frontend calls, normalize dynamic route templates, compare them to controller route templates, emit a Markdown/JSON report, and fail tests on frontend-called route drift. Any concrete missing route or method mismatch discovered by the report will be fixed in the smallest matching backend or frontend contract surface.

## Technical Context

**Language/Version**: C# 13 / .NET 9 backend, TypeScript 5.x / Next.js 16.2.1 / React 19 frontend, Node.js v20+ scripts
**Primary Dependencies**: ASP.NET Core controllers, MediatR, EF Core, Axios service layer, native Node.js filesystem/path APIs, pytest
**Storage**: PostgreSQL for existing application data; no new database persistence for the audit
**Testing**: `pytest`, `node scripts/generate-endpoint-inventory.mjs --check`, `dotnet build`, `npm run lint`, `npm run build`
**Target Platform**: Local and Docker Compose monorepo stack
**Project Type**: Monorepo web application with backend API, Next.js frontend surfaces, Node worker, and Python smoke tests
**Performance Goals**: Static inventory should run in seconds on the current codebase and avoid runtime destructive requests
**Constraints**: Preserve existing dirty worktree changes, use frontend calls as source of truth, avoid inventing unused backend features, maintain RTL/product UI rules, keep contract tooling dependency-free
**Scale/Scope**: 22 frontend service files, frontend API handlers, 32 backend controllers, direct API calls inside current frontend components/packages

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Layer impact**: Backend controllers are read and minimally patched only if a frontend-called route is missing. Frontend service files are read and patched only if they call a wrong route or parse an incompatible field. Worker routes are classified as frontend-local/external where they do not target backend controllers. No database schema change is planned.
- **Automated tests required**: Extend and run `tests/test_endpoint_inventory.py`, run the inventory generator in write and check mode, then run backend/frontend build verification where feasible.
- **Manual QA flows**: Verify high-risk frontend surfaces: admin content/users/codes/questions/exams/settings/subjects/teachers/HR/operations/chat/CRM/media/finance/reports, assistant dashboard/chat/CRM, teacher finance, student dashboard/content/exams/homework/community/video session, public auth/forms/stats.
- **Docker gates**: Run `docker compose config -q`; run container health checks if the local stack is available. Static contract verification must not require destructive live requests.
- **No-next-phase rule**: Phase 4 implementation cannot complete until route findings are zero or documented as intentional frontend-local/external exceptions.
- **UI/UX planning**: Impeccable product-register guidance applies: keep product UI familiar, dense, accessible, and consistent; no decorative redesign. UI-UX Pro Max guidance applies only to any touched UI: focus states, hover states, accessible forms, responsive 375/768/1024/1440 checks. This feature primarily changes contract tooling, so UI changes are expected to be minimal.

## Project Structure

### Documentation (this feature)

```text
specs/101-full-frontend-api-contract-audit/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── frontend-backend-contract-inventory.md
└── tasks.md
```

### Source Code (repository root)

```text
scripts/
└── generate-endpoint-inventory.mjs

tests/
├── endpoint_inventory.json
├── endpoint_inventory.md
└── test_endpoint_inventory.py

frontend/src/
├── services/
├── app/api/
├── components/
└── packages/

backend/src/NaderGorge.API/
└── Controllers/
```

**Structure Decision**: Reuse the existing `scripts/generate-endpoint-inventory.mjs` and `tests/test_endpoint_inventory.py` instead of adding a second contract-audit tool. This keeps the audit in the existing Python smoke-test surface and prevents duplicated route inventory logic.

## Phase Closure & Verification Plan

**Automated Tests Required**:

- `node scripts/generate-endpoint-inventory.mjs`
- `node scripts/generate-endpoint-inventory.mjs --check`
- `.venv/bin/python -m pytest tests/test_endpoint_inventory.py`
- `dotnet build backend/NaderGorge.sln`
- `cd frontend && npm run lint && npm run build`

**Docker Gate Required**:

- `docker compose config -q`
- `docker compose ps` when containers are available

**Manual QA Required**:

- Admin: navigate and exercise requests from all current admin service domains.
- Assistant: verify task, chat, and CRM service requests load and submit.
- Teacher: verify teacher finance requests load and payout request submission aligns.
- Student/public: verify auth, dashboard, content, exam, homework, community, forms, and video-session flows do not surface route/model-binding failures.
- Negative permissions: wrong-role users get 401/403, not accidental 404/405 from route drift.

**End-of-Phase Report Format**:

- Implemented scope
- Files changed
- Inventory counts: backend endpoints, frontend backend calls, missing route findings
- Contract findings and fix status
- Commands run and results
- Docker status
- Manual QA checklist and remaining environment blockers

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Static regex-based TypeScript/C# parser instead of full compiler AST | The existing script is dependency-free and already regex-based; extending it keeps CI simple | Adding TypeScript/Roslyn dependencies for this pass would widen setup and slow verification without being required for the route drift class of bugs |
