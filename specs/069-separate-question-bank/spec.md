# Feature Specification: Complete Separation of Question Bank from Inline Exam Questions

**Feature Branch**: `069-separate-question-bank`  
**Created**: 2026-06-02  
**Status**: Draft  
**Input**: User request: "عايز افصل كل الفصل بين بنك الاسئله ده حاجه و الاسئلبه بتاعر الفيديو و بتاعت الحصهع حاجه تانيه خالص بنك اسئبه وجه ه تنيه و حاجه تانيه"

## User Scenarios & Testing

### User Story 1 - Question Bank Listing & Isolation (Priority: P1)

As an Administrator, I want the global Question Bank (`/admin/questions`) to display only the questions explicitly created within the Question Bank interface, excluding any inline questions created directly inside lesson or video exams, so that the global question bank remains organized and uncluttered.

**Acceptance Scenarios**:
1. **Given** an exam containing inline questions (marked with tag `Inline` or `Added`), **When** the admin lists questions on the Question Bank page, **Then** none of the inline exam questions are returned in the list.
2. **Given** a globally created question in the Question Bank, **When** the admin lists questions on the Question Bank page, **Then** the global question is returned in the list.
3. **Given** a search query on the Question Bank page, **When** the admin searches, **Then** only matching global Question Bank questions are searched, leaving inline questions untouched.

---

## Edge Cases
- **Tag Substrings**: A user-defined tag containing "Inline" (e.g., "InlineCheck") must not accidentally hide a global bank question unless it is exactly the system-assigned inline identifiers. We must check for exact match `Inline` and `Added` tags.

---

## Requirements

### Functional Requirements
- **FR-001**: The global Question Bank query handler (`ListQuestionsQueryHandler` in `AdminQuestionQueries.cs`) MUST filter out all `QuestionBankItem`s where the `Tags` field contains or equals `"Inline"` or `"Added"`.
- **FR-002**: The global Question Bank admin page `/admin/questions` must list only the filtered global question bank items.
- **FR-003**: The E2E integration test suite MUST contain a test checking that inline questions from mock packages are successfully excluded from the returned Question Bank list.

---

## Success Criteria

### Measurable Outcomes
- **SC-001**: `GET /api/admin/questions` returns zero items when only inline/added exam questions exist in the database.
- **SC-002**: Rebuilding the backend and running the pytest test suite results in all tests passing.
