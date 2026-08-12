# Capability Baseline Contract

**Purpose**: Make “all current Admin reads and actions” a deterministic, reviewable release condition.
**Authority**: The compiled backend registry plus its immutable activated baseline snapshot.
**Default**: Anything absent, stale, disabled, unknown, or incompatible is unavailable to the agent.

## Why the current endpoint inventory is not the baseline

The tracked tests/endpoint_inventory.json is stale in the current worktree, and its parser is regex-based. It can miss modern C# class/attribute shapes, mis-normalize routes, and cannot determine:

- whether a request is reachable from the Admin Shell;
- actual runtime authorization/permission metadata;
- GET actions that write state;
- POST actions that are preview/export/read-only;
- direct service/background/upload/external effects;
- authoritative MediatR/service ownership;
- risk, confirmation, idempotency, concurrency, transaction, audit, or secure-input behavior.

The existing inventory remains a useful diagnostic, but it is not acceptance evidence.

## Baseline sources

### Backend runtime source

An integration host reads:

- EndpointDataSource;
- ControllerActionDescriptor;
- resolved HTTP methods and route templates;
- Authorize/AllowAnonymous and role metadata;
- HasPermission metadata;
- request size/rate policy metadata;
- source controller/action symbol;
- MediatR/application-service mapping recorded by the semantic manifest.

This output is canonicalized and hashed.

### Frontend reachable source

A TypeScript AST/import graph starts at:

- frontend/src/app/admin routes;
- frontend/src/packages/admin/navigation.tsx;
- frontend/src/packages/admin/route-permissions.ts;
- frontend/src/components/admin/AdminShellChrome.tsx;
- components/features imported by reachable Admin routes;
- API calls made through their service graph.

Dynamic URL construction must resolve to a declared route contract. A call that cannot be resolved fails the generator rather than becoming a wildcard.

### Manual semantic source

Review includes:

- MediatR commands/queries and application services;
- upload/file/storage flows;
- exports and report jobs;
- BullMQ/background jobs;
- finance posting/period/reconciliation rules;
- HR approvals/payroll/lifecycle rules;
- external providers such as Bunny, WhatsApp, and AI jobs;
- current direct-controller DbContext writes;
- audit and idempotency behavior;
- existing original-screen confirmation/version fields.

## Baseline identity

Every baseline snapshot includes:

| Field | Meaning |
|---|---|
| version | Reviewed human-readable version |
| sourceRevision | Sealed source revision and reviewed dirty-worktree fingerprint |
| runtimeInventoryHash | Canonical runtime endpoint inventory hash |
| frontendInventoryHash | Canonical reachable frontend graph hash |
| manifestHash | Canonical semantic manifest hash |
| sensitivePolicyVersion/hash | Exact redaction contract |
| createdAt/approvedAt/approvedBy | Governance evidence |
| read/action/exclusion counts | Acceptance evidence, not hard-coded planning numbers |
| status | Draft, Active, Superseded, or Rejected |

Only one baseline may be Active. Turns and proposals bind to the exact active IDs/versions/hashes.

## Manifest item contract

Every candidate source item has exactly one semantic manifest item.

Required fields:

