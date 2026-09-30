# Admin AI Agent — Implementation Evidence

## 2026-09-30 remaining Teacher-only branches

- Seven additional `/teacher` frontend calls came from shared code-group, subscriber, context, and finance services. Their backend `TeacherController` and `TeacherFinanceController` require the Teacher role. Admin alternatives for code groups and teacher statements remain in the inventory. The seven calls are now reviewed `teacher-surface` exclusions alongside the nine Teacher-only report calls.
- The generated baseline now has 1,073 items, 623 blocked entries, 23 unresolved frontend calls (11 mutations), and 29 reviewed exclusions. Digest: `e05145fce26b401aa6b821c6322d3c88a7897662bf289291a0f1b5eb07b88757`. The full capability gate passed 10/10 frontend graph tests and 26/26 Python tests. The production catalog remains read-only and activation remains blocked.

## 2026-09-30 Teacher-only report branch exclusion

- The shared `advancedReportService` contains both `/admin/reports` and `/teacher/reports` branches. `TeacherReportsController` requires the Teacher role, while the Admin route uses its separate Admin controller. The nine Teacher-only frontend calls are now reviewed `teacher-surface` exclusions; the matching Admin calls remain in the capability inventory, including strong confirmation for Admin report-definition deletion.
- The generated baseline now contains 1,080 items, 623 blocked mutation/external-effect entries, 30 unresolved frontend calls (11 mutations), and 22 reviewed exclusions. Its digest is `ea7b19ead4582cdf5e07a3aa2fbe332e0a20caad130b771d63c1e009ef7bc999`. The full capability gate passed 10/10 frontend graph tests and 26/26 Python tests. The production catalog remains read-only and activation remains blocked.

## 2026-09-30 shared Admin task inventory coverage

- The Admin-accessible Assistant task details, status, and comments routes are now included in the diagnostic backend inventory. The two mutation routes map to their original `UpdateTaskStatusCommand` and `AddTaskCommentCommand`, and the frontend calls inherit those exact operation labels instead of remaining unresolved. The baseline now has 1,089 items, including 628 blocked mutation/external-effect entries; 39 frontend calls remain unresolved, 16 of them mutations. The production registry stays read-only.
- The endpoint inventory was regenerated from current source (831 backend endpoints, 712 frontend calls), followed by the baseline (digest `890be11ad9d7d3bf06bdc7a95a5059f3f4babf7e314f82c5b53b6c397675c797`). The full capability gate passed 10/10 frontend graph tests and 26/26 Python endpoint/capability/source tests. This improves mapping accuracy but does not close T171/T172/T176/T177 or authorize activation.

## 2026-09-30 read-batch callback replay

- A unique turn/batch receipt now binds the exact request to an encrypted response. The receipt, consumed read budget, and renewed lease commit in one PostgreSQL transaction; expired receipts are purged after 24 hours.
- A fresh-context lost-response retry returned the identical result without a second read on PostgreSQL 16.10. A changed-payload retry returned a conflict; an expired receipt was purged on the next recovery sweep. The additive migration passed the EF model guard; the complete AdminAI PostgreSQL integration group passed 35/35 and the application group passed 264/264. The worker now retries transient read-callback failures with the exact same batch payload, including an intermediate in-flight HTTP 409 after an ambiguous response. Its focused 20/20 tests and full 230/230 suite passed without another model request. The combined real-backend/worker/Redis restart gate and activation remain open.

## 2026-09-30 community-post approval identity

- The original community-post approval command now accepts an optional server-owned operation identity. For AdminAI calls, it commits the public status change, audit, outbox notification, and a payload-bound safe result receipt in one PostgreSQL transaction. Original Admin/Teacher calls without an operation identity retain their existing command path.
- A real PostgreSQL replay after a later moderation change returned the original approval result without another publication or audit entry; actor mismatch returned an idempotency conflict. The recovery resolver uses that exact receipt. The complete AdminAI PostgreSQL group passed 36/36 and the application group passed 264/264. This covers one reviewed operation and does not activate the still read-only production catalog.

## 2026-09-30 comment approval identities

- The original lesson-comment and community-comment approval commands now accept the same optional server-owned operation identity. Their status changes, existing audit and outbox effects, and safe result receipts commit atomically for AdminAI calls; the existing screen paths without that identity remain available.
- The full proposal/confirmation PostgreSQL scenario replayed both types of approved comments in a fresh context, returned the receipt-backed result, and kept the outbox counts unchanged. Both recovery resolvers returned the matching safe result. The AdminAI PostgreSQL group passed 36/36; the final changed-target conflict assertion passed on a separate focused rerun. These remain candidate adapters behind the read-only production catalog.

## 2026-09-30 task-comment creation identity

