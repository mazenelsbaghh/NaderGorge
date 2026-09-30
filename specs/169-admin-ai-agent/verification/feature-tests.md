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
- The frontend AdminAI graph suite passed 9/9 after narrowing shared subscriber/moderation calls and excluding TypeScript-only references; both generated baseline checks passed, and all seven Python inventory assertions passed by direct invocation because local `pytest` is unavailable. The inventory now includes Admin routes regardless of controller name, including all 14 assessment review routes. Matched frontend calls share backend effect, domain, risk, confirmation, and refresh scopes. The changed components passed TypeScript typecheck and focused ESLint.
- The AdminAI Playwright Chromium project ran against the existing local preview with the installed Chrome channel and mocked API responses: 16 passed, 1 real-backend reconnect test skipped because the E2E seed API was unavailable. The first attempt could not launch Playwright's bundled Chromium because it is not installed. This result covers UI contracts only, not T066's real-backend acceptance or WebKit.
- The expanded inventory checks passed 8/8 after static Admin/HR route branches were made explicit; all literal Admin/HR frontend calls now resolve to a backend endpoint. Frontend TypeScript and focused ESLint passed for the changed services/components. This still does not cover unknown dynamic URLs or the real-backend browser gate.
- The permission-gated support/campaign/video-learning inventory passed 9/9 Python assertions and 9/9 frontend graph contracts after the shared attachment reader's participant GET branch was retained conservatively. Frontend TypeScript, focused ESLint, both generated checks, and whitespace verification passed. The public WhatsApp webhook is absent from the Admin capability inventory.
- The shared report-service routing passed 10/10 Python inventory assertions, TypeScript typecheck, focused ESLint, and regenerated manifest checks; all nine literal Admin report calls map to Admin report endpoints. Teacher report variants remain separate diagnostic calls.
