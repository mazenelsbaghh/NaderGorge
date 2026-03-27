# Technical Research & Findings
**Feature**: E2E Testing for Phase 2 Academic Operations

## Playwright Best Practices for E2E
- **Decision**: Use Playwright's Page Object Model or dedicated test fixtures for re-usability, and `E2eTestingController.cs` for reliable backend state management.
- **Rationale**: Keeps tests isolated and fast by bypassing long UI setup flows natively supported by API-first backend setup methods.
- **Alternatives Considered**: Direct DB access via frontend scripts (insecure, complex). Seed data strictly via UI (too slow).

## Mocking External Services (BullMQ, SMS, Emails) during E2E
- **Decision**: In E2E environments (`ASPNETCORE_ENVIRONMENT=E2e`), the Node Worker connects to a separate Redis DB or skips actual 3rd-party REST API dispatches. In ASP.NET, we mock the `IPublisher` or simply skip enqueueing SMS tasks.
- **Rationale**: We want end-to-end integration of the DB state (gamification, warning generation) without polluting real communication channels.
- **Alternatives Considered**: Mocking the entire Node Worker out. But we still want to test BullMQ message enqueuing.

## Validating Gamification Points and Parent Report
- **Decision**: Fetch parent summary from UI at `/parent-report/[studentId]` or valid REST API call. Extract DOM elements to ensure points calculation (XP = 20 for homework) matches.
- **Rationale**: Reflects exactly what the parent/student sees.
