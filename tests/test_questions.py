import pytest
from tests.conftest import NaderGorgeClient

def test_question_bank_isolation(mock_package):
    # 1. Start Client as Admin
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    # 2. Get current global Question Bank list
    initial_res = admin.get("/api/admin/questions")
    assert initial_res.status_code == 200
    initial_count = initial_res.json().get("data", {}).get("totalCount", 0)

    # Fetch a valid teacher and subject to satisfy validation rules
    teachers_res = admin.get("/api/admin/teachers")
    assert teachers_res.status_code == 200
    valid_teacher_id = teachers_res.json().get("data", [])[0].get("id")

    subjects_res = admin.get("/api/admin/subjects")
    assert subjects_res.status_code == 200
    valid_subject_id = subjects_res.json().get("data", [])[0].get("id")

    # 3. Create a global Question Bank question
    create_res = admin.post("/api/admin/questions", json={
        "text": "What is the capital of Egypt?",
        "type": 0,
        "defaultPoints": 5,
        "tags": "geography",
        "subjectId": valid_subject_id,
        "teacherId": valid_teacher_id,
        "options": [
            {"text": "Cairo", "isCorrect": True},
            {"text": "Alexandria", "isCorrect": False}
        ]
    })
    assert create_res.status_code == 201

    # 4. Get Question Bank list again -> total count should increment by 1
    list_res = admin.get("/api/admin/questions")
    assert list_res.status_code == 200
    new_data = list_res.json().get("data", {})
    new_count = new_data.get("totalCount", 0)
    assert new_count == initial_count + 1

    # 5. Verify the seeded mock package's inline questions (which have tag "Inline" or "Added") ARE NOT present in the bank list
    items = new_data.get("items", [])
    # Inline question seeded in setup-mock-package is "1+1=?"
    assert not any(item.get("text") == "1+1=?" for item in items)
