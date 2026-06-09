import pytest
from tests.conftest import NaderGorgeClient

def test_reports_authorization(clean_db):
    # Case 1: Student is blocked (403)
    student = NaderGorgeClient()
    student.login("20000000001", "password")
    
    res = student.get("/api/admin/reports/audit")
    assert res.status_code == 403
    
    res = student.get("/api/admin/reports/kpi")
    assert res.status_code == 403

    # Case 2: Assistant is blocked (403)
    assistant = NaderGorgeClient()
    assistant.login("20000000003", "password")
    
    res = assistant.get("/api/admin/reports/audit")
    assert res.status_code == 403
    
    res = assistant.get("/api/admin/reports/kpi")
    assert res.status_code == 403

    # Case 3: Teacher is blocked (403)
    teacher = NaderGorgeClient()
    teacher.login("20000000004", "password")
    
    res = teacher.get("/api/admin/reports/audit")
    assert res.status_code == 403
    
    res = teacher.get("/api/admin/reports/kpi")
    assert res.status_code == 403

    # Case 4: Admin is allowed (200)
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    
    res = admin.get("/api/admin/reports/audit")
    assert res.status_code == 200
    
    res = admin.get("/api/admin/reports/kpi")
    assert res.status_code == 200


def test_audit_logs_query_parameters(clean_db):
    # Retrieve Assistant User ID dynamically
    assistant = NaderGorgeClient()
    assist_login = assistant.login("20000000003", "password")
    assert assist_login.status_code == 200
    assistant_id = assist_login.json().get("data", {}).get("user", {}).get("id")

    # Login Admin and retrieve Admin User ID dynamically
    admin = NaderGorgeClient()
    admin_login = admin.login("20000000000", "password")
    assert admin_login.status_code == 200
    admin_id = admin_login.json().get("data", {}).get("user", {}).get("id")

    # Perform a logged action (e.g. create a task)
    task_payload = {
        "title": "E2e Audit Verification Task",
        "description": "Verify task creation audit log writes",
        "assigneeId": assistant_id,
        "priority": 1,
        "dueDate": "2026-07-09T00:00:00Z",
        "createdById": admin_id
    }
    
    # We call operations controller to create a task
    res_task = admin.post("/api/admin/operations/tasks", json=task_payload)
    assert res_task.status_code == 200

    # Query audit logs
    res = admin.get("/api/admin/reports/audit", params={"entityType": "TaskItem"})
    assert res.status_code == 200
    
    data = res.json().get("data", {})
    assert "items" in data
    assert data["totalCount"] >= 1
    
    latest_log = data["items"][0]
    assert latest_log["action"] == "CreateTask"
    assert latest_log["entityType"] == "TaskItem"
    assert "E2e Audit Verification Task" in latest_log["newValues"]
    assert latest_log["performedByUserId"] == admin_id


def test_kpi_dashboard_structure(clean_db):
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    res = admin.get("/api/admin/reports/kpi")
    assert res.status_code == 200
    
    kpis = res.json().get("data", {})
    assert "attendance" in kpis
    assert "tasks" in kpis
    assert "crmOutcomes" in kpis
    assert "media" in kpis
    assert "payments" in kpis
    assert "payrollStatus" in kpis

    # Verify structured elements
    attendance = kpis["attendance"]
    assert "presentRate" in attendance
    assert "lateRate" in attendance
    assert "absentRate" in attendance

    tasks = kpis["tasks"]
    assert "completionRate" in tasks
    assert "totalTasks" in tasks

    payments = kpis["payments"]
    assert "autoMatchRate" in payments
    assert "totalTransactions" in payments
