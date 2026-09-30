# Full repository verification

Date: 2026-08-12 (Africa/Cairo)

`make ops-check` passed after the additive EF migrations were generated: build, pending-model check, Application tests, frontend lint/typecheck, worker build/tests, and Compose configuration all passed.

`PATH="$PWD/.venv/bin:$PATH" make verify` passed backend (895 pass, 1 skip), frontend production build, worker build, Compose configuration, route-budget contract tests, and 16 deployment performance contract tests. It then stopped fail-closed because `artifacts/performance-167/final/frontend-routes.json` is absent; the verifier requires a regular non-symlink candidate artifact. No result was fabricated.

`git diff --check` passed before this evidence update and must be rerun before commit.

## 2026-09-30 rerun

`make PYTHON=.venv/bin/python verify` built the full .NET solution with zero warnings/errors, then passed 1,632 application tests with 20 skips because the PostgreSQL integration connection was not supplied to this command. The frontend focused checks passed, including 165 playback/security tests; lint reported one warning and no errors, and the Next.js production build completed. The worker TypeScript build and Compose configuration passed. The route-budget contract group passed 5/5 and the deployment performance contract group passed 26/26.

The command stopped at `verify-performance-budgets` with exit code 6 because `artifacts/performance-167/baseline/frontend-routes.json` is absent. The corresponding candidate and raw evidence are also absent. The verifier requires genuine regular files and source-bound raw measurements; this run is **not** a full-repository pass. The AdminAI capability check appears later in the Makefile and was not reached in this run. A separate `make PYTHON=.venv/bin/python verify-admin-ai-capabilities` passed: frontend graph 527 files/549 calls, generated baseline 1,054 items with activation blocked, and Python contracts 18/18.
