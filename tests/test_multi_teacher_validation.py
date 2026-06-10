import pytest
import uuid
from tests.conftest import NaderGorgeClient

def test_multi_teacher_package_and_question_validation(clean_db):
    # 1. Login as Admin
    admin = NaderGorgeClient()
    admin_login = admin.login("20000000000", "password")
    assert admin_login.status_code == 200, f"Admin login failed: {admin_login.text}"

    # Setup mock package to get existing data (program, subject, etc.)
    setup_res = admin.post("/api/e2e/setup-mock-package")
    assert setup_res.status_code == 200
    mock_data = setup_res.json()
    pkg_id = mock_data.get("packageId")

    # Get subject ID from the created package
    pkg_details_res = admin.get(f"/api/admin/packages/{pkg_id}")
    assert pkg_details_res.status_code == 200
    pkg_data = pkg_details_res.json().get("data", {})
    subject_id = pkg_data.get("subjectId") or pkg_data.get("programId")
    assert subject_id is not None

    # Fetch teachers to get the valid seeded teacher
    teachers_res = admin.get("/api/admin/teachers")
    assert teachers_res.status_code == 200
    teachers = teachers_res.json().get("data", [])
    assert len(teachers) > 0
    valid_teacher_id = teachers[0].get("id")
    assert valid_teacher_id is not None

    # Fetch subject ID (which is linked to program)
    # The SetupMockPackage seeds a subject, let's get it by listing subjects
    subjects_res = admin.get("/api/admin/subjects")
    assert subjects_res.status_code == 200
    subjects = subjects_res.json().get("data", [])
    assert len(subjects) > 0
    valid_subject_id = subjects[0].get("id")
    assert valid_subject_id is not None

    # 2. Create a new teacher user and profile with NO taught subjects
    user_payload = {
        "fullName": "Unassociated Teacher",
        "phoneNumber": "20000000099",
        "password": "password",
        "role": "Teacher"
    }
    user_res = admin.post("/api/admin/users", json=user_payload)
    assert user_res.status_code == 201
    user_id = user_res.json().get("data", {}).get("id")

    teacher_profile_payload = {
        "userId": user_id,
        "bio": "Teaches nothing",
        "specialization": "None",
        "commissionRate": 0.15,
        "contactInfo": "none",
        "subjectIds": []
    }
    teacher_profile_res = admin.post("/api/admin/teachers", json=teacher_profile_payload)
    assert teacher_profile_res.status_code == 201
    unassociated_teacher_id = teacher_profile_res.json().get("data")
    assert unassociated_teacher_id is not None

    # --- Test Package Validation Boundaries ---

    # T1: Fail if subjectId is missing (null/empty)
    res = admin.post("/api/admin/packages", json={
        "name": "Test Package Without Subject",
        "description": "Desc",
        "price": 100,
        "teacherId": valid_teacher_id,
        "targetGrade": "1st Secondary"
    })
    assert res.status_code == 400
    assert "Subject is required" in res.json().get("message", "")

    # T2: Fail if teacherId is missing
    res = admin.post("/api/admin/packages", json={
        "name": "Test Package Without Teacher",
        "description": "Desc",
        "price": 100,
        "subjectId": valid_subject_id,
        "targetGrade": "1st Secondary"
    })
    assert res.status_code == 400
    assert "Teacher is required" in res.json().get("message", "")

    # T3: Fail if teacherId does not exist
    res = admin.post("/api/admin/packages", json={
        "name": "Test Package With Fake Teacher",
        "description": "Desc",
        "price": 100,
        "subjectId": valid_subject_id,
        "targetGrade": "1st Secondary",
        "teacherId": str(uuid.uuid4())
    })
    assert res.status_code == 400
    assert "Selected teacher not found" in res.json().get("message", "")

    # T4: Fail if teacher exists but does not teach subject
    res = admin.post("/api/admin/packages", json={
        "name": "Test Package With Unassociated Teacher",
        "description": "Desc",
        "price": 100,
        "subjectId": valid_subject_id,
        "targetGrade": "1st Secondary",
        "teacherId": unassociated_teacher_id
    })
    assert res.status_code == 400
    assert "Selected teacher does not teach this subject" in res.json().get("message", "")

    # T5: Success when correctly mapped
    res = admin.post("/api/admin/packages", json={
        "name": "Valid Package",
        "description": "Desc",
        "price": 100,
        "subjectId": valid_subject_id,
        "targetGrade": "1st Secondary",
        "teacherId": valid_teacher_id
    })
    assert res.status_code == 201
    assert res.json().get("success") is True

    # --- Test Question Validation Boundaries ---

    # Q1: Fail if subjectId is missing
    res = admin.post("/api/admin/questions", json={
        "text": "1+1=?",
        "defaultPoints": 5,
        "tags": "Math",
        "teacherId": valid_teacher_id,
        "options": [{"text": "2", "isCorrect": True}, {"text": "3", "isCorrect": False}]
    })
    assert res.status_code == 400
    assert "Subject is required" in res.json().get("message", "")

    # Q2: Fail if teacherId is missing
    res = admin.post("/api/admin/questions", json={
        "text": "1+1=?",
        "defaultPoints": 5,
        "tags": "Math",
        "subjectId": valid_subject_id,
        "options": [{"text": "2", "isCorrect": True}, {"text": "3", "isCorrect": False}]
    })
    assert res.status_code == 400
    assert "Teacher is required" in res.json().get("message", "")

    # Q3: Fail if teacherId does not exist
    res = admin.post("/api/admin/questions", json={
        "text": "1+1=?",
        "defaultPoints": 5,
        "tags": "Math",
        "subjectId": valid_subject_id,
        "teacherId": str(uuid.uuid4()),
        "options": [{"text": "2", "isCorrect": True}, {"text": "3", "isCorrect": False}]
    })
    assert res.status_code == 400
    assert "Selected teacher not found" in res.json().get("message", "")

    # Q4: Fail if teacher exists but does not teach subject
    res = admin.post("/api/admin/questions", json={
        "text": "1+1=?",
        "defaultPoints": 5,
        "tags": "Math",
        "subjectId": valid_subject_id,
        "teacherId": unassociated_teacher_id,
        "options": [{"text": "2", "isCorrect": True}, {"text": "3", "isCorrect": False}]
    })
    assert res.status_code == 400
    assert "Selected teacher does not teach this subject" in res.json().get("message", "")

    # Q5: Success when correctly mapped
    res = admin.post("/api/admin/questions", json={
        "text": "Valid Question",
        "defaultPoints": 5,
        "tags": "Math",
        "subjectId": valid_subject_id,
        "teacherId": valid_teacher_id,
        "options": [{"text": "2", "isCorrect": True}, {"text": "3", "isCorrect": False}]
    })
    assert res.status_code == 201  # API returns 201 for question creation success
    assert res.json().get("success") is True
