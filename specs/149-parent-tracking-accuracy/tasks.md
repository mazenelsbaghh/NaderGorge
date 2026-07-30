# Tasks: Parent Tracking Accuracy

**Input**: Design documents from `specs/149-parent-tracking-accuracy/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/parent-student-details.yaml`, `quickstart.md`

**Tests**: Mandatory for backend behavior and Android compile safety.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Phase 1: Setup

**Purpose**: Confirm current parent tracking files and preserve existing dirty work.

- [x] T001 Inspect current diff for `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs`, `mobile/parent-android/app/src/main/java/com/nadergorge/parent/data/api/StudentDetailsResponse.kt`, `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/DashboardScreen.kt`, and `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt`; record any unrelated local edits in `achievements.md`.
- [x] T002 Verify `specs/149-parent-tracking-accuracy/contracts/parent-student-details.yaml` matches the DTO names used in `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs`.

## Phase 2: Foundational

**Purpose**: Create one backend read projection that every academic parent section uses.

- [x] T003 [P] Expand backend parent tests in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` with fixture helpers for two teachers, packages, terms, sections, lessons, videos, active/inactive grants, watch events, exams, homework, and balance transactions; expected result: tests can seed all entities without compile errors.
- [x] T004 [P] Update Android API models in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/data/api/StudentDetailsResponse.kt` so exam `attemptId` and `submittedAt` can be absent, exam `status` can be `NotStarted`, homework `submissionState` is represented, and all deployment-sensitive lists remain nullable/defaulted.
- [x] T005 Implement private read-model helpers inside `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs` for active grants and purchased lessons: grant is active only when `IsActive`, `CancelledAt == null`, and `ExpiresAt` is null or in the future; expected result: package, term, section, lesson, and lesson-video grants produce distinct lesson rows with teacher from `Lesson.ContentSection.Term.Package.Teacher`.
- [x] T006 Replace the current package-only `activePackageIds`/`lessonRows` logic in `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs` with the purchased lesson projection from T005; expected result: teachers, attendance, progress, watch logs, exams, and homework all use the same lesson ID set.

## Phase 3: User Story 1 - View Teacher-Based Watch Logs (Priority: P1)

**Goal**: Parent selects a teacher and sees all purchased lessons for that teacher with aggregated watch metrics.

**Independent Test**: Backend returns Teacher A lessons only under Teacher A with zero-watch lessons included.

- [x] T007 [P] [US1] Create/extend a failing test in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` named `GetStudentDetails_ShouldResolveTeachersAndWatchLessonsFromAllActiveGrantTypes`; expected result before implementation: direct lesson/video/section grants are missing or wrong.
- [x] T008 [US1] Implement database grouping for `VideoWatchEvents`, active `LessonVideos`, and `LessonProgresses` in `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs`; expected result: `WatchLessonDetailDto` aggregates distinct watched videos, watch count, watched seconds, completion, and latest timestamp per lesson.
- [x] T009 [US1] Update `WatchLogScreen` and `WatchLessonItem` in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt` to keep teacher selection stable when the teacher list changes and show a zero-watch empty/metric state without crashing.
- [x] T010 [US1] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter GetStudentDetails_ShouldResolveTeachersAndWatchLessonsFromAllActiveGrantTypes`; expected result: passes.

## Phase 4: User Story 2 - Review Exams by Purchased-Lesson Teacher (Priority: P1)

**Goal**: Parent sees exams under the purchased lesson teacher, including unsolved exams and mistake review for attempts.

**Independent Test**: Exam created by Teacher B but attached to Teacher A lesson appears under Teacher A.

- [x] T011 [P] [US2] Create/extend a failing test in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` named `GetStudentDetails_ShouldAssignExamToPurchasedLessonTeacherAndIncludeUnattemptedExam`; expected result before implementation: exam is missing or assigned to creator teacher.
- [x] T012 [US2] Change `ExamDetailDto` in `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs` to include nullable attempt/submission fields as needed by `specs/149-parent-tracking-accuracy/contracts/parent-student-details.yaml`; expected result: unattempted exams can be returned without fake attempt data.
- [x] T013 [US2] Query exams from purchased lessons and purchased lesson videos in `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs`, then left-join the student's latest attempt and answers; expected result: teacher ID/name comes from purchased lesson map, not `Exam.CreatedByTeacherId`.
- [x] T014 [US2] Update `ExamItem` in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt` so `NotStarted`, `Passed`, and `Failed` statuses render with Arabic labels and unattempted exams show no mistake review.
- [x] T015 [US2] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter GetStudentDetails_ShouldAssignExamToPurchasedLessonTeacherAndIncludeUnattemptedExam`; expected result: passes.

## Phase 5: User Story 3 - Review Homework Like Exams (Priority: P1)

**Goal**: Parent sees homework under the purchased lesson teacher with the same list/detail pattern as exams.

**Independent Test**: Homework from a purchased lesson appears under that lesson teacher whether submitted or not.