- The original task-comment command now accepts an optional server-owned operation identity and stores the new comment ID with a payload-bound receipt in one PostgreSQL transaction. A changed-payload replay fails before touching the task; a matching replay returns the original ID even if the comment was later deleted. The AdminAI action bridge passes this identity and a recovery resolver returns the same safe result.
- The direct PostgreSQL replay and resolver test passed. The complete AdminAI PostgreSQL group passed 37/37 and the application group passed 264/264; the full proposal/confirmation scenario also passed on a focused rerun after asserting its task-comment receipt and no duplicate comment. The production action catalog remains read-only, and the remaining operations/coverage gates are open.

## 2026-09-30 task status and approval identities

- The original task-status and manager approval/rejection commands now accept optional server-owned operation identities. Their task/pipeline/comment/audit effects and safe result receipts commit in one PostgreSQL transaction for AdminAI calls. Original screen calls without the identity retain their command paths.
- The full proposal/confirmation PostgreSQL scenario replayed the final status change and rejection after later state changes, preserved the task and pipeline, created no second rejection comment, and rejected a changed payload under the same identity. Both recovery resolvers returned the receipt-backed safe result. The complete AdminAI PostgreSQL group passed 37/37 and the application group passed 264/264. The production action catalog remains read-only.

## 2026-09-30 task creation identity

- The original task-create command now accepts an optional server-owned operation identity. Task, audit, workroom, participants, and result receipt save together, replacing the prior two-save sequence. The AdminAI bridge supplies the execution identity and a receipt-backed resolver returns the task ID after an ambiguous outcome.
- A disposable PostgreSQL test created one task and one workroom, replayed the same identity without another effect, rejected changed input, and recovered the task ID. The focused test passed 1/1; the complete AdminAI PostgreSQL group passed 38/38 and the AdminAI plus operations application tests passed 273/273. The production action catalog remains read-only.
- The task-create candidate now has an authoritative, read-only preview. It verifies the assignee is not a student, verifies the actor exists, and fingerprints the assignee and supervisor workroom participants. On PostgreSQL the preview left task and workroom counts unchanged; renaming the assignee changed the fingerprint before confirmation. The focused replay/preview test passed 1/1, the complete AdminAI PostgreSQL group passed 38/38, and the AdminAI plus operations application group passed 273/273.
- Its action adapter is now registered in the API service container alongside the preview and recovery resolver. The production catalog still exposes reads only, so this candidate is not enabled for users.

## 2026-09-30 media pipeline creation identity

- The original media-pipeline create command now accepts an optional server-owned operation identity. The pipeline, audit, and result receipt commit together for AdminAI calls; the original Admin path remains available. The adapter passes the identity, a resolver returns the pipeline ID from the receipt, and the API service container registers both.
- A read-only preview checks the assigned agent's current role and fingerprints its name and student status. It reports whether an asset folder was provided without displaying a URL that may contain a token. A focused PostgreSQL test passed preview without writes, changed fingerprint after an agent rename, one pipeline after replay, changed-payload conflict, receipt recovery, refusal to assign a student, and a token-sentinel exclusion from the preview. The complete AdminAI PostgreSQL group passed 39/39 on the final code, the AdminAI plus operations application group passed 273/273, and the API build passed. The production action catalog remains read-only.

## 2026-09-30 social plan draft creation identity

- The original social-plan create command now accepts an optional server-owned operation identity. For AdminAI calls, draft or scripting plans, audit, and a result receipt commit atomically. Scheduled and published statuses are refused in both preview and the original command's AdminAI path; the ordinary candidate cannot silently represent publication. Existing Admin screen calls without the identity keep the original command path.
- The read-only preview checks the linked media pipeline and fingerprints its title and stage. It reports whether a script is provided without displaying its text. A focused PostgreSQL test passed no-effect preview, changed fingerprint after pipeline stage update, no ordinary publication, one draft on replay, changed-intent conflict, and receipt recovery. The complete AdminAI PostgreSQL group passed 40/40, the AdminAI plus operations application group passed 273/273, and the API build passed. The production action catalog remains read-only.

## 2026-09-30 form draft identities

- The original form-create and form-update commands now accept optional server-owned operation identities and actors. For AdminAI calls, form data and payload-bound receipts commit together. The ordinary path is limited to creating inactive forms and updating inactive forms with no submissions; activation and edits to active/responded forms require a high-risk capability. Original Admin screen calls without an identity keep their existing path. Slug uniqueness checks now normalize case before querying, matching the stored lowercase slug.
- A dedicated read-only preview validates the field JSON array, current slug availability, and update target state. It returns field counts and cover presence without exposing field definitions or tokenized cover URLs. A focused PostgreSQL test passed no-effect creation preview, inactive create/replay/conflict, stale update fingerprint, update/replay/conflict, receipt recovery, and refusal to edit active or responded forms. The complete AdminAI PostgreSQL group passed 41/41, the AdminAI plus operations application group passed 273/273, and the API build passed. The production action catalog remains read-only.

