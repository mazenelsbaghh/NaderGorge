# Tasks: Parent Tracking System & Mobile Apps

**Input**: Design documents from `/specs/147-parent-tracking-app/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are MANDATORY for this project when a phase changes behavior, data, permissions, API contracts, worker jobs, or user-visible UI. Include backend, frontend, worker, and mobile unit tests.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create mobile directories `mobile/parent-android` and `mobile/parent-ios`
- [ ] T002 Initialize Kotlin project with Jetpack Compose dependencies in `mobile/parent-android`
- [ ] T003 Initialize Swift SPM project with SwiftUI dependencies in `mobile/parent-ios`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Update `StudentProfile.cs` with `ParentTrackingCode` and `HasSeenTrackingCodePopup` fields in `backend/src/NaderGorge.Domain/Entities/StudentProfile.cs`
- [ ] T005 Create `ParentDeviceToken.cs` entity in `backend/src/NaderGorge.Domain/Entities/Notifications/ParentDeviceToken.cs`
- [ ] T006 Add `ParentDeviceToken` db set and config mapping in EF Core `backend/src/NaderGorge.Infrastructure/Data/ApplicationDbContext.cs`
- [ ] T007 Generate and run EF Core database migration for parent tracking updates in `backend/src/NaderGorge.Infrastructure`
- [ ] T008 [P] Add JWT token service method for Parent role in `backend/src/NaderGorge.Infrastructure/Services/TokenService.cs`

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Student Tracking Code Web UI (Priority: P1) 🎯 MVP

**Goal**: Display parent tracking code to student on web app and acknowledge the modal

**Independent Test**: Student logs in, sees glassmorphism modal with code and copy icon, closes modal. Reloads page, modal is gone, badge in header displays code. Expected result is modal displays correctly and doesn't repeat.

- [ ] T009 [P] [US1] Create frontend mock and contract verification for popup endpoint `/api/student/acknowledge-tracking-popup` in `frontend/tests/e2e/parent-flow.spec.ts`
- [ ] T010 [P] [US1] Implement `ParentCodePopup.tsx` glassmorphism component in `frontend/src/components/student/ParentCodePopup.tsx`
- [ ] T011 [P] [US1] Implement `HeaderParentBadge.tsx` component in `frontend/src/components/layout/HeaderParentBadge.tsx`
- [ ] T012 [US1] Integrate popup and badge into `StudentShellChrome.tsx` in `frontend/src/app/student/shell/StudentShellChrome.tsx`
- [ ] T013 [US1] Create backend API controller endpoint to acknowledge popup `/api/student/acknowledge-tracking-popup` in `backend/src/NaderGorge.API/Controllers/StudentController.cs`

---

## Phase 4: User Story 2 - Mobile App Student Linking and Multi-Student Selection (Priority: P1)

**Goal**: Link student profiles via code on mobile apps and support switching active student

**Independent Test**: Parent inputs code in Android/iOS app, retrieves JWT token and links profile. Can link multiple profiles and toggle between them. Expected result is active child token updates successfully.

- [ ] T014 [US2] Implement `/api/parent/verify-code` backend API handler in `backend/src/NaderGorge.API/Controllers/ParentController.cs`
- [ ] T015 [P] [US2] Write unit tests for verify-code API logic in `backend/tests/NaderGorge.Application.Tests/Parent/VerifyCodeTests.cs`
- [ ] T016 [US2] Implement secure local storage service for JWT tokens list in Android `mobile/parent-android/app/src/main/java/com/nadergorge/parent/data/StorageService.kt`
- [ ] T017 [US2] Implement secure Keychain storage service for JWT tokens list in iOS `mobile/parent-ios/NaderGorgeParent/Services/KeychainService.swift`
- [ ] T018 [US2] Create Jetpack Compose linking view `LinkingScreen.kt` in `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/LinkingScreen.kt`
- [ ] T019 [US2] Create SwiftUI linking view `LinkingView.swift` in `mobile/parent-ios/NaderGorgeParent/Views/LinkingView.swift`

---

## Phase 5: User Story 3 - Academic Tracking Dashboard (Priority: P1)

**Goal**: Load and display detailed student academic stats (grades, attendance, warnings) on mobile apps

**Independent Test**: Select linked student on mobile app, view grades tab, attendance rate, and warning events. Expected result is student profile details load correctly.

- [ ] T020 [US3] Implement `/api/parent/student-details` backend API endpoint with `RequireParent` authorization in `backend/src/NaderGorge.API/Controllers/ParentController.cs`
- [ ] T021 [P] [US3] Write unit tests for student details API logic in `backend/tests/NaderGorge.Application.Tests/Parent/GetDetailsTests.cs`
- [ ] T022 [US3] Implement Dashboard screen with user selector in Android `mobile/parent-android/app/src/main/java/com/nadergorge/parent/ui/screens/DashboardScreen.kt`
- [ ] T023 [US3] Implement Glassmorphic Dashboard view with Liquid Glass layout in iOS `mobile/parent-ios/NaderGorgeParent/Views/DashboardView.swift`
- [ ] T024 [P] [US3] Implement sub-screens for Overview, Attendance, and Exams in both mobile projects

---

## Phase 6: User Story 4 - Push Notifications on Student Events (Priority: P2)

**Goal**: Push real-time Firebase notifications to linked parent devices when student events occur

**Independent Test**: Student completes exam, backend pushes BullMQ job, Node worker sends push notification to parent device via Firebase Admin SDK. Expected result is notification is successfully sent.

- [ ] T025 [P] [US4] Configure Firebase Admin SDK listener in worker `worker/src/jobs/notification-sender.ts`
- [ ] T026 [US4] Write unit tests for worker push notification logic in `worker/tests/jobs/notification-sender.test.ts`
- [ ] T027 [US4] Update backend Event dispatchers to push notification jobs into BullMQ queue upon exam completion, homework submission, or warning creation

---

## Phase 7: User Story 5 - Automated Mobile Build and Verification (Priority: P1)

**Goal**: Automate compilation and unit testing for mobile apps

**Independent Test**: Run Makefile commands, Gradle container builds Android, Swift CLI builds iOS, and both test suites pass. Expected result is build compilation completes successfully.

- [ ] T028 [US5] Add `build-mobile-android`, `build-mobile-ios`, and `build-mobile` tasks to `Makefile`
- [ ] T029 [US5] Write Unit tests for Jetpack Compose ViewModels in `mobile/parent-android/app/src/test/java/com/nadergorge/parent/`
- [ ] T030 [US5] Write Unit tests for SwiftUI ViewModels in `mobile/parent-ios/Tests/NaderGorgeParentTests/`
- [ ] T031 [US5] Configure local unit testing script for CI execution

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Critique fixes, clean code, tests auditing, and validation checks

- [ ] T032 Run deep critique check on all implemented code and verify expected result passes
- [ ] T033 Run `clean-code-guard` against all production files
- [ ] T034 Run `test-guard` against all modified tests
- [ ] T035 Run final feature tests, verify build passes, and run `validate_run.py` verification script
