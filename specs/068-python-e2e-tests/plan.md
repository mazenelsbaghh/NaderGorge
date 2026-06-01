# Implementation Plan: Python E2E Integration Test Suite

**Branch**: `068-python-e2e-tests` | **Date**: 2026-06-01 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/068-python-e2e-tests/spec.md)
**Input**: Feature specification from `/specs/068-python-e2e-tests/spec.md`

## Summary

This plan outlines the architecture, setup, and implementation of a comprehensive Python-based E2E integration test suite for the Nader Gorge educational platform API. The test suite will be written using `pytest` and `requests` to assert correctness across all major customer and admin workflows by interacting with the backend API running in the E2E sandbox environment.

## Technical Context

**Language/Version**: Python 3.14+  
**Primary Dependencies**: `requests`, `pytest`, `pytest-ordering` (optional, or standard sequential execution)  
**Storage**: Tests execute against PostgreSQL (via `/api/e2e` reset/seed hooks)  
**Testing**: `pytest`  
**Target Platform**: Local development environment or GitHub Actions (targeting backend API at `http://localhost:5245`)  
**Project Type**: Integration / E2E Test Suite  
**Performance Goals**: Executes the entire suite of 15+ tests in under 1 minute  
**Constraints**: Must isolate state by re-seeding the DB before each test module, handle Egyptian verification logic, and mock local time/timezone checks for the birthday script.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Modular Architecture**: Tests are isolated in a `/tests/` root directory to prevent cluttering application source code.
- **Provider Abstraction First**: Evaluates YouTube, Telegram, VK, and Rutube playback payload returns correctly without relying on browser UI.
- **Security & Access Control**: Access control rate limits and token authorization headers are fully tested.

## Project Structure

### Documentation (this feature)

```text
specs/068-python-e2e-tests/
├── plan.md              # This file
├── spec.md              # Feature specification
└── checklists/
    └── requirements.md  # Spec quality checklist
```

### Source Code (repository root)

We will create a new `tests/` directory at the repository root containing:

```text
tests/
├── venv/                     # Python virtual environment (ignored in git)
├── requirements.txt          # Python dependencies (pytest, requests)
├── conftest.py               # Shared API client fixture and DB seed/reset hooks
├── test_auth.py              # Egyptian name/phone validation, device limits
├── test_codes.py             # Access codes, packages, wallet balance, and balance purchases
├── test_video.py             # Playback session key verification, watch progress thresholds, extra views
├── test_exams.py             # MCQ/Essay grading, countdown timers, sequential progression locks
├── test_community.py         # timeline posts, likes, comment approval moderation, poll voting
└── test_birthday.py          # Cairo local time sweep, leap-year March 1st edge-case, notification inserts
```

**Structure Decision**: Option 2: Web application (as we have native `backend/` and `frontend/` folders, placing the python test suite in a top-level `tests/` folder keeps it cross-cutting and easy to run from the root).

## Proposed Implementation Details

### 1. conftest.py Setup
We will construct `NaderGorgeClient` to manage authentication state and automatically inject device fingerprints and bearer tokens:

```python
class NaderGorgeClient:
    def __init__(self, fingerprint="e2e-test-device"):
        self.session = requests.Session()
        self.fingerprint = fingerprint
        self.token = None
        # ... methods for HTTP requests ...
```

A pytest fixture `clean_db` will call `/api/e2e/seed` before each test function to ensure clean state:
```python
@pytest.fixture(autouse=True)
def clean_db():
    requests.post("http://localhost:5245/api/e2e/seed", json={
        "clearDatabase": True,
        "seedAdmin": True,
        "seedStudents": True,
        "seedAssistant": True
    })
```

### 2. Verification of Egyptian Form validation & Device Limits (`test_auth.py`)
- Test registration failure cases (fewer than 4 name parts, incorrect Egyptian phone prefixes).
- Test device limits: Login twice on different fingerprints -> succeeds. Try 3rd fingerprint -> fails. Disconnect device via Admin API -> 3rd succeeds.

### 3. Verification of Access Code & Purchases (`test_codes.py`)
- Setup mock package via `/api/e2e/setup-mock-package`.
- Redeem access codes (balance cards vs package cards).
- Buy packages using student wallet balances and assert debit logic.

### 4. Playback session watch tracking (`test_video.py`)
- Issue playback session tokens via `POST /api/video-session`.
- Track progress using threshold rules: ping watch progress -> verify count increments only when threshold is hit.
- Test lockouts on exceeding limits, sending extra watch requests, and admin approvals resetting counts.

### 5. Progression Locks & AI Essay webhooks (`test_exams.py`)
- Verify subsequent videos/lessons return `isLocked: true` when requirements aren't met.
- Submit MCQ exams -> instantly graded -> unlocks progress.
- Submit Essay exams -> pending grading -> lock remains.
- POST callback to `/api/v1/internal/callbacks/essay-graded` with token -> status updates -> unlocks progress.

### 6. Timeline Moderation (`test_community.py`)
- Write posts/comments -> default to Pending (invisible to other students).
- Admin approves posts/comments -> status Approved -> visible in feeds.
- Polls and Likes increment tests.

### 7. Birthday Sweep (`test_birthday.py`)
- Seed student profiles with specific birthdates.
- Run `birthday-congratulator.ts` script in the worker using subprocess.
- Assert that notifications are inserted and normalization of phone numbers is correct.

## Verification Plan

### Automated Tests
- Command to initialize python virtual environment and run the tests:
  ```bash
  python3 -m venv tests/venv
  source tests/venv/bin/activate
  pip install -r tests/requirements.txt
  pytest tests/
  ```

### Manual Verification
- We can verify that tests are successfully querying endpoints and resetting the Postgres db by running `docker compose logs backend` and seeing request logs matching `api/e2e/seed` and others.
