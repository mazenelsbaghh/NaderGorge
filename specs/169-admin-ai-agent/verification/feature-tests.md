# Feature test evidence

## 2026-09-30 current restart and inventory checks

- `make verify-admin-ai-capabilities PYTHON='uv run --no-project --with pytest python'` passed 12/12 frontend graph tests, both generated-artifact checks, and 30/30 Python endpoint/capability/source tests. The graph now detects the computed HTTP method used by Learning Center writes; the activation baseline remains blocked.
- `npm run test:admin-ai-redis-restart --prefix worker` passed 1/1 with a disposable Redis 7 container: the replacement worker replayed saved callback completion without a second inference. `dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --no-build --filter FullyQualifiedName~AdminAIRecoveryIntegrationTests` passed 2/2 against an isolated PostgreSQL 16.10 container. Both containers were removed. These are separate restart tests; the combined real-backend/worker/Redis restart gate T181 remains open.

The student-note operation now has a source-bound receipt and recovery resolver. Its two disposable-PostgreSQL tests passed after the final review, and the full AdminAI PostgreSQL integration group passed 29/29. The focused AdminAI application group passed 258/258; the capability inventory gate passed 10/10 frontend graph and 24/24 Python checks. This is candidate coverage only: the production action catalog and remaining acceptance tests still block activation.

The subject-create candidate now shares the durable receipt mechanism and recovers the original subject ID after an ambiguous response or later deletion. Its PostgreSQL test passed 1/1. The updated full AdminAI integration group passed 30/30, focused application group 259/259, migration guard passed, and the capability inventory gate remained current at 10/10 graph and 24/24 Python checks.

The subject-update candidate now rejects a conflicting replay and leaves a later Admin edit intact. Its real-PostgreSQL test passed 1/1; the full AdminAI integration group passed 31/31 and the focused application group passed 260/260. The EF guard and capability inventory gate passed. Browser/provider/manual acceptance remain open.

Video-type create/update now store bounded safe response snapshots for exact replay and recovery. Their two disposable-PostgreSQL tests passed, the complete AdminAI integration group passed 33/33, the focused application group passed 261/261, and the existing video-type lifecycle group passed 17/17. The additive migration and inventory gates passed; activation remains blocked.

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
- The refreshed runtime endpoint inventory test passed 3/3 and exported 831 routes, adding four statement routes without removing any. The AdminAI graph suite passed 10/10 after a closed transport-path assertion became part of `verify-admin-ai-capabilities`. Ten Python inventory assertions passed directly, plus TypeScript, focused ESLint, and both generator checks. Thirteen AdminAI transport calls are documented self-service exclusions, not platform business capabilities.
- `make verify-admin-ai-capabilities PYTHON='uv run --no-project --with pytest python'` passed: 10/10 Node graph tests, both generator checks, and 24/24 Python endpoint/AdminAI inventory and source-contract tests. Source endpoint inventory was regenerated to 831 backend routes and 712 frontend calls with zero missing-route findings after removing an unused nonexistent Teacher bulk-code creation URL. Frontend TypeScript, focused ESLint, and whitespace checks passed.
