# Tasks: Session-Safe Video View Counting

**Input**: `specs/140-fix-video-session-counting/spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/video-progress-api.md`, `quickstart.md`  
**Target prompt**: create the tasks file so that a cheaper llm model can implement without problems

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) produced and validated `specs/140-fix-video-session-counting/spec.md`.
- [x] Phase 2: Arabic Clarification (`speckit-clarify`) encoded session renewal and superseded-player behavior in `specs/140-fix-video-session-counting/spec.md`.
- [x] Phase 3: Technical Planning (`speckit-plan`) produced all design artifacts and passed plan validation.
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`) mapped every story to atomic implementation and verification work.

## Phase 1: Setup and Red Tests

- [x] T001 Verify `specs/140-fix-video-session-counting/checklists/requirements.md` has zero incomplete items and record the pass in `achievements.md`.
- [x] T002 [P] Replace the legacy same-session multi-count expectations with failing session-bound tests in `backend/tests/NaderGorge.Application.Tests/VideoWatchProgressTests.cs`: one view maximum, excess discard, stable duplicate sequence, partial progress across two sessions, max lock, renewal, superseded/expired/mismatched rejection.
- [x] T003 [P] Add a failing stale-session player journey in `frontend/tests/e2e/video-session-counting.spec.ts` that expects pause, Arabic newer-tab/device text, and a reload control after `SESSION_SUPERSEDED`.
- [x] T004 [P] Update `tests/test_video.py` contract coverage so sessionless `/api/tracking/video-event` expects non-mutating `SESSION_REQUIRED`, while the session-aware endpoint requires session ID and sequence.

## Phase 2: Foundational Session State

- [x] T005 Add `HasRegisteredView`, `LastProgressSequence`, `LastProgressAt`, and `IsSuperseded` with documented defaults to `backend/src/NaderGorge.Domain/Entities/VideoPlaybackSession.cs`.
- [x] T006 Add playback-session defaults and the `(UserId, LessonVideoId, CreatedAt)` index in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`.
- [x] T007 Create and inspect an EF migration under `backend/src/NaderGorge.Infrastructure/Migrations/` that adds only the four session fields and lookup index, then update `AppDbContextModelSnapshot.cs`.

## Phase 3: User Story 1 — One View Per Session (P1)

**Goal**: A session contributes actual time only until its first threshold and never counts a second view.

**Independent Test**: One session reaches the threshold, then sends additional/retried progress; persisted seconds stop at the boundary and count remains +1.

