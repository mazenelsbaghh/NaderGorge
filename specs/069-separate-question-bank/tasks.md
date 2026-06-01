# Tasks: Separate Question Bank from Inline Exam Questions

**Input**: Design documents from `/specs/069-separate-question-bank/`  
**Prerequisites**: plan.md (required), spec.md (required)

---

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) Completed
- [x] Phase 2: Technical Planning (`speckit-plan`) Completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) Completed

---

## Phase 1: Backend Implementation

**Goal**: Exclude inline/added questions from the global question list query.

- [ ] T001 Open `backend/src/NaderGorge.Application/Features/Admin/Queries/AdminQuestionQueries.cs`.
- [ ] T002 Modify `ListQuestionsQueryHandler.Handle` to filter out questions with `"Inline"` and `"Added"` tags from `_db.QuestionBankItems`:
  ```csharp
  var query = _db.QuestionBankItems.Include(q => q.Options)
      .Where(q => q.Tags != "Inline" && q.Tags != "Added")
      .AsQueryable();
  ```

---

## Phase 2: E2E Integration Testing

**Goal**: Implement E2E test verifying isolation of global questions.

- [ ] T003 Create a new test file `tests/test_questions.py`.
- [ ] T004 Implement `test_question_bank_isolation(mock_package)` inside `tests/test_questions.py`:
  - Login as Admin.
  - Query initial Question Bank list (`GET /api/admin/questions`) and read `totalCount`.
  - Create a new global question via `POST /api/admin/questions`.
  - Query Question Bank list again and assert `totalCount` has incremented by 1.
  - Verify that the seeded inline questions (e.g., `"1+1=?"`) from the mock package setup are NOT present in the returned list.

---

## Phase 3: Build & Verification

**Goal**: Compile and run the test suite.

- [ ] T005 Rebuild the backend container in E2e mode:
  ```bash
  ASPNETCORE_ENVIRONMENT=E2e docker compose up -d --build backend
  ```
- [ ] T006 Run the pytest suite:
  ```bash
  ./tests/venv/bin/python3 -m pytest tests/
  ```
