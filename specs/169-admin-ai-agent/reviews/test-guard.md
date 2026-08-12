# Test review: AdminAI Worker and baseline/security gates

## Summary

The reviewed tests are behavior-focused and provide useful boundary coverage without database or domain-object mocks. No must-fix Test Guard violation remains. The suite must not be interpreted as release approval: the tests correctly preserve the blocked capability baseline, and the repository endpoint parity gate still has two real route mismatches.

## Findings

No blocking violations.

### Documented exceptions

- `worker/src/jobs/processAdminAITurn.test.ts` asserts inference and callback counts for the provider-completed/callback-pending crash case. Ordinarily call-count assertions are brittle under Rule 1/LLM Rule 12; here the protocol explicitly requires proof that callback replay performs **no second inference**, so the count is the externally significant safety property.
- `worker/src/services/adminAITelemetry.test.ts` inspects structured log fields. LLM Rule 11 normally discourages telemetry-wiring tests; this test instead enforces the security contract that prompts, identifiers, and conversational content cannot enter logs/metrics, so it is justified.
- Callback and provider doubles represent HTTP/LLM/BullMQ boundaries. They do not replace domain entities or internal persistence under test.

## Rule coverage

- **Rule 1 — behavior:** decisions, transitions, callback replay, safe failures, limits, and redaction boundaries are asserted.
- **Rule 2 — mocks:** doubles are restricted to network, provider, queue job, clock, and cancellation boundaries.
- **Rule 3 — variants:** HTTP retry classifications and invalid decision variants are grouped rather than copied into separate fixtures.
- **Rule 4 — justification:** each test protects a protocol, security, budget, replay, or drift failure.
- **Rule 5 — names:** test names describe scenario and expected outcome.
- **Rule 6 — regressions:** crash/replay and no-synthetic-read-result cases are retained as safety regressions.
- **Rule 7 — framework guarantees:** no tests merely assert Node, BullMQ, fetch, JSON, or pytest behavior.
- **Rule 8 — state objects:** claims, decisions, and job payloads are real typed values rather than mocked DTO classes.
- **Rule 9 — infrastructure:** no Worker test claims to validate PostgreSQL; the Worker has no AdminAI database authority.
- **Rule 10 — prompts:** assertions target structural trust markers and forbidden tool categories, not prose wording.
- **Rule 11 — observability:** only content-exclusion and cardinality policy are asserted.
- **Rule 12 — agent flow:** tests cover state transitions for reads, cancellation, deadline, budget failure, persisted completion, replay, and terminal decisions.

## Verification evidence

- AdminAI Worker focused tests: 22 passing.
- Full Worker suite: 102 passing.
- `tests/test_admin_ai_capability_inventory.py` and `tests/test_admin_ai_agent.py`: 9 passing.
- Full endpoint/inventory group: 13 passing, 2 failing because `POST /api/{conversationId}/archive` and `/restore` have no matching backend route. These are product parity failures, not defective tests, and must not be skipped or weakened.
- `git diff --check` passes for the reviewed scope.

## Verdict

Test quality gate passes for T204. Feature/release completion remains blocked by the truthful baseline and endpoint parity failures.
