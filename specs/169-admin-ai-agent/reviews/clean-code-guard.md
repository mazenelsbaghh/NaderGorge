# Code review: AdminAI Worker and baseline gates

## Summary

The reviewed Worker path is fail-closed and has no platform-action or database authority. It is acceptable for continued integration: no critical Clean Code, security, swallowed-error, fake-success, dead-import, or hallucinated-package blocker remains. Release activation is still blocked by the capability baseline and endpoint parity, independently of this code-quality verdict.

## Critical findings

None remaining.

The guard pass specifically confirmed that callback transport failures are classified, missing read results are rejected rather than synthesized, cancellation storage failures propagate, completion is persisted before callback replay, and `propose_actions` never executes an effect.

## Important findings

- **Orchestration functions exceed the preferred small-function target** — `worker/src/services/adminAIAgent.ts` and `worker/src/jobs/processAdminAITurn.ts`.
  Evidence: `runAdminAIAgent` owns the bounded model/read loop and `createAdminAITurnProcessor` owns claim/replay/failure orchestration.
  Principle: small functions and KISS.
  Disposition: non-blocking documented debt. Splitting these state machines before the backend protocol stabilizes would increase replay/version-ordering risk. Revisit only with transition-equivalence tests.

- **The baseline generator remains a conservative candidate generator, not an activation manifest** — `scripts/generate-admin-ai-capability-baseline.mjs`.
  Evidence: generated activation is `blocked`; mutations receive explicit adapter/idempotency/concurrency/audit blockers.
  Principle: no hardcoded success and no false completion claim.
  Disposition: correct fail-closed behavior. Do not weaken it or label it active until reviewed adapters replace every blocker.

## Nits

- Several Worker files use dense one-line statements consistent with the surrounding generated implementation. Formatting can be expanded in a later behavior-preserving pass, but it is not a correctness blocker.

## What's good

- Names encode protocol intent (`callbackIdempotencyKey`, `expectedTurnVersion`, `remainingRedactedContextBytes`) rather than generic transport state.
- The Worker depends on declared read functions and backend callbacks only; no SQL, PostgreSQL client, public Admin controller, web, MCP, code execution, or automatic function execution is available.
- Errors crossing trust boundaries use stable safe codes and never include raw provider bodies, prompts, tool arguments, or tool results.
- Provider completion is durably stored before callback delivery, so retry does not perform a second inference.
- Telemetry uses an explicit low-cardinality field allowlist and sanitizes provider/model/capability labels.
- Installed `@google/genai` APIs and TypeScript shapes were verified by the project build and exercised through the real compile target.

## Verification evidence

- `cd worker && npm run build`
- Focused AdminAI Worker tests: 22 passing.
- Full Worker suite previously and in this review chain: 102 passing.
- Root AdminAI capability/security tests: 9 passing.
- Baseline remains honestly blocked: 961 items, 562 blocked mutations, 93 direct-controller blockers.
- The broader endpoint inventory gate still reports two missing backend routes for AdminAI conversation archive/restore; this is outside the reviewed Worker code and prevents release completion.

## Self-check coverage

- [x] Walked Section A (naming & functions)
- [x] Walked Section B (comments & formatting)
- [x] Walked Section C (SOLID)
- [x] Walked Section D (DRY/KISS/YAGNI)
- [x] Walked Section E (AI failure modes)
