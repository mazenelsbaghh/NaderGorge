import pytest
from tests.conftest import NaderGorgeClient

def test_assistant_endpoints_role_protection(clean_db):
    # 1. Login as Student and verify 403 Forbidden on assistant tasks endpoints
    student = NaderGorgeClient(fingerprint="e2e-dev1")
    student_login = student.login("20000000002", "password")
    assert student_login.status_code == 200, f"Student login failed: {student_login.text}"

    student_res = student.get("/api/v1/assistant/tasks/my")
    # Expected: 403 Forbidden
    assert student_res.status_code == 403, f"Expected 403 for student on assistant endpoint, got {student_res.status_code}"

    # 2. Login as Teacher and verify 403 Forbidden on assistant tasks endpoints
    teacher = NaderGorgeClient()
    teacher_login = teacher.login("20000000001", "password")
    assert teacher_login.status_code == 200, f"Teacher login failed: {teacher_login.text}"

    teacher_res = teacher.get("/api/v1/assistant/tasks/my")
    # Expected: 403 Forbidden
    assert teacher_res.status_code == 403, f"Expected 403 for teacher on assistant endpoint, got {teacher_res.status_code}"

    # 3. Login as Assistant and verify 200 OK
    assistant = NaderGorgeClient()
    assistant_login = assistant.login("20000000003", "password")
    assert assistant_login.status_code == 200, f"Assistant login failed: {assistant_login.text}"

    assistant_res = assistant.get("/api/v1/assistant/tasks/my")
    assert assistant_res.status_code == 200, f"Expected 200 for assistant, got {assistant_res.status_code}: {assistant_res.text}"