## 2026-09-30 concurrent worker claim

- The internal claim endpoint now turns a PostgreSQL optimistic-concurrency collision into a safe lease conflict. A barrier forced two separate database contexts to load the same queued turn before either claimed it. The real PostgreSQL test passed twice: exactly one worker received a lease, one received HTTP 409, and the durable turn and step advanced once. The disposable PostgreSQL 16.10 container was removed afterward.
- The worker gives each model read batch a distinct key even when multiple batches use the same backend step; its full suite passed 229/229. The backend replay gap identified by this audit was closed by the read-batch receipt described above. The claim test alone does not close T181.

## 2026-09-30 Redis callback replay after worker replacement

- An expired callback lease no longer forces a second model inference. A replacement worker first tries the saved completion; on a lease conflict it claims the same active turn and step again, verifies the baseline and sensitive-policy versions, saves the renewed callback fields in the BullMQ job, and resubmits the original decision. A second claim while any worker lease remains live is rejected without advancing the turn version. The backend accepts a matching already-delivered decision as an idempotent acknowledgment even after the turn deadline, while a wrong callback identity is rejected.
- A disposable Redis 7 instance exercised actual stream ingestion, BullMQ delayed retry, worker instance replacement, and duplicate stream delivery. The first callback failed transiently, the replacement saw a stale lease, then reclaimed and delivered the persisted decision. The observed counts were one inference, two claims, and three delivery attempts; the test passed twice. The temporary Redis container was removed. The worker suite passed 229/229, and the AdminAI application suite passed 264/264, including backend claim-after-reads and terminal callback replay tests.
- T181 still needs a combined real-backend/worker process restart with PostgreSQL and Redis for every specified restart point. This isolated Redis test uses a simulated callback service and does not establish the full end-to-end gate.

## 2026-09-30 turn deadline and restart lease safety

- Internal claim, lease renewal, read continuation, completion, and failure callbacks now use the same absolute turn deadline. Renewal cannot extend a live lease beyond it; expired callbacks return HTTP 410. The application test covers renewal near the deadline and rejection of all four callback paths after it, and passed 2/2 with the existing readiness test.
- The recovery sweep now leaves a step with an unexpired worker lease in progress even when its original start time is old. A disposable PostgreSQL 16.10 run passed both restart recovery integration tests, including this live-lease case, the expired worker/callback paths, and a second no-op sweep. The temporary database container was removed afterward.
- T181 remains open for actual worker and Redis delivery restart evidence; these checks cover backend lease and PostgreSQL recovery only.

## 2026-09-30 student-note authoritative replay slice

- `AddStudentNoteCommand` now accepts the AdminAI execution identity while retaining the original Admin call. A PostgreSQL serializable transaction commits the note and a unique, payload-bound receipt together. The receipt stores a SHA-256 request digest and note identity, not the note text; it survives later note deletion. Reusing the identity with a different request is rejected. An AdminAI result resolver reads the receipt after an ambiguous completion without issuing another write.
- A disposable PostgreSQL 16.10 database was migrated from the current EF model. Two new integration tests passed for replay after deletion, conflicting payload, eight concurrent requests with one resulting note, and resolver identity binding. The complete AdminAI PostgreSQL group passed 29/29 after the final code review. The focused AdminAI application group passed 258/258, `make ops-db-guard` found no pending EF model change, and the AdminAI inventory gate passed 10/10 frontend graph plus 24/24 Python checks.
- This closes one candidate operation's durable replay gap. The production registry remains read-only and the remaining blocked Admin mutations, browser/provider/performance gates, and owner acceptance remain open.

## 2026-09-30 subject-create authoritative replay slice

- `CreateSubjectCommand` now uses the same unique receipt table when invoked with an AdminAI operation identity and actor. The subject and receipt commit in one serializable transaction. A replay returns the original subject ID even if that subject was later deleted; a changed request is rejected. The original Admin call still uses its existing command arguments and behavior.
- A resolver can recover the original subject ID from the receipt without creating a second subject. The disposable-PostgreSQL replay/deletion/conflict test passed 1/1. After this change the full AdminAI PostgreSQL group passed 30/30 and the focused application group passed 259/259. The EF migration guard and capability inventory gate passed; the manifest remains blocked pending the other operations and acceptance gates.

## 2026-09-30 subject-update authoritative replay slice

