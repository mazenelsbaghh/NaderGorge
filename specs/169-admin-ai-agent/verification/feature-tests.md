# Feature test evidence

Date: 2026-08-12 (Africa/Cairo)

- Backend solution build: passed with 0 warnings and 0 errors.
- Application suite: 895 passed, 1 Redis-dependent test skipped, 0 failed.
- AdminAI Application focused suite: 184 passed.
- Worker suite: 103 passed; TypeScript build passed.
- Inventory/security suite: 15 passed.
- Frontend production build, lint and typecheck: passed.
- AdminAI/route Playwright matrix: 27 passed and 2 real-backend cases skipped when the E2E seed API was unavailable. The WebKit secure-dialog focus failure and unauthorized-route loop found during the first run were fixed and their focused reruns passed.

The real PostgreSQL and real-backend browser portions remain mandatory open gates; mock-only results are not treated as release acceptance.

## 2026-09-30 continuation

- `dotnet test ... --filter FullyQualifiedName~AdminAI` passed 258/258 application tests and 27/27 PostgreSQL integration tests. The integration run used an isolated disposable test database and included teacher financial review replay, concurrency, and typed confirmation.
- The frontend AdminAI graph suite passed 7/7; both generated baseline checks passed, and all five Python inventory assertions passed by direct invocation because local `pytest` is unavailable.
- The AdminAI Playwright Chromium project ran against the existing local preview with the installed Chrome channel and mocked API responses: 16 passed, 1 real-backend reconnect test skipped because the E2E seed API was unavailable. The first attempt could not launch Playwright's bundled Chromium because it is not installed. This result covers UI contracts only, not T066's real-backend acceptance or WebKit.
