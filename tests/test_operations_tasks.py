import pytest
from tests.conftest import NaderGorgeClient

def test_operations_tasks_lifecycle(clean_db):
    # 1. Login Admin and Assistant, retrieve User IDs dynamically
    admin = NaderGorgeClient()
    admin_login = admin.login("20000000000", "password")
    assert admin_login.status_code == 200, f"Admin login failed: {admin_login.text}"
    admin_id = admin_login.json().get("data", {}).get("user", {}).get("id")
    assert admin_id is not None

    assistant = NaderGorgeClient()
    assist_login = assistant.login("20000000003", "password")
    assert assist_login.status_code == 200, f"Assistant login failed: {assist_login.text}"
    assistant_id = assist_login.json().get("data", {}).get("user", {}).get("id")
    assert assistant_id is not None

    # 2. Admin creates a task assigned to the Assistant
    task_payload = {
        "title": "Comprehensive Task Testing",
        "description": "Verify the full operations task lifecycle via API",
        "assigneeId": assistant_id,
        "priority": 3, # High
        "dueDate": "2026-07-09T12:00:00Z"
    }

    create_res = admin.post("/api/admin/operations/tasks", json=task_payload)
    assert create_res.status_code == 200, f"Task creation failed: {create_res.text}"
    create_data = create_res.json()
    assert create_data.get("success") is True, f"Response was not success: {create_data}"
    task_id = create_data.get("data")
    assert task_id is not None

    # 3. Admin lists tasks and verifies filtering works
    list_res = admin.get("/api/admin/operations/tasks", params={
        "search": "Comprehensive",
        "priority": "High",
        "status": "New"
    })
    assert list_res.status_code == 200, f"Listing tasks failed: {list_res.text}"
    tasks_data = list_res.json().get("data", [])
    assert len(tasks_data) > 0, "No tasks returned with search filters"
    
    # Locate our created task
    our_task = next((t for t in tasks_data if t["id"] == task_id), None)
    assert our_task is not None
    assert our_task["title"] == "Comprehensive Task Testing"
    assert our_task["status"] == "New"

    # 4. Assistant lists their tasks and verifies the task is there
    my_tasks_res = assistant.get("/api/v1/assistant/tasks/my")
    assert my_tasks_res.status_code == 200, f"Assistant get my tasks failed: {my_tasks_res.text}"
    my_tasks = my_tasks_res.json().get("data", [])
    
    # Locate task in my tasks
    my_our_task = next((t for t in my_tasks if t["id"] == task_id), None)
    assert my_our_task is not None
    assert my_our_task["title"] == "Comprehensive Task Testing"

    # 5. Assistant retrieves task details
    details_res = assistant.get(f"/api/v1/assistant/tasks/my/{task_id}")
    assert details_res.status_code == 200, f"Get task details failed: {details_res.text}"
    details_data = details_res.json().get("data", {})
    assert details_data.get("task") is not None
    assert details_data.get("task")["id"] == task_id
    assert len(details_data.get("comments", [])) == 0

    # 6. Assistant adds a comment to the task
    comment_payload = {
        "content": "Starting work on this task now.",
        "attachmentUrl": None
    }
    comment_res = assistant.post(f"/api/v1/assistant/tasks/my/{task_id}/comments", json=comment_payload)
    assert comment_res.status_code == 200, f"Adding comment failed: {comment_res.text}"
    assert comment_res.json().get("success") is True

    # 7. Assistant updates status to InProgress
    status_payload_in_progress = {"status": "InProgress"}
    status_res = assistant.post(f"/api/v1/assistant/tasks/my/{task_id}/status", json=status_payload_in_progress)
    assert status_res.status_code == 200, f"Updating status to InProgress failed: {status_res.text}"

    # 8. Assistant updates status to Review
    status_payload_review = {"status": "Review"}
    status_res2 = assistant.post(f"/api/v1/assistant/tasks/my/{task_id}/status", json=status_payload_review)
    assert status_res2.status_code == 200, f"Updating status to Review failed: {status_res2.text}"

    # Verify status is now Review and comment is present in details
    details_res2 = assistant.get(f"/api/v1/assistant/tasks/my/{task_id}")
    assert details_res2.status_code == 200
    details_data2 = details_res2.json().get("data", {})
    assert details_data2.get("task")["status"] == "Review"
    assert len(details_data2.get("comments", [])) == 1
    assert details_data2.get("comments", [])[0]["content"] == "Starting work on this task now."

    # 9. Admin resolves (approves) the task
    resolve_payload = {
        "approve": True,
        "rejectionReason": None
    }
    resolve_res = admin.post(f"/api/admin/operations/tasks/{task_id}/resolve", json=resolve_payload)
    assert resolve_res.status_code == 200, f"Admin resolve failed: {resolve_res.text}"
    assert resolve_res.json().get("success") is True

    # 10. Verify task is now Completed
    details_res3 = assistant.get(f"/api/v1/assistant/tasks/my/{task_id}")
    assert details_res3.status_code == 200
    assert details_res3.json().get("data", {}).get("task")["status"] == "Completed"