- `UpdateSubjectCommand` now binds the actor, target, canonical requested values, and AdminAI execution identity to an immutable receipt in the same serializable transaction as the edit. A later replay returns the recorded success without rewriting newer subject values; changing the request under the same identity fails. The existing Admin call remains supported without an operation identity.
- The recovery resolver detects the committed receipt without applying the edit again. A disposable-PostgreSQL test passed for a later edit, replay, conflict, and resolver identity; the full AdminAI PostgreSQL group passed 31/31 and the focused application group passed 260/260. `make ops-db-guard` and the capability inventory gate passed. The production registry and baseline are still read-only/blocked.

## 2026-09-30 video-type safe-result replay slice

- The shared receipt now has an additive, bounded `SafeResultJson` column. Video-type create/update commands bind their canonical request and actor to the AdminAI execution identity, commit their existing audit plus the receipt in one serializable transaction, and return the original `VideoTypeDto` on replay. The resolver restores that exact safe DTO after an ambiguous response, even if a created type was deleted or an updated type changed again. Both commands detach failed entities and audit rows after a database duplicate-name rejection so a later save in the same request cannot retry that rejected write.
- The generated EF migration adds only the nullable `SafeResultJson` column. Two disposable-PostgreSQL tests passed for create-after-delete and update-after-later-edit replay, conflicting payloads, original audit counts, and resolver result/identity parity. The complete AdminAI PostgreSQL group passed 33/33, the focused application group passed 261/261, and the existing video-type lifecycle group passed 17/17. The EF migration guard and inventory gate passed. These remain unregistered candidate actions in a blocked manifest.

## 2026-09-30 watch-request operation recovery

- The original watch-approval command now accepts an optional operation identifier. It records that identifier and the request ID on the persisted `VideoOverride`, with a unique non-null database index. Replaying the same request, actor, reason, and view increment returns success without another view increase or notification; reuse with different inputs fails. AI approvals refuse an unbounded or missing watch event because that path cannot leave a durable override marker.
- A dedicated resolver recognizes a persisted override only when its identifier matches the execution ID. The existing recovery sweep can then mark the execution and proposal succeeded after an interrupted response. The generated additive migration changes only `video_overrides` with two nullable columns and indexes.
- The migrated PostgreSQL flow passed 1/1. It verified two approved view increases, harmless direct replay, conflicting replay rejection, and restoration of a simulated `RecoveryRequired` execution and proposal without a third override. This closes the operation-level replay gap for this candidate; the other blocked mutations and the full T171 inventory remain open.
- The complete AdminAI PostgreSQL integration group passed 25/25 with this migration. The generated 1,054-item diagnostic baseline now classifies both backend and frontend watch-approval routes as strong confirmation; all five baseline contract assertions passed. Baseline activation remains `blocked` while the remaining operation mappings are reviewed.

## 2026-09-30 watch-request approval candidate

- The original Admin screen intentionally lets an already approved request receive another explicit view increase. The authoritative command now rejects a reason longer than its database field and a view increment that would overflow `int`, before mutating tracked state. The AdminAI candidate previews the exact student's video limit, watch lock and request status, and invalidates a changed limit before execution.
- The implementation plan places watch mutations in the high-risk family, so this candidate uses TypedStrong confirmation and the existing high-risk MediatR bridge. The generated diagnostic baseline's initial ordinary label was reconciled for both Admin watch-approval routes during T172 work; the full baseline remains blocked.
- A migrated PostgreSQL flow passed 1/1 with 18 confirmed executions. It rejected an overflowing direct command without changing request status, invalidated a stale AI proposal without a video override, then confirmed two distinct view increases (4→6→7) with two overrides and four notifications. The complete application suite passed 1,644 tests with 20 skips. This candidate alone does not seal the baseline.

## 2026-09-30 moderation candidates

- Added reviewed candidate previews for lesson-comment, community-post, and community-comment approval through the existing Admin commands. The lesson preview requires Pending status and an approved parent. The post preview requires the same effective academic scope as its command when no teacher owns the post; it binds post text, poll options, and effective scope rows to the confirmation fingerprint. The community-comment preview also requires an approved post and parent comment.
- Repaired the authoritative community-comment approval command to reject an already resolved comment, an unpublished post, or an unapproved/mismatched parent. This prevents repeat public outbox notifications and orphaned reply publication for the original Admin and teacher screens as well as AdminAI.
- A migrated PostgreSQL flow passed 1/1 with 16 confirmed executions. It blocked premature replies and an unscoped post, invalidated edited content, academic scope, and a poll option, then verified two approvals in each moderation family and eight notification outbox rows. The original post command used the real `AcademicScopeService` in this run. The complete application suite passed 1,644 tests with 20 skips.
- These three actions remain candidates. No production action catalog or approved full baseline exists yet, so feature activation remains closed.

## 2026-09-30 ordinary operations candidate

