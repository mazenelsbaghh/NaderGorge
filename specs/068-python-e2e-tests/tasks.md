# Tasks: Python E2E Integration Test Suite

**Input**: Design documents from `/specs/068-python-e2e-tests/`  
**Prerequisites**: plan.md (required), spec.md (required)

---

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) Completed
- [x] Phase 2: Technical Planning (`speckit-plan`) Completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) Completed

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Python test environment initialization

- [x] T001 Create directory `tests/` at the repository root.
- [x] T002 Create `tests/requirements.txt` containing the dependencies:
  ```text
  pytest==8.1.1
  requests==2.31.0
  ```
- [x] T003 Create `tests/conftest.py` with:
  - Base URL configuration (`http://localhost:5245`).
  - `NaderGorgeClient` helper class wrapping `requests.Session`, storing `Authorization` header, injecting device fingerprint headers (`X-Device-Fingerprint`, `X-Device-Name`), and exposing request methods.
  - Pytest autouse fixture `clean_db` that sends a `POST` request to `/api/e2e/seed` resetting and seeding the database.
  - Pytest fixture `mock_package` that calls `/api/e2e/setup-mock-package` and returns the generated entity GUIDs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Smoke testing the environment

- [x] T004 Create a smoke test `tests/test_smoke.py` containing a simple test that hits `/api/health` and asserts HTTP `200` status.
- [x] T005 Run python virtual environment setup and execute the smoke test to verify connectivity to the API:
  ```bash
  python3 -m venv tests/venv
  source tests/venv/bin/activate
  pip install -r tests/requirements.txt
  pytest tests/test_smoke.py
  ```

---

## Phase 3: User Story 1 - Authentication & Device Limits (Priority: P1)

**Goal**: Verify Egyptian name/phone registration rules and device limit locks

- [x] T006 Write tests in `tests/test_auth.py`:
  - **Registration Failure**: Attempt `POST /api/auth/register` with:
    - Name containing less than 4 parts (e.g. "أحمد محمد") -> assert HTTP `400`.
    - Phone number not starting with Egyptian prefixes (e.g., "12345678910") -> assert HTTP `400`.
    - Correct details -> assert HTTP `200`.
  - **Device limit enforcement**:
    - Login Student 2 (`20000000002` / `password`) using fingerprint `dev1` -> assert `200`.
    - Login Student 2 using fingerprint `dev2` -> assert `200`.
    - Login Student 2 using fingerprint `dev3` -> assert HTTP `400` or validation error (device limit exceeded).
  - **Admin override**:
    - Log in as Admin (`20000000000` / `password`).
    - Disconnect device for Student 2 using `DELETE /api/admin/users/students/{userId}/devices/{deviceId}`.
    - Login Student 2 using fingerprint `dev3` -> assert HTTP `200` (succeeds).

---

## Phase 4: User Story 2 - Access Codes & Purchases (Priority: P1)

**Goal**: Verify access codes activation, wallet balance credit, and package buying

- [x] T007 Write tests in `tests/test_codes.py`:
  - **Access Code Redemption**:
    - Call `/api/e2e/setup-mock-package` -> get `PackageId`.
    - Log in as Student 1 (`20000000001` / `password`).
    - Call `POST /api/e2e/grant-package` to grant package to Student 1.
    - Assert `GET /api/student/grants` returns the package.
  - **Wallet Balance Activation**:
    - Login as Student 1.
    - Verify initial balance is 0 via `GET /api/student/balance`.
    - Call `/api/codes/activate` with a generated balance code or mock state -> assert balance updates.
    - Purchase month or package using `POST /api/student/balance/purchase` -> assert balance deducted.

---

## Phase 5: User Story 3 - Playback Session Watch Limits (Priority: P1)

**Goal**: Verify video session keys, watch threshold percentage, lockout limits, and extra view approvals

- [x] T008 Write tests in `tests/test_video.py`:
  - **Play Session Token**:
    - Setup mock package -> get `VideoId`.
    - Call `POST /api/video-session` for `VideoId` -> assert returns encrypted session token and credentials.
  - **Watch progress thresholds**:
    - Call `POST /api/tracking/watch-progress` with watch time under threshold (e.g. 5 seconds for a 100-second video) -> assert view count does not increment.
    - Call progress tracking above threshold -> assert view count increments.
  - **Session Lockout**:
    - Trigger watch progress until watch count > `MaxWatchCount` -> assert subsequent `POST /api/video-session` returns HTTP `400` / locked video payload.
  - **Extra watch request**:
    - Call `POST /api/tracking/watch-requests` -> returns request GUID.
    - Log in as Admin, call `POST /api/admin/watch-requests/{id}/approve` -> assert success.
    - Log in as Student, verify video is unlocked and allows watch.

---

## Phase 6: User Story 4 - Homework & Exam Progression Locks (Priority: P1)

**Goal**: Verify sequential progression locks, automatic exam scoring, and AI callback essay grading

- [x] T009 Write tests in `tests/test_exams.py`:
  - **Sequential Video Lock**:
    - Get lesson detail -> assert Video 2 is locked when Video 1's exam has not been passed.
  - **MCQ Exam Grading**:
    - Student submits MCQ answers via `POST /api/exams/submit` -> fails score -> Video 2 remains locked.
    - Student submits correct MCQ answers -> passes exam -> Video 2 is unlocked.
  - **Essay grading callback**:
    - Student submits exam containing essay questions -> check status is pending grading, Video 2 remains locked.
    - Trigger callback `POST /api/v1/internal/callbacks/essay-graded` with token header `X-Internal-Token` and high score -> assert status updates to Passed, Video 2 unlocks.

---

## Phase 7: User Story 5 - Timeline & Comments Moderation (Priority: P2)

**Goal**: Verify student timeline feed approval states, poll votes, and likes

- [x] T010 Write tests in `tests/test_community.py`:
  - **Timeline posts**:
    - Student creates community post via `POST /api/community/posts` -> status defaults to Pending (does not appear in public feed).
    - Assistant (`20000000003` / `password`) approves post via `/api/admin/community/posts/{id}/approve` -> post status Approved, visible in feed.
  - **Comments moderation**:
    - Student creates lesson comment -> defaults to Pending.
    - Assistant approves comment -> status updates, visible in comment list.
  - **Likes & Polls**:
    - Student likes post -> post likes = 1. Toggles like -> likes = 0.
    - Student votes on a poll option -> count updates. Try to vote again -> blocked.

---

## Phase 8: User Story 6 - Birthday Sweep Verification (Priority: P2)

**Goal**: Verify Egypt Standard Time calculations, leap year March 1st fallback, and notification creations

- [x] T011 Write tests in `tests/test_birthday.py`:
  - **Cairo offset & Birthday sweep**:
    - Seed user with birthday on Cairo local day.
    - Execute birthday congratulator script via python subprocess or mock service call -> assert `notification_events` has birthday notification.
  - **Leap year fallback**:
    - Seed user born on Feb 29th. Trigger script sweep assuming non-leap year March 1st date -> assert user gets birthday notification.

---

## Phase 9: Polish & Running Suite

**Purpose**: Verification sweep of the complete suite

- [x] T012 Run the entire test suite `pytest tests/` and assert all tests pass (returns exit code 0).
- [x] T013 Verify that console outputs and reports are clean, without warnings.