- [x] T016 [P] [US3] Create/extend a failing test in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` named `GetStudentDetails_ShouldReturnHomeworkForPurchasedLessonTeacherWithSubmissionStates`; expected result before implementation: direct-grant homework or state detail is missing.
- [x] T017 [US3] Ensure homework query in `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs` uses the purchased lesson projection dictionary and returns `NotSubmitted`, `InProgress`, `PendingReview`, `Graded`, or `Missed`; expected result: no lookup exception for purchased lesson IDs.
- [x] T018 [US3] Update `HomeworkInfo` and `HomeworkItem` in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/data/api/StudentDetailsResponse.kt` and `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt` to display `submissionState`, grade, and mistake blocks using the same expand/collapse behavior as exams.
- [x] T019 [US3] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter GetStudentDetails_ShouldReturnHomeworkForPurchasedLessonTeacherWithSubmissionStates`; expected result: passes.

## Phase 6: User Story 4 - View Correct Balance and Balance Details (Priority: P1)

**Goal**: Parent sees authoritative current balance and newest official transactions.

**Independent Test**: Balance screen payload matches `StudentBalance.CurrentBalance` and `BalanceTransaction` ledger.

- [x] T020 [P] [US4] Create/extend a failing test in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` named `GetStudentDetails_ShouldReturnAuthoritativeBalanceAndNewestTransactions`; expected result before implementation: missing balance row or transaction details fail.
- [x] T021 [US4] Align `BalanceDetailsDto` query in `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs` with `backend/src/NaderGorge.Application/Features/Student/Queries/GetStudentBalanceQuery.cs`: use `StudentBalance.CurrentBalance`, query `BalanceTransactions` newest first, and return `0` plus empty list when no row exists.
- [x] T022 [US4] Update `BalanceScreen` and `BalanceTransactionItem` in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt` to show current balance, transaction type/description, amount direction, balance after, and empty transaction state.
- [x] T023 [US4] Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter GetStudentDetails_ShouldReturnAuthoritativeBalanceAndNewestTransactions`; expected result: passes.

## Phase 7: User Story 5 - No-Crash Empty, Missing, and Failure States (Priority: P2)

**Goal**: Parent app remains usable with empty, partial, failed, or old payloads.

**Independent Test**: Android compile succeeds and backend empty fixture returns empty lists/defaults.

- [x] T024 [P] [US5] Create/extend a backend test in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs` named `GetStudentDetails_ShouldReturnEmptyCollectionsForStudentWithoutPurchases`; expected result: zero teachers, zero watch lessons, zero exams, zero homework, zero current balance.
- [x] T025 [US5] Audit null/default access in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/DashboardScreen.kt` and `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt`; expected result: every use of `teachers`, `watchLessons`, `balance.transactions`, `exam.mistakes`, and `homework.mistakes` has a default-safe path.
- [x] T026 [US5] Remove or disconnect the static `ScheduleScreen` path in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt` if it is not referenced; expected result: no user path shows mock schedule rows.
- [x] T027 [US5] Run `make build-mobile-android-offline`; expected result: debug APK builds successfully.

## Phase 8: Polish & Cross-Cutting Verification

- [x] T028 Run `dotnet test backend/tests/NaderGorge.Application.Tests/NaderGorge.Application.Tests.csproj --filter Parent`; expected result: all parent tests pass.
- [x] T029 Run `dotnet build backend/NaderGorge.sln --no-restore`; expected result: backend solution builds without introduced errors.
- [x] T030 Run `make build-mobile-android-offline`; expected result: Android debug APK builds.
- [x] T031 Run `docker compose config -q`; expected result: compose config is valid.
- [x] T032 Document Docker runtime gate status in `achievements.md`: `make up`, optional `make migrate` if schema changed, `curl -f http://localhost:5245/api/health`, and `make ps`.
- [x] T033 Perform deep critique fixes across `backend/src/NaderGorge.Application/Features/Parent/Queries/GetStudentAcademicDetailsQuery.cs`, `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs`, `mobile/parent-android/app/src/main/java/com/nadergorge/parent/data/api/StudentDetailsResponse.kt`, `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/DashboardScreen.kt`, and `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/SubScreens.kt`; expected result: every spec requirement has a matching implementation or recorded exception.
- [x] T034 Run clean-code-guard against changed production files; expected result: no unresolved clean-code-guard findings remain.
- [x] T035 Run test-guard against changed test files; expected result: no unresolved test-guard findings remain.
- [x] T036 Run feature tests from `specs/149-parent-tracking-accuracy/quickstart.md` and record exact results in `achievements.md`; expected result: feature tests pass or blocked checks are documented with reason.
- [x] T037 Run final validation `python3 .agents/skills/speckit-all/scripts/validate_run.py --root . --spec-dir specs/149-parent-tracking-accuracy`; expected result: validation passes.

## Dependencies & Execution Order

- T001-T002 precede all code work.
- T003-T006 are foundational and block US1-US4 implementation.
- US1, US2, US3, and US4 can proceed after T006, but backend DTO changes from T012 must be coordinated with Android model task T004.
- US5 should run after primary data shape tasks so null/empty handling covers final fields.
- T028-T037 run after all story tasks are complete.

## Parallel Opportunities

- T003 and T004 can run in parallel.
- T007, T011, T016, T020, and T024 can be drafted in parallel because they are independent test cases in the same file but must be merged carefully.
- Android UI tasks T009, T014, T018, and T022 touch the same file and should be sequenced by the main implementer to avoid conflicts.

## Implementation Strategy

1. Finish the backend purchased lesson projection first; it is the source of truth for all teacher filtering.
2. Make US1 watch logs pass because it proves the projection and teacher list.
3. Add exams and homework using the same projection.
4. Verify balance separately against the existing student balance query behavior.
5. Compile Android after model/status changes and then run all parent tests.