- The reviewed candidate set now has eight ordinary capabilities: five identity/content commands plus task comment, task status, and the Admin task approval/rejection route. The operations previews read the current task, Admin/manager role, and media-pipeline stage, then bind those values to the proposal fingerprint before the authoritative MediatR commands run.
- A migrated PostgreSQL flow passed 1/1 with ten confirmed executions: it rejected a stale comment proposal without writing a comment; then it verified task comment, status change, approval with Completed/Approved media stage, and rejection with InProgress/Editing stage and a persisted reason comment. The complete focused AdminAI application suite passed 258/258. API compilation passed with no warnings or errors.
- These are candidate actions only. The production registry remains read-only and the baseline remains blocked; this evidence does not approve or activate the whole Admin mutation inventory.

## 2026-09-30 teacher financial review extraction and candidate

- `AdminFinanceController.ReviewTeacherEvent` now delegates to `ReviewTeacherFinancialAllocationCommand`. The command owns the teacher-account credit in a serializable transaction and persists a unique review operation ID, actor, and note on the allocation. A replay in a fresh context returns the same success without crediting twice; a changed payload with the same ID is rejected.
- Two PostgreSQL tests passed: durable replay/conflict plus eight concurrent review attempts with exactly one balance effect. A third PostgreSQL test ran the AdminAI proposal, typed strong confirmation, original command, and result resolver on a migrated database. That test also invalidated a stale financial proposal before any account credit, then recovered a completed operation without a second credit; the expanded case passed 1/1 on rerun. The earlier three-test focused run passed 3/3; the full AdminAI PostgreSQL integration group passed 27/27 before the expanded case. The focused AdminAI application group passed 258/258.
- The frontend graph generator recognizes direct calls through imported service objects whenever their use is proven; uncertain runtime object escapes retain all members. The shared subscriber component calls each surface service directly, removing 29 unrelated teacher calls. The moderation component provides only its four used Admin methods, and references inside TypeScript `typeof` types no longer imply runtime use; this removed another 19 unrelated calls. The 9/9 graph contract suite, frontend typecheck and focused lint passed. The frontend check passed at 527 reachable files and 490 calls. The semantic digest tracks the generator source; watch-request approval is classified under identity.
- The backend filter now includes every `/api/admin` route even when its controller class has a different name. It restored 14 assessment review routes and AI provider status, linking seven previously unresolved frontend calls. The regenerated baseline passed at 1,010 items, 605 blocked effects, 392 exact frontend/backend route links, and 98 unresolved frontend routes (55 mutations). Seven Python inventory assertions and the baseline check passed. These newly found Admin operations remain blocked pending adaptation.
- Explicit branches for exam/homework revisions, period close/reopen, six payroll transitions, and four content-subscriber levels now expose static frontend routes. The generated baseline passed at 1,026 items, 613 blocked effects, 417 exact links, and 89 unresolved routes (51 mutations). Every literal `/admin/` and `/hr/` call is linked; the 89 remaining calls include other surfaces and unknown dynamic paths. Eight Python assertions, TypeScript typecheck, focused ESLint, and both generator checks passed.
- The endpoint filter now includes permission-gated support connections, support staff, WhatsApp campaign/template routes, the exam Admin unlock, and Admin video-learning author/report/AI routes. The public WhatsApp webhook remains excluded. Explicit campaign pause/resume/cancel, staff/participant attachment, and video-learning read branches improve exact links. The generated baseline passed at 1,083 items, 646 blocked effects, 453 exact links, and 57 unresolved routes (29 mutations). Nine Python assertions, nine frontend graph contracts, TypeScript, focused ESLint, and both generator checks passed. A shared attachment reader legitimately retains its participant GET branch conservatively.
- The advanced-report service now exposes literal Admin and Teacher route branches. Nine Admin report calls link exactly to `AdminReportsController`; the Teacher variants remain diagnostic other-surface calls. The generated baseline passed at 1,092 items, 651 blocked effects, 462 exact links, and 57 unresolved routes (29 mutations). Ten Python inventory assertions, TypeScript, focused ESLint, and generator checks passed.
- Explicit Admin/Teacher code-group and teacher-statement routes plus Axios query parameters for recharge requests removed the last generic dynamic calls outside the AdminAI transport service. Runtime endpoint inventory was regenerated from `EndpointDataSource`; its 3/3 focused tests passed and it added the two Admin and two Teacher statement routes. The diagnostic manifest now has 1,086 items, 642 blocked effects, 468 exact links, 43 unresolved calls (19 mutations), and 13 reviewed self-service exclusions for the AdminAI conversation/proposal transport. Its transport path builder is checked by the mandatory 10/10 frontend graph suite. Ten Python inventory assertions, TypeScript, focused ESLint, and both generator checks passed.
- The complete `verify-admin-ai-capabilities` target first exposed one stale source endpoint inventory. Regeneration then found one genuine missing backend route: the Admin-only code-group creator carried an unused Teacher bulk-generation branch. Removing that branch left 831 backend endpoints, 712 frontend calls, and zero missing-route findings. The full target passed 10/10 Node graph tests and 24/24 Python tests using temporary `pytest`; TypeScript, focused ESLint, and whitespace checks passed. The current AdminAI manifest has 1,085 items, 641 blocked effects, 468 exact links, 42 unresolved calls (18 mutations), and 13 reviewed self-service exclusions. Production activation remains blocked.
- A follow-up risk parity audit found 54 exact frontend/backend matches with different risk or domain metadata, including deletion calls incorrectly marked Ordinary on the frontend. Exact matches now inherit effect, domain, risk, confirmation, and refresh scopes from the authoritative backend endpoint. Unresolved DELETE calls require strong confirmation. All six Python inventory assertions passed, and the generated baseline check and whitespace check passed. The registry is still read-only.
- The production registry remains read-only. This one finance candidate does not resolve the other blocked mutations or satisfy T151, T171, T172, T176, or release acceptance.
- The existing local preview allowed the AdminAI Chromium UI contract suite to run with installed Chrome: 16 passed, one real-backend reconnect case skipped while the E2E seed API was unavailable. This does not close T066 or owner manual QA.

