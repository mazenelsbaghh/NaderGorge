# Tasks: E2E Test Flow for Course Content Creation and Access

## Spec Kit Preparation Workflow
- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Arabic Clarification (`speckit-clarify`)
- [x] Phase 3: Technical Planning (`speckit-plan`)
- [x] Phase 4: Detailed Task Breakdown (`speckit-tasks`)

## Technical Tasks

### Phase 1: Setup
- [x] T001 Create the initial test file tests/test_e2e_content_flow.py with standard imports and conftest client references

### Phase 2: Foundational
- [x] T002 Implement database clean/seed fixture setup at the beginning of tests/test_e2e_content_flow.py to ensure isolation

### Phase 3: User Story 1 - Admin Content Management Setup
- [x] T003 [US1] Implement subject creation helper calling POST `/api/admin/subjects` in tests/test_e2e_content_flow.py
- [x] T004 [US1] Implement teacher user creation helper calling POST `/api/admin/users` (role "Teacher") in tests/test_e2e_content_flow.py
- [x] T005 [US1] Implement teacher profile creation helper calling POST `/api/admin/teachers` (associating the subject) in tests/test_e2e_content_flow.py
- [x] T006 [US1] Implement yearly package creation helper calling POST `/api/admin/packages` (target grade "1st Secondary") in tests/test_e2e_content_flow.py
- [x] T007 [US1] Implement Month 1 and Month 2 course (term) creation helpers calling POST `/api/admin/terms` in tests/test_e2e_content_flow.py
- [x] T008 [US1] Implement section and lesson creation helpers calling POST `/api/admin/sections` and POST `/api/admin/lessons` in tests/test_e2e_content_flow.py
- [x] T009 [US1] Implement video creation helper calling POST `/api/admin/videos` in tests/test_e2e_content_flow.py
- [x] T010 [US1] Implement inline exam creation helper calling POST `/api/admin/exams/inline` in tests/test_e2e_content_flow.py
- [x] T011 [US1] Implement homework attachment helper calling POST `/api/admin/content/lessons/{id}/homework` in tests/test_e2e_content_flow.py

### Phase 4: User Story 2 - Student Free Content Access
- [x] T012 [US2] Implement student login and assert that fetching details of a Paid Lesson returns minimal details (hasAccess=false, empty videos/resources list) in tests/test_e2e_content_flow.py
- [x] T013 [US2] Implement lesson-level purchase (Price = 0) calling POST `/api/student/balance/purchase` for the free lesson in tests/test_e2e_content_flow.py
- [x] T014 [US2] Assert that free lesson detail now returns full video details, exam options, and homework questions in tests/test_e2e_content_flow.py

### Phase 5: User Story 3 - Student Paid Content Purchase and Access
- [x] T015 [US3] Implement balance recharge helper calling admin POST `/api/admin/users/students/{userId}/balance/adjust` in tests/test_e2e_content_flow.py
- [x] T016 [US3] Implement package purchase calling POST `/api/student/balance/purchase` for the yearly package in tests/test_e2e_content_flow.py
- [x] T017 [US3] Assert that student balance is correctly deducted and that student now has full access to both Month 1 and Month 2 courses and paid lessons in tests/test_e2e_content_flow.py
- [x] T017b [US3] Fix `TermDetailPageClient.tsx` to correctly display free terms (price = 0) and prevent falling back to the Package price/purchase flow

### Phase 6: Polish & Verification
- [x] T018 Run clean-code-guard on tests/test_e2e_content_flow.py to ensure clean code standards
- [x] T019 Run test-guard on tests/test_e2e_content_flow.py to audit test structure and ensure test-guard guidelines
- [x] T020 Execute final feature tests by running `pytest tests/test_e2e_content_flow.py -v` and verify it passes successfully with expected results

## Dependencies
US1 (Admin Setup) → US2 (Student Free Access) → US3 (Student Paid Purchase)
