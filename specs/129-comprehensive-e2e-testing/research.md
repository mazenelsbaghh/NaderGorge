# Technical Research: E2E and API Test Catalog

## 1. Playwright E2E Test Suite (Frontend-driven)

Located at `frontend/tests/e2e/`. These tests simulate user actions in Chromium against the Next.js frontend (port 3000) and verify that backend API requests perform correctly.

| Test File | Focus Area / Target Endpoints Covered |
|---|---|
| `auth.spec.ts` | `/api/auth/login`, `/api/auth/register`, `/api/auth/complete-profile`, device fingerprinting |
| `admin-users.spec.ts` | `/api/admin/users`, `/api/admin/users/{id}/status`, `/api/admin/users/{id}/roles` |
| `admin-content.spec.ts` | `/api/admin/packages`, `/api/admin/terms`, `/api/admin/sections`, `/api/admin/lessons`, `/api/admin/videos` |
| `assistant-dashboard.spec.ts` | `/api/v1/assistant/tasks/pending`, `/api/v1/assistant/tasks/{taskid}/resolve`, task resolution |
| `codes-wallet.spec.ts` | `/api/student/balance`, `/api/student/balance/purchase`, package access grants |
| `codes.spec.ts` | `/api/codes/activate`, `/api/codes/validate/{code}` |
| `comprehensive-features.spec.ts` | Student dashboard, theme preferences, student notifications read status |
| `homework-system.spec.ts` | `/api/homework/pending`, `/api/homework/{id}/submit`, essay submissions and grading |
| `package-code-profiles.spec.ts` | `/api/admin/packages/{id}/code-profile` (GET, PUT, DELETE) |
| `parent-report.spec.ts` | `/api/parent/reports/{studentid}/summary`, `/api/parent/reports/{studentid}/links` |
| `signalr-events.spec.ts` | SignalR Hub connection, real-time exam timers, and live notifications |
| `student-academic.spec.ts` | `/api/student/dashboard`, `/api/student/progress`, `/api/student/profile` |
| `student-journey.spec.ts` | Integrated end-to-end flow: login -> package view -> lesson -> exam start -> exam submit |
| `teacher-isolation.spec.ts` | `/api/teacher/dashboard/stats`, `/api/teacher/finance/account`, `/api/teacher/profile` |

---

## 2. Python API Test Suite (Backend-driven)

Located at `tests/`. These tests directly invoke backend endpoints to verify security, performance, and functionality.

| Test File | Focus Area / Target Endpoints Covered |
|---|---|
| `test_all_endpoints.py` | Automatically tests **all 255 API endpoints** from `endpoint_inventory.json` under Anonymous, Student, Teacher, and Admin roles to verify role segregation. |
| `test_auth.py` | Direct API authentication endpoints. |
| `test_codes.py` | Direct code activation and balance verification APIs. |
| `test_purchases.py` | Package purchase and granular balance deductions. |
| `test_video.py` | Video session tracking, watched progress, and extra watch request approvals. |
| `test_exams.py` | Exam start, swap question, fifty-fifty lifeline, and submission grading. |
| `test_homework_flows.py` | Homework submissions and pending homework endpoints. |
| `test_community.py` | Community post creation, post liking, and post comments moderation. |
| `test_assistant_permissions.py` | Assistant role-specific route gates and task resolution boundaries. |
| `test_multi_teacher_validation.py` | Multi-teacher registration, subjects binding, and isolated dashboard stats. |
| `test_birthday.py` | Student birthday greeting logic, locked videos, and exam locks. |
| `test_operations_tasks.py` | Operations task management APIs and transitions. |
| `test_audit_and_reports.py` | System audit logs, report generation, and admin dashboard KPI stats. |