## 2026-09-30 reviewed activation gate

- Enabling AdminAI no longer manufactures or activates a read-only baseline at backend startup. Startup now requires exactly one manually approved active baseline, a catalog with actions, a matching registry hash and exact key/version list, supported unique inventory items, and no current-business exclusion. Duplicate JSON fields and stale or incomplete catalogs fail closed before policy bootstrap. The feature remains disabled in the current release configuration.
- The new activation contract suite passed 9/9, covering read-only startup, exact approved baseline, missing or stale capabilities, blocked items, business exclusion, duplicate fields, missing approval, and two active baselines. The complete AdminAI application group passed 255/255. These are guard tests, not proof that the still-blocked production inventory has been sealed or approved.

## 2026-09-30 action wire contract

- The three authoritative action bridges now deserialize exact camelCase JSON field names, matching the worker's proposed `arguments` and the closed action schemas. They still reject unknown or incorrectly cased fields before command dispatch.
- All 14 currently implemented ordinary bridges accepted camelCase inputs during preview; a student-note command received the correct target and actor; the secure password-reset bridge accepted camelCase target input and rejected wrong casing before dispatch. The focused AdminAI application group passed 246/246. This corrects a shared bridge contract but does not register the missing production action catalog.

## 2026-09-30 PostgreSQL restart recovery matrix

- Added a real PostgreSQL restart-context matrix for cancelled and stale queued turns, claimed/provider-running/reads-completed worker leases, exhausted pending callback delivery, and an already completed turn. A fresh DbContext performed the sweep and a separate context verified the durable outcomes and replay-safe second sweep.
- A second matrix covered stale `Claimed` and `Executing` action executions, an already succeeded effect, expired proposals and challenge, and purging an expired secure-input payload. Both new tests passed 2/2; the complete AdminAI PostgreSQL integration group passed 23/23 against a disposable PostgreSQL 16.10 instance, which was stopped and removed afterward.
- T181 remains open for actual worker and Redis delivery restart/callback acceptance evidence. These database sweeps alone do not prove end-to-end recovery.

## 2026-09-30 closed action-input contract

- Proposal construction now validates every nested field against a closed, recursively checked action schema before invoking an authoritative preview or persisting a proposal. Unknown or duplicate fields, malformed UUIDs, enum/range/length/item-count violations, and ignored or open schema keywords fail closed. Optional nested schemas are checked even when omitted from the input.
- Focused proposal tests passed 21/21 and the full AdminAI application group passed 242/242. Negative cases assert zero preview calls and zero persisted proposals. This validates the proposal boundary; it does not supply the missing production action catalog or complete action parity.
- The create response and later get/cancel response now use the same redacted, persisted preview as JSON objects. A preview containing a prohibited password field kept its raw sentinel out of the response and storage. The proposal group passed 29/29 and the full AdminAI application group passed 244/244 after this correction.

## 2026-09-30 serializable confirmation claim