| Field | Rules |
|---|---|
| key | Stable namespaced key, maximum 160 characters |
| version | Capability contract version |
| disposition | Supported, ReadOnly, SecureContinuation, or Excluded |
| kind | Read, Preview, Export, Mutation, ExternalSideEffect, SecureContinuation, or Excluded |
| domain | One approved domain family |
| titleAr/descriptionAr | Reviewed safe Arabic labels |
| sourceRoutes | All runtime routes represented |
| sourceFrontendCalls | All reachable call sites represented |
| authoritativeOperation | Exact MediatR command/query or application service |
| inputSchema | Closed JSON schema; additional properties forbidden |
| outputSchema | Closed safe projection/result schema |
| allowedFilters/sorts | Explicit for reads |
| limits | Rows, fields, bytes, timeout, rate, pagination/export behavior |
| redactionPolicy | Field allowlist plus sensitive-policy classifications |
| evidence | Scope, filters, count, completeness, dataAsOf, drill-down |
| riskFlags | Ordinary or one/more high-risk flags |
| confirmationType | None for reads, Explicit, or TypedStrong |
| previewAdapter | Server adapter that creates zero business effect |
| executionAdapter | Server adapter invoking authoritative operation |
| stateFingerprint | Target/concurrency/bulk membership inputs |
| idempotency | Durable identity/hash/replay behavior |
| transactionConcurrency | Isolation, row/version/lock behavior |
| bulkSemantics | NotBulk, Atomic, or Partial plus item outcome contract |
| secureInputs | None or secure flow kinds/metadata |
| externalEffects | Provider/job/file/notification effects and recovery identity |
| audit | AdminAI event plus original operation audit linkage |
| refreshScopes | Existing allowlisted cache/query domains |
| deepLink | Server routeKey/parameter mapping; never model URL |
| tests | Required generated and representative test IDs |
| exclusionReason | Required only for Excluded |

No manifest item may name raw SQL, a controller method as its executor, a reflection type selected by the model, or a generic endpoint invoker.

## Domain families to inventory

This table defines required coverage families, not final item counts.

| Domain family | Read coverage | Action families and special rules |
|---|---|---|
| Identity, users, students, staff, roles, devices | Search/detail/status/profile/roles/devices/notes/access/balance/gamification/watch | Create/update users; roles/permissions; status/disable; device disconnect; notes; password reset through secure continuation; balance/points/access/watch operations. Financial, credential, permission, disable, delete, and bulk are strong. |
| Teachers, subjects, photos | Profiles, subjects, assignments, stats, students, essays, activations, images | Teacher/subject create/update/delete, assignment/photo activation/upload/delete. Files use secure continuation; delete/reassignment is strong. |
| Academic content and assessment | Packages, terms, sections, lessons, videos, resources, video types, homework, exams, questions, essays, subscribers, Bunny/AI state | CRUD/link/unlink/publish/activate, grading, uploads, exports, AI analysis/mindmaps, Bunny lifecycle. Delete/unpublish/bulk/external job/provider effects are strong; ordinary metadata edits are explicit. |
| Codes and shared packages | Code groups, access codes, profiles, batches, delivery, shared package details | Bulk generation, settings, removal, profile reset, shared package create/publish/image. Bulk/delete/publish/financial terms are strong. |
| Comments and community moderation | Posts/comments/polls/likes/moderation queues/history | Approve/reject/delete/moderate. Destructive/visibility/rejection operations follow reviewed risk rules; no model-generated moderation reason is trusted without proposal. |
| Gifts, promotions, sales | Gifts/recipients/grants/promotional balances, rules, coupons, templates, public exam products, sales effects | Issue/revoke, coupon/rule/template/product lifecycle, price/discount/publication/batches. Grant/balance/price/bulk effects are strong. |
| Forms, submissions, settings, popup, notifications, WhatsApp | Definitions/submissions/status/settings safe values | Form lifecycle/status, safe platform settings, popup, approved messaging/test actions. Secret settings never read; platform-wide/external/delete actions are strong. |
| Legacy Admin finance/payroll | Payroll, adjustments, payouts, teacher events, reports | Generate/approve/adjust/delete/resolve/review. Every money-affecting operation is strong and preserves precision/period/audit. |
| Teacher finance center | Agreements, liabilities, settlements, invoices, allocations, terms, delivery | Create/replace agreement, settlement/pay/cancel/reverse, invoice attachment, terms/delivery confirmation. All mutations strong; files secure. |
| Platform financial center | Ledger, treasury/cashboxes, expenses, refunds, budgets, reconciliation, periods, migration/history, wallet reviews | Post/reverse/transfer/reconcile/close/reopen/backfill/classify/record. All mutations strong; original posting/period/document/idempotency rules are mandatory. |
| Wallets, recharge, SMS transfer matching | Wallets, limits, requests, match evidence, balances, transfers | Create/toggle/limit/security token regeneration, match/reassign/resolve/reverse credit. All security/financial actions strong; token value secure and never model-visible. |
| Human resources | People/org/jobs/locations/contracts/shifts/attendance/leave/approvals/payroll/docs/assets/performance/cases/recruitment/lifecycle/governance/migration | Complete current HR Admin workflows. Compensation/payroll/delete/offboard/hire/offer/approval/case/asset/migration/retention are strong; ordinary scheduling/profile edits use catalog risk. Self-attendance-only actions that reject general Admin are not Admin business capabilities. |
| Operations and CRM | Tasks/approvals, assignments, queues, calls, follow-up, reports | Create/assign/comment/call/resolve/archive/pin/bulk. Approval/destructive/bulk impact determines strong confirmation. |
| Internal chat administration | Rooms/participants/messages/pin/archive where reachable as Admin business workflow | Agent can propose administrative chat operations but never merges its own transcript with chat. Content is untrusted and minimized. |
| Live-support administration | Queue/staff/config/intervention/ratings/stats and student-support AI policy/knowledge/evidence | Administer existing support system while remaining a separate AdminAI conversation. Enable/disable/publish/intervene/policy/knowledge links are strong; previews/stats are reads. |
| Reports, audit, system logs, media operations | Reports/KPIs/exports/audit/log/media pipelines/social plans | Report execution/export is semantically read-only when it has no business effect. Saved definitions/media state follow risk. AdminAI audit/protected policy evidence can never be deleted or weakened. |

