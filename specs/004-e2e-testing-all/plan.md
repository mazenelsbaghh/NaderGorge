# Implementation Plan: E2E Testing Coverage

**Branch**: `004-e2e-testing-all` | **Date**: 2026-03-23 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-e2e-testing-all/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

This plan dictates the setup of Playwright for End-to-End (E2E) UI testing across the Nader Gorge platform. It implements isolated database testing through a C# E2E environment configuration, avoiding disruption to development or production databases. Key user streams will be covered: Auth, Code Generation, Code Redemption, and Content Consumption.

## Technical Context

**Language/Version**: TypeScript 5+ (Playwright Scripts), C# (.NET 8 for Backdoor Endpoints) 
**Primary Dependencies**: `@playwright/test`
**Storage**: Separate `nadergorge_e2e` database instance on PostgreSQL.
**Testing**: Playwright Test Runner  
**Target Platform**: Web browsers (Chromium, Firefox, WebKit via CI/CD)
**Project Type**: Infrastructure / Automated E2E Testing Suite  
**Performance Goals**: < 10 minutes total run time locally  
**Constraints**: Must preserve the integrity of dev/prod DBs. Testing framework must interact with actual browser DOM state.  
**Scale/Scope**: Coverage of 4 core user journeys (Auth, Content Creation, Redemptions, Exams).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Architecture**: Pass. Testing code will be isolated and live purely within a test orchestration folder rather than cluttering business logic.
- **Provider Abstraction**: Pass. External SMS API will be bypassed locally in `ASPNETCORE_ENVIRONMENT=E2e` via internal override.
- **Security by Default**: Pass. Backdoor test endpoints are EXPLICITLY restricted to the E2E environment profile, ensuring production systems cannot be seeded or wiped.
- **Observability**: Pass. Playwright generates robust HTML test reports and traces.

## Project Structure

### Documentation (this feature)

```text
specs/004-e2e-testing-all/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
backend/src/NaderGorge.API/
├── appsettings.E2e.json               # E2E test database connection details
└── Controllers/E2eTestingController.cs # Mock endpoints ONLY active in E2E env

frontend/
├── playwright.config.ts           # Runner configuration
└── tests/
    ├── e2e/                       # Playwright specs directory
    │   ├── admin-content.spec.ts  # TDD content creation
    │   ├── auth.spec.ts           # TDD auth and device limits
    │   ├── codes.spec.ts          # TDD codes bulk generation
    │   └── student-journey.spec.ts # TDD studying & exams
    ├── fixtures/                  # Page objects and setup wrappers
    └── utils/                     # API interceptors and helpers
```

**Structure Decision**: Playwright suite will live tightly coupled with the `frontend` application codebase. The `backend` will just expose an additional configuration file and controller that are specifically excluded/disabled from Production deployments.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Backdoor Controller | E2E Tests need predictable states | Seeding DB from Node via raw SQL breaks architecture bounds and creates messy cross-DB dependencies for the frontend developer writing UI tests. |