- T110: a disposable PostgreSQL 16.10 database exposed `40001` during two simultaneous confirmations of one proposal. The executor now retries only a failed pre-effect claim in a fresh serializable transaction; it never retries after invoking an authoritative adapter. Matching retries may observe the durable `Claimed` row while the first request is still executing, and a later replay returns the same terminal execution ID.
- The real PostgreSQL suite passed 7/7: ambiguous outcome recovery, same-intent replay and conflicting key, two-tab single effect, stale fingerprint, conflicting payload across proposals, second-Admin ownership denial, and a database-backed adapter executing after the claim commit. The two-tab test passed three additional independent runs. The focused AdminAI application suite passed 225/225. Frontend typecheck and focused lint passed after the execution card was corrected to present `Claimed`/`Executing` as pending. The disposable PostgreSQL container was stopped and removed after verification.
- The source-only GitHub review branch is updated after verification; shared `codex/production` and production services remain unchanged. Full baseline coverage, provider, real-backend browser, performance, restart, and owner acceptance gates remain open.

## 2026-09-29 continuation

- T110/T181 partial PostgreSQL recovery evidence: a disposable PostgreSQL 16.10 test reproduced a duplicate effect when an authoritative adapter raised an ambiguous exception after execution. The executor now commits its unique execution claim before invoking the adapter. A repeat with the same idempotency key returns the existing `RecoveryRequired` execution without reissuing the effect. The recovery sweep also converts a stranded claim older than five minutes to `RecoveryRequired` without execution; the combined real-database test passed 1/1. This covers one ambiguous failure and one restart state, not the full concurrency and restart matrices required by T110/T181.
- A subsequent full AdminAI integration run was briefly blocked during compilation by concurrent, unrelated edits to `TeacherStatementService.cs`. After that file was corrected by its owner, the complete AdminAI PostgreSQL integration group passed 15/15, including the short-fingerprint regression. No finance file was changed as part of AdminAI.
- T062 PostgreSQL read performance: a disposable PostgreSQL 16.10 run passed `AdminAIReadQueryPlanTests` (1/1), then the complete AdminAI integration group passed 14/14 on another disposable instance. The student search used two database commands with one and then 2,001 student records, returned the same sole match under a five-second command and cancellation deadline, and its normalized-name partial-search plan used `IX_users_admin_ai_normalized_name_trgm`. The first test run exposed a detached-role seed error; the seed now references the existing role by ID, and the rerun passed. Both disposable containers/databases were removed.
- T016 migration tests ran against a disposable local PostgreSQL 16.10 container and an isolated test database. All four tests passed, including upgrade from the predecessor migration with a preserved business-data sentinel, restricted foreign keys, partial unique indexes, and required checks. The container and its anonymous data volume were removed after the run.
- T056 Phase 2 checkpoint: 220/220 AdminAI application tests, 13/13 AdminAI integration tests on disposable PostgreSQL 16.10, 228/228 worker tests, and 15/15 focused frontend contract/store tests passed. An unrelated security setting added after the original endpoint-inventory fixture was written caused two initial endpoint tests to fail; the fixture now supplies a synthetic test-only media-relay secret, and the complete integration group passed on rerun. The migration test preserved its preexisting-data sentinel and asserted no destructive operations. Source inspection found no direct LiveSupport/chat entity dependency in the AdminAI domain, mapping, or application contract modules; `live-support.summary` is an intentional bounded read capability.
- The current generated capability baseline contains 1,125 items, including 659 blocked items corresponding to 307 distinct authoritative-operation labels. Ninety-four items still require extraction of direct controller writes. The production catalog is read-only and action bridges are not registered; full activation remains blocked.
- The Codex CLI provider boundary and node-3 model were verified with synthetic prompts only. No code or image was published to production.

## Phase 1 baseline seal

**Started from revision:** `1eae3a01c6b21db160ff66e54a804f1ee40a516a`
**Worktree state at start:** 74 modified or untracked paths; no owner change was staged, discarded, moved, or rewritten.

### Owner-change overlap rules

- Treat every pre-existing modified/untracked product file as owner work until its author explicitly hands it off.
- Never overwrite, format wholesale, or revert an owner file. Re-read its current diff immediately before a necessary narrow integration edit.
- Prefer new `AdminAI` namespaces, files, routes, migrations, tests, and frontend feature directories.
- The following existing files are known high-conflict integration points and require a narrow, reviewed edit only: `AdminController.cs`, `HrAttendanceController.cs`, `HrEmployeesController.cs`, `LiveSupportAdminController.cs`, `AdminShellChrome.tsx`, `navigation.tsx`, `admin-service.ts`, `content-service.ts`, `hr-service.ts`, `live-support-service.ts`, and `api-client.ts`.
- Existing untracked HR/content/live-support files remain owner work. They must not be deleted, renamed, or absorbed into the AdminAI feature.

### Baseline observations before implementation

- `tests/endpoint_inventory.json` was stale at the start; `node scripts/generate-endpoint-inventory.mjs --check` failed with the expected stale-inventory result.
- The legacy source-regex inventory is diagnostic only. The AdminAI release baseline will be the deterministic merge of runtime `EndpointDataSource`, reachable Admin frontend call graph, and reviewed semantic metadata.
- No AdminAI capability, model, transcript, proposal, audit event, controller, worker queue, database migration, or frontend route existed at the start of this phase.