## Risk and confirmation derivation

| Condition | Required confirmation |
|---|---|
| Pure bounded read/preview/export with no state/external effect | None |
| Ordinary create/update/comment/assignment with narrow reversible effect | Explicit proposal button |
| Delete, revoke, reverse, cancel with loss, unpublish, destructive moderation | TypedStrong |
| Any amount/balance/price/payroll/ledger/treasury/refund/settlement/budget/financial term | TypedStrong |
| Role, permission, policy, access, security version, token regeneration, device/session termination | TypedStrong |
| Account disable, credential reset/change, protected verification | TypedStrong plus secure continuation where needed |
| Bulk/multi-target or platform-wide setting | TypedStrong |
| External message/provider job/file publication with consequential effect | TypedStrong unless manifest proves narrow reversible ordinary effect |

Risk is derived by server registry. Model and client cannot lower it.

## Direct-controller extraction gate

Any business write currently performed directly in a controller must first move into a reusable authoritative command/service with its existing validation and transaction behavior. Known review targets include:

- pending-essay GET behavior that changes states;
- teacher-event finance review;
- platform finance wallet-transfer backfill/record/reverse flows;
- shared package create/upload/publish;
- teacher code finance terms/delivery;
- teacher finance agreement/settlement/payment/cancel/reverse/invoice flows;
- HR approval definition/delegation;
- HR asset, leave type/policy/balance, payroll component/rule/compensation;
- HR performance cycle/evidence/response;
- HR recruitment requisition/candidate/interview/offer/acceptance;
- HR shift assignment/attendance-policy writes.

The exact list is regenerated from the sealed source. A wrapper that calls a controller or duplicates its DbContext statements does not satisfy the gate.

## Authoritative adapter contract

Each action adapter must:

1. Accept the initiating Admin ID, proposal ID, idempotency identity, normalized typed input, and cancellation token.
2. Load the current capability definition by exact key/version.
3. Revalidate current Admin access and original operation permission/validation.
4. Recompute target/state/bulk fingerprint inside the safe transaction boundary.
5. Call the same MediatR command/application service as the original screen.
6. Pass the original workflow's concurrency/idempotency/approval/document/provider requirements.
7. Return a closed result union with safe references, counts, item outcomes, original audit ID, and refresh scopes.
8. Never return raw exceptions, secrets, unrestricted entity snapshots, or a provider payload.

If the original operation lacks durable idempotency or authoritative ambiguous-timeout recovery, it must be refactored before the capability is marked Supported.

