# Docs Guard Review: Admin AI Agent

**Scope:** frontend-facing claims in `spec.md`, `plan.md`, `contracts/ui-contract.md`, `contracts/admin-ai-api.yaml`, `contracts/realtime-events.md`, `quickstart.md`, `docs/verification-contract.md`, and checked-in frontend/worker configuration examples. Backend-only and Worker-internal behavioral claims were not re-reviewed here.

**Verdict:** **pass after fixes**. 93 path, endpoint, command, config, and status claims were checked; the 5 false or stale claims below were corrected and 0 were left unverifiable inside this scope.

## Resolved findings

**Rule 3 violation** in `specs/169-admin-ai-agent/quickstart.md:5`

- **Claim:** The repository contains planning artifacts only and implementation must not start before a future approval.
- **Reality:** The approved implementation now includes `frontend/src/app/admin/ai-agent/page.tsx`, the complete `frontend/src/features/admin-ai-agent/` module, `frontend/src/services/admin-ai-agent-service.ts`, and `frontend/tests/e2e/admin-ai-agent.spec.ts`.
- **Fix:** Replace the planning-only status with the current fail-closed implementation state and list the remaining release gates without claiming availability.

**Rule 1 violation** in `specs/169-admin-ai-agent/quickstart.md:47`

- **Claim:** `AI_ADMIN_AGENT_CONCURRENCY` defaults to `4`.
- **Reality:** `.env.example:96`, `worker/.env.example:20`, and `docker-compose.yml:187` all default it to `2`.
- **Fix:** Document `2` as the checked-in default, or change all authoritative configuration readers/examples together after capacity evidence exists.

**Rule 1 violation** in `specs/169-admin-ai-agent/quickstart.md:49`

- **Claim:** `AI_ADMIN_AGENT_PROVIDER_DEADLINE_MS` is a supported worker setting with a 30000 ms default.
- **Reality:** The key is absent from `.env.example`, `worker/.env.example`, `docker-compose.yml`, and the worker source. AdminAI provider timing currently arrives through the authoritative claim's `deadlineAt` field and is enforced in `worker/src/services/adminAIAgent.ts`.
- **Fix:** Remove the nonexistent key and document `deadlineAt`, or implement and validate a real configuration key before documenting it.

**Rule 3 violation** in `specs/169-admin-ai-agent/contracts/ui-contract.md:68`

- **Claim:** AdminAI owns a query-key namespace in `frontend/src/lib/query-keys.ts`.
- **Reality:** No AdminAI namespace exists in that file. The implemented cross-surface refresh mapping is `ADMIN_AI_REFRESH_SCOPE_KEYS` in `frontend/src/lib/query-contracts.ts:30`, while conversation/snapshot state is controller/store owned.
- **Fix:** Update the ownership section to describe the actual controller/store and refresh-scope mapping, or implement the promised authenticated query namespace and cache boundary.

**Rule 3 violation** in `specs/169-admin-ai-agent/contracts/ui-contract.md` under “Query cache”

- **Claim:** The frontend caches the safe active capability-baseline summary.
- **Reality:** `/admin/ai-agent/capability-baseline` exists in `contracts/admin-ai-api.yaml`, but `adminAiAgentPaths` and `adminAiAgentService` expose no capability-baseline request. The workspace displays snapshot baseline metadata only.
- **Fix:** Describe the currently implemented snapshot metadata, or add the typed client endpoint and authenticated cache behavior before retaining this claim.

## Verified claims

- All 13 AdminAI REST paths currently called by `frontend/src/services/admin-ai-agent-service.ts` match `contracts/admin-ai-api.yaml`, including archive and restore under `/admin/ai-agent/conversations/{conversationId}`.
- Every public AdminAI service operation accepts an `AbortSignal`; mutation operations use the typed idempotency header helper.
- The documented route wrappers, feature components, service contract, realtime hook, and client validation module exist at the stated paths.
- Turn, proposal, execution, secure-input, risk, error, and refresh-scope values rendered by the frontend match the exported TypeScript unions.
- The verification commands for frontend typecheck and the two-project Playwright invocation resolve against `frontend/package.json` and `frontend/playwright.config.ts`.
- `ADMIN_AI_ENABLED` and `ADMIN_AI_HMAC_KEY` are declared in `.env.example` and mapped to backend configuration by `docker-compose.yml`; no real secret value is committed in those examples.
- `docs/verification-contract.md` correctly distinguishes mocked protocol/browser evidence from real-provider acceptance and forbids volume reset during rollback.
- Internal Markdown paths referenced by the reviewed frontend sections resolve, and the reviewed command blocks contain placeholders rather than credentials.

## Documentation quality notes

- The UI contract is specific about RTL, mixed-direction content, safe route keys, memory-only sensitive state, and authoritative snapshot recovery; these claims are directly traceable to frontend source and tests.
- “Planned” wording remains throughout `quickstart.md` and `ui-contract.md`. It should be normalized after the five correctness findings above are resolved so readers can distinguish implemented behavior from release-gated behavior.
- No marketing superlatives, copied upstream API prose, or secret-bearing samples were found in the reviewed scope.

## Verification trail

- Frontend symbols and paths: `frontend/src/app/admin/ai-agent/`, `frontend/src/features/admin-ai-agent/`, `frontend/src/services/admin-ai-agent-contract.ts`, `frontend/src/services/admin-ai-agent-service.ts`, `frontend/src/hooks/useAdminAiAgentEvents.ts`.
- Endpoint paths and status schemas: `specs/169-admin-ai-agent/contracts/admin-ai-api.yaml` compared with `adminAiAgentPaths` and the exported TypeScript unions.
- Query/cache claims: `frontend/src/lib/query-keys.ts`, `frontend/src/lib/query-contracts.ts`, and `frontend/src/features/admin-ai-agent/useAdminAiAgentController.ts`.
- Commands: `frontend/package.json` and `frontend/playwright.config.ts`.
- Configuration: `.env.example`, `worker/.env.example`, `docker-compose.yml`, and the worker AdminAI startup/config readers.
