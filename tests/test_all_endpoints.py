import json
import pytest
import re
import requests
import os
from tests.conftest import NaderGorgeClient, BASE_URL, INTERNAL_SECRET, E2E_TEST_TOKEN

def format_path(path):
    parts = re.findall(r'\{([^}]+)\}', path)
    formatted = path
    for part in parts:
        part_lower = part.lower()
        if 'slug' in part_lower:
            formatted = formatted.replace(f"{{{part}}}", "dummy-slug")
        elif 'username' in part_lower:
            formatted = formatted.replace(f"{{{part}}}", "dummy-user")
        elif 'number' in part_lower or 'phone' in part_lower:
            formatted = formatted.replace(f"{{{part}}}", "20000000000")
        else:
            formatted = formatted.replace(f"{{{part}}}", "00000000-0000-0000-0000-000000000000")
    return formatted

def make_request(method, path, token=None, headers=None, json_body=None):
    req_headers = {
        "Content-Type": "application/json",
        "X-Device-Fingerprint": "e2e-test-device",
        "X-Device-Name": "E2E Test Agent"
    }
    if token:
        req_headers["Authorization"] = f"Bearer {token}"
    if headers:
        req_headers.update(headers)
    
    if json_body is None:
        json_body = {}
        
    try:
        return requests.request(method, f"{BASE_URL}{path}", headers=req_headers, json=json_body, timeout=5)
    except Exception as e:
        pytest.fail(f"Request failed for {method} {path}: {str(e)}")

def test_all_endpoints_security_and_isolation(clean_db):
    # Load endpoints from inventory
    inventory_path = os.path.join(os.path.dirname(__file__), "endpoint_inventory.json")
    with open(inventory_path) as f:
        inventory = json.load(f)
    
    endpoints = inventory["endpoints"]
    assert len(endpoints) > 0, "No endpoints found in inventory"
    
    # Setup authenticated clients
    student = NaderGorgeClient()
    res = student.login("20000000001", "password")
    assert res.status_code == 200, "Student login failed"
    
    teacher = NaderGorgeClient()
    res = teacher.login("20000000004", "password")
    assert res.status_code == 200, "Teacher login failed"
    
    assistant = NaderGorgeClient()
    res = assistant.login("20000000003", "password")
    assert res.status_code == 200, "Assistant login failed"
    
    admin = NaderGorgeClient()
    res = admin.login("20000000000", "password")
    assert res.status_code == 200, "Admin login failed"
    
    failures = []
    
    # Controllers that student should never access
    student_forbidden_controllers = {
        "AdminController", "AdminCommunityController", "AdminFinanceController", 
        "AdminFormsController", "AdminHrController", "AdminLessonCommentsController", 
        "AdminMediaController", "AdminOperationsController", "AdminReportsController", 
        "AssistantController", "TeacherController"
    }
    
    # Controllers that teacher should never access (pure admin/finance/operations/assistant)
    teacher_forbidden_controllers = {
        "AdminFinanceController", "AdminHrController", "AdminOperationsController", 
        "AdminReportsController", "AssistantController"
    }
    
    for endpoint in endpoints:
        method = endpoint["method"]
        raw_path = endpoint["path"]
        auth_type = endpoint["authorization"]
        controller = endpoint["controller"]
        action = endpoint["action"]
        
        path = format_path(raw_path)
        
        # 1. Anonymous Access Check
        if auth_type != "anonymous":
            res = make_request(method, path)
            # Should reject unauthenticated requests with 401 or 403
            if res.status_code not in {401, 403}:
                failures.append(
                    f"Anonymous access allowed to protected endpoint: {method} {raw_path} "
                    f"({controller}.{action}), got status {res.status_code}"
                )
        
        # 2. Student Access Segregation Check
        if controller in student_forbidden_controllers and auth_type != "anonymous":
            res = make_request(method, path, token=student.token)
            if res.status_code not in {401, 403}:
                failures.append(
                    f"Student allowed to access forbidden controller: {method} {raw_path} "
                    f"({controller}.{action}), got status {res.status_code}"
                )
                
        # 3. Teacher Access Segregation Check
        # Teachers are allowed to view and resolve tasks on the AssistantController by design,
        # but are blocked from all other assistant-specific actions (like GetMyTasks, etc.)
        is_allowed_teacher_assistant_action = controller == "AssistantController" and action in {"GetPendingTasks", "ResolveTask"}
        if controller in teacher_forbidden_controllers and not is_allowed_teacher_assistant_action and auth_type != "anonymous":
            res = make_request(method, path, token=teacher.token)
            if res.status_code not in {401, 403}:
                failures.append(
                    f"Teacher allowed to access forbidden controller: {method} {raw_path} "
                    f"({controller}.{action}), got status {res.status_code}"
                )

    # Report failures if any
    if failures:
        pytest.fail(f"Vulnerabilities detected: {len(failures)} failures:\n" + "\n".join(failures))