## Read adapter contract

Each read adapter must:

1. Use a closed input validator and field-level projection.
2. Recheck Admin access immediately before the query.
3. Apply bounded filters, pagination, sort, timeout, and query tags.
4. Compute counts/money/ratios/reconciliation deterministically.
5. Return dataAsOf, result count, complete/truncated, safe scope/filters, and routeKey drill-down references.
6. Treat stored text as untrusted content.
7. Reject prohibited fields before provider serialization.
8. Have query-count/plan tests for representative data.

## Exclusion contract

Excluded requires:

- exact source route/call/symbol;
- stable reason code;
- why it is not a current Admin business capability;
- owner/reviewer;
- test proving the agent cannot invoke it.

Allowed reason codes:

- InternalCallback
- E2eOnly
- InfrastructureOrDeployment
- GeneratedCode
- PublicOrAuthentication
- StudentTeacherParentSelfService
- NonAdminRoleOnly
- DuplicateAliasOfCapability
- ForbiddenProtectedAuditMutation

No “not implemented,” “too hard,” or “future work” exclusion is permitted for a current Admin business operation at release.

## Secure continuation disposition

SecureContinuation is supported coverage, not an exclusion. The manifest identifies:

- safe proposal/preview fields;
- protected input kind;
- original secure validator/file policy;
- maximum lifetime and one-time grant;
- operation adapter receiving the protected value;
- purge behavior;
- tests proving absence from model/transcript/audit/log/realtime/cache.

## Bidirectional coverage assertions

The release test fails unless all are true:

1. Every runtime/reachable baseline item has exactly one manifest disposition.
2. Every Supported/ReadOnly/SecureContinuation item points to live source routes/calls and one authoritative operation.
3. No two capability keys ambiguously execute the same semantic mutation unless one is an explicitly documented alias.
4. Every semantic mutation, including GET-side effects, has an action capability or allowed non-business exclusion.
5. Every preview/export declared read-only has a no-business-side-effect test.
6. Every action has input/output schemas, risk, confirmation, fingerprint, idempotency, transaction, audit, and refresh contract.
7. Every high-risk flag maps to TypedStrong.
8. Every secure input uses secure continuation.
9. Every read field is in a projection allowlist and passes secret sentinel tests.
10. Every deep link uses an allowlisted routeKey mapping.
11. Baseline/source/frontend/runtime hashes match the sealed candidate.
12. Exclusion count/reasons match owner-reviewed evidence.
13. Current Admin business mutation unsupported/excluded count equals zero.

## Generated test matrix

Every action capability receives generated cases:

- current Admin allowed;
- non-Admin/disabled/deleted/role removed/security-version changed denied;
- owner and conversation binding;
- valid/invalid/missing/extra input;
- proposal produces zero business/external effect;
- correct risk/confirmation/secure flow;
- cancel, expiry, stale fingerprint, capability/policy change;
- wrong/old/locked strong phrase;
- matching duplicate returns same result;
- conflicting idempotency payload rejects;
- two-tab/two-Admin concurrency;
- restart/queue/callback retry;
- original-operation validation/audit/notification/transaction parity;
- refresh scopes;
- prohibited sentinel absence.

Representative domain integration adds finance precision/period/posting, bulk atomic/partial, file/storage, provider timeout/recovery, HR approvals, and query-plan cases.

## Drift workflow

1. Any Admin route/navigation/service/handler change regenerates runtime and frontend candidates.
2. CI reports added/removed/changed items and fails.
3. Reviewer updates the semantic manifest and tests or adds an allowed exclusion.
4. A new immutable Draft baseline is produced.
5. Full coverage/secret/parity gates run.
6. Activation supersedes the prior baseline; pending incompatible proposals invalidate.
7. New operations remain available in original Admin screens but unavailable to the agent until activation.

## Planning note on counts

No endpoint/action count in this planning document is a completion claim. The current worktree is changing and the existing parser is stale. The accepted counts and hashes are produced from the sealed runtime/reachable baseline during implementation and reported at release.
