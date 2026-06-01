# Implementation Plan: Separate Question Bank from Inline Exam Questions

**Branch**: `069-separate-question-bank` | **Date**: 2026-06-02 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/069-separate-question-bank/spec.md)

## Summary

This plan outlines the changes required to isolate the global Question Bank from the inline/added exam questions. We will modify the C# backend query handler to filter out questions marked with `Inline` and `Added` tags. We will also add E2E tests to verify the isolation.

## Proposed Changes

### Backend Component

#### [MODIFY] [AdminQuestionQueries.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Queries/AdminQuestionQueries.cs)
- Update `ListQuestionsQueryHandler` to filter out `QuestionBankItem`s with tags `"Inline"` or `"Added"`.
```csharp
        var query = _db.QuestionBankItems.Include(q => q.Options)
            .Where(q => q.Tags != "Inline" && q.Tags != "Added")
            .AsQueryable();
```

### E2E Test Component

#### [NEW] [test_questions.py](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/tests/test_questions.py)
- Implement `test_question_bank_isolation(mock_package)` which:
  1. Checks initial question bank count.
  2. Creates a global bank question and verifies count increments.
  3. Asserts that the seeded exam's inline question (`"1+1=?"`) is NOT returned in the global bank list.

## Verification Plan

### Automated Tests
- Build backend docker image and run pytest:
  ```bash
  ASPNETCORE_ENVIRONMENT=E2e docker compose up -d --build backend
  ./tests/venv/bin/python3 -m pytest tests/
  ```