- [x] T008 [US1] Change `TrackWatchProgressCommand`, response DTO, and handler in `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs` to require session ID/positive sequence, validate ownership/video/lifecycle, return stable session errors, and perform all session/watch changes in one serializable transaction.
- [x] T009 [US1] Add an exact remaining-boundary cap to `backend/src/NaderGorge.Application/Common/VideoWatchProgressCalculator.cs` so one call cannot cross more than one threshold and excess seconds cannot seed the next view.
- [x] T010 [US1] Persist `LastProgressSequence`, treat lower/equal sequences as successful no-ops, set `HasRegisteredView` on the first threshold, and ignore later session contributions in `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs`.
- [x] T011 [US1] Replace `RegisterView` with `SessionId` and `ProgressSequence`, and map validation/session/409 errors in `backend/src/NaderGorge.API/Controllers/VideoSessionController.cs` exactly as `specs/140-fix-video-session-counting/contracts/video-progress-api.md` defines.
- [x] T012 [US1] Make `backend/src/NaderGorge.Application/Features/Tracking/Commands/RecordVideoEventCommand.cs` return non-mutating `SESSION_REQUIRED`, preserving the endpoint only as an explicit compatibility failure.
- [x] T013 [US1] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter FullyQualifiedName~VideoWatchProgressTests`; expected result: one-session, idempotency, boundary, and lock tests pass.

## Phase 4: User Story 2 — Continue Partial Progress Across Sessions (P2)

**Goal**: Refresh creates a fresh eligible session while preserving incomplete aggregate progress.

**Independent Test**: Three accepted seconds in session A plus three in refreshed session B persists six seconds; a session created after a counted session can register exactly one next view.

- [x] T014 [US2] Change `backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs` to create a distinct row on every successful open, supersede prior active rows for the same student/video transactionally, and never reuse an already active session.
- [x] T015 [US2] Renew `ExpiresAt` and `LastProgressAt` on each valid newest-session heartbeat without resetting `HasRegisteredView` in `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs`.
- [x] T016 [US2] Preserve negative-time reset normalization, cumulative pre-threshold seconds, custom maximum, exact lock, extra-watch, admin reset, and repurchase behavior in `backend/src/NaderGorge.Application/Features/Student/Commands/TrackWatchProgressCommand.cs` and `VideoWatchProgressCalculator.cs`.
- [x] T017 [US2] Extend `backend/tests/NaderGorge.Application.Tests/VideoWatchProgressTests.cs` with explicit cross-refresh, post-count refresh, renewal-after-count, reset-signal, and custom-limit assertions; expected persisted totals and counts must match `spec.md`.

## Phase 5: User Story 3 — Newest Session Wins (P3)

**Goal**: Older tabs/devices cannot mutate watch state and visibly stop with recovery.

**Independent Test**: Create session B after session A; A receives 409 without persistence changes, while B succeeds, and A's player pauses with reload UI.

- [x] T018 [US3] Extend `frontend/src/services/video-session-service.ts` so `trackProgress` sends `sessionId`, stable positive `progressSequence`, watched delta, and duration using the existing Axios service layer.
- [x] T019 [US3] Store the active session ID and logical flush sequence in `frontend/src/components/video/SecureVideoPlayer.tsx`; retry the same sequence/delta until success, advance only after acceptance, and continue zero-contribution heartbeats for expiry renewal after a view registers.
- [x] T020 [US3] Handle `SESSION_SUPERSEDED` in `frontend/src/components/video/SecureVideoPlayer.tsx` by clearing pending/timers, pausing the iframe, stopping retries, showing `تم فتح الفيديو في تبويب أو جهاز أحدث. أعد تحميل الفيديو للمتابعة هنا.`, and rendering a page-reload action.
- [x] T021 [US3] Ensure expired/invalid session failures use recoverable Arabic player errors without leaking ownership details in `frontend/src/components/video/SecureVideoPlayer.tsx`.
- [x] T022 [US3] Complete `frontend/tests/e2e/video-session-counting.spec.ts` service interception assertions for stable retry sequence and the superseded stop/message/reload outcome.

## Phase 6: Regression and Contract Closure

- [x] T023 Run `python3 -m pytest tests/test_video.py -k watch -q`; expected result: legacy bypass is rejected and current extra-watch/reset cases pass.
- [x] T024 Run `npm --prefix frontend run test:e2e -- video-session-counting.spec.ts`; expected result: newest-session and retry UI cases pass.
- [x] T025 Run `dotnet test backend/NaderGorge.sln --no-restore`, `npm --prefix frontend run lint`, and `npm --prefix frontend run build`; expected result: zero feature-introduced compile/lint failures.

## Phase 7: Mandatory Quality Gates and Final Verification

- [x] T032 Resolve Docker migration verification safely after `make migrate` found a pre-existing populated schema with no `__EFMigrationsHistory`; verify all migrations on a disposable database and restore services to the original database without modifying its schema/history.
- [x] T033 Deep-review finding: strengthen `frontend/tests/e2e/video-session-counting.spec.ts` so a temporary failure retries the exact same sequence and seconds before the superseded-session response; remove a legacy response-field fallback from `tests/test_video.py`.
- [x] T034 Clean-code-guard findings: replace the five-argument frontend progress call and eight-argument backend progress helper with typed request/context objects, extract frontend response/error state transitions, remove dead parameter assignments, and remove a restating step comment.
- [x] T035 Test-guard finding: split the combined expired-or-mismatched backend test in `backend/tests/NaderGorge.Application.Tests/VideoWatchProgressTests.cs` into one observable scenario per test.
- [x] T026 Perform deep architectural/code/UI critique against `specs/140-fix-video-session-counting/spec.md`, `plan.md`, and `tasks.md`; record every finding in both `achievements.md` and this file, fix it, and verify it before checking the item.
- [x] T027 Run `clean-code-guard` against every changed production-code file, record/fix all findings in `achievements.md` and this file, then verify relevant tests.
- [x] T028 Run `test-guard` against every changed test file after clean-code-guard, record/fix all findings in `achievements.md` and this file, then verify the focused suites.
- [x] T029 Run final feature tests from `specs/140-fix-video-session-counting/quickstart.md`, covering every user story, access failures, validation, persistence, concurrency/idempotency, lock/reset regressions, and UI smoke behavior.
- [x] T030 Run `docker compose config -q`, `make up`, `make migrate`, `make ps`, and health checks from `quickstart.md`; expected result: migration succeeds and backend/frontend/worker surfaces are healthy, or any external blocker is recorded precisely.
- [x] T031 Run final backend/frontend build verification, update `achievements.md` with exact commands/results, mark all dynamic findings resolved, and run `validate_run.py` before the final report.

## Dependencies and Execution Order

- T001–T004 establish passed checklists and red behavior tests.
- T005–T007 create shared persisted state and block every story.
- US1 T008–T013 establishes the session-safe counting invariant.
- US2 T014–T017 depends on US1 aggregate/session behavior.
- US3 T018–T022 depends on the final backend contract from US1/US2.
- T023–T025 close functional regressions; T026 → T027 → T028 → T029 → T030 → T031 is mandatory strict order.

## Parallel Opportunities

- T002, T003, and T004 touch separate test surfaces and may run in parallel.
- After the backend contract stabilizes, T018 service typing and T017 backend regression tests touch separate files.
- Backend focused tests and frontend lint may run in parallel only after their respective implementation tasks finish.

## Implementation Strategy

Implement the shared session state and backend P1 invariant first. Validate one-view-per-session independently, then add refresh/renewal, then wire the newest-session frontend recovery. Do not claim concurrency correctness from EF InMemory alone; include a Docker/PostgreSQL-backed path in final feature tests.