### Deferred owner-file extraction blockers

The sealed manifest must flag direct controller database writes and operations without durable idempotency as blocked until their business logic is extracted to an authoritative application command/service. Known examples include pending-essay state mutation, selected platform and teacher-finance operations, shared-package mutations, and multiple HR controller writes. A controller wrapper is never an authoritative AdminAI executor.

## Commands and results

| Command | Result |
|---|---|
| `SPECIFY_FEATURE=169-admin-ai-agent .specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks` | Passed; feature documents present. |
| `node scripts/generate-endpoint-inventory.mjs --check` | Failed as expected before T005; baseline is stale. |
| `node scripts/generate-endpoint-inventory.mjs --check` after T005 | Passed; diagnostic inventory contains 675 backend endpoints and 571 frontend calls. |
| `dotnet test backend/tests/NaderGorge.Integration.Tests/NaderGorge.Integration.Tests.csproj --filter FullyQualifiedName~AdminAIEndpointInventoryTests --no-restore` | Passed: 1/1 runtime route inventory contract. |
| `node --test frontend/scripts/generate-admin-ai-capability-baseline.test.mjs` | Passed: 2/2 reachable-route graph contracts. |
| `node frontend/scripts/generate-admin-ai-capability-baseline.mjs --check` | Passed: 392 reachable Admin files and 509 reachable calls. |
| `node scripts/generate-admin-ai-capability-baseline.mjs --check` | Passed: 948-item blocked candidate manifest and generated Markdown table. |
| `python3` direct invocation of `test_admin_ai_capability_inventory.py` test functions | Passed. `pytest` is not installed in the local Python runtimes, so the equivalent assertions were run without installing a global dependency. |

### Current baseline state

The generated `tests/admin_ai_capability_baseline.json` is intentionally `blocked`: it contains diagnostic source data and conservative semantic classification, but no runtime snapshot export or owner-reviewed operation mapping yet. Its mutation entries all carry an explicit adapter blocker. This means the feature remains fail-closed while Phase 1 reconciliation continues.

The runtime export is now present at `tests/admin_ai_runtime_endpoint_inventory.json`; the generator validates every included diagnostic endpoint against it before merging. The sealed candidate has 948 items and digest `6f1122bce16c19ee42356b85bab015e7316283dcaa585b222a56d72c66d728b2`. Every item has one candidate/blocked disposition, no exclusion is present, and all 552 mutations are blocked pending authoritative adaptation; 93 direct-controller items have explicit extraction blockers.

### Phase 1 final verification

- Runtime/source diagnostic inventory: 676 backend endpoints, 571 frontend calls, zero missing route findings.
- Reachable Admin graph: 392 files and 509 calls.
- Canonical AdminAI manifest: 948 items, activation `blocked`; every mutation has missing idempotency/concurrency/audit called out and a refresh scope.
- `AdminAIEndpointInventoryTests`: 2/2 passed.
- Frontend graph Node tests: 2/2 passed.
- Python endpoint and AdminAI baseline tests: 9/9 passed.
- Phase 1 result: PASS. Unknown/new operations fail closed until a new baseline is regenerated, reviewed, and activated.

## Phase 2 foundation progress

- Added the isolated AdminAI domain model, DbSets, restricted EF mapping, JSONB fields, uniqueness/check/index/concurrency contracts, and additive `AddAdminAIAgent` migration.
- The migration `Up` creates 13 AdminAI tables and contains no drop/delete/seed operation; the generated `Down` removes only those new tables.
- AdminAI model/sentinel tests passed 17/17; Infrastructure build completed with zero warnings and zero errors.
- Added PostgreSQL-backed current-Admin/security-version access revalidation, purpose-separated encryption/HMAC, an immutable closed capability registry, recursive sensitive-schema defense, and append-only redacted evidence plus AuditLog summaries.
- Added stable `ai-admin-agent-turns/respond` queue identity, schema-v1 worker decision parsing/canonical hashing, and a bounded internal callback client for claim/renew/read/complete/fail. Worker focused tests passed 7/7.
- Added the standalone Admin-only `/admin/ai-agent` shell route, fail-closed responsive RTL workspace, content-free realtime envelope validator, owner-scoped query keys, AbortSignal REST client, and in-memory-only event/intent store. Frontend typecheck, focused ESLint, route-permission contract, and realtime tests passed.
- Current focused backend evidence: AdminAI application tests 43/43 and AdminAI integration tests 4/4 passed. Feature activation and every platform mutation remain blocked; no action executor is registered.
