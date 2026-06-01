import pytest
from tests.conftest import NaderGorgeClient

def test_unpurchased_access_blocked(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    exam_id = mock_package.get("examId")
    assert package_id is not None
    assert lesson_id is not None
    assert exam_id is not None

    # Student 1 tries to access unpurchased lesson detail
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code in [400, 403]
    assert "access" in lesson_res.text.lower() or "have" in lesson_res.text.lower()

    # Student 1 tries to start the exam of the unpurchased lesson
    exam_res = student.post(f"/api/exams/{exam_id}/start")
    assert exam_res.status_code in [400, 403]
    assert "access" in exam_res.text.lower() or "have" in exam_res.text.lower()

def test_package_code_purchase_flow(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    exam_id = mock_package.get("examId")

    # Admin generates Package activation code
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    gen_res = admin.post("/api/admin/codes/bulk-generate", json={
        "groupName": "E2E Course Recharge Batch",
        "codeType": "Package",
        "count": 1,
        "codeLength": 8,
        "packageId": package_id
    })
    assert gen_res.status_code == 200
    code = gen_res.json().get("data", {}).get("codes", [])[0]

    # Student 1 logs in and activates
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    act_res = student.post("/api/codes/activate", json={"code": code})
    assert act_res.status_code == 200

    # Student 1 can now access the lesson
    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200

    # Student 1 can start the exam
    exam_res = student.post(f"/api/exams/{exam_id}/start")
    assert exam_res.status_code == 200
    assert exam_res.json().get("data", {}).get("attemptId") is not None

def test_balance_purchase_flow(mock_package):
    package_id = mock_package.get("packageId")
    lesson_id = mock_package.get("lessonId")
    exam_id = mock_package.get("examId")

    # Admin generates Balance recharge code (150 EGP)
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    gen_res = admin.post("/api/admin/codes/bulk-generate", json={
        "groupName": "E2E Wallet Recharge Batch",
        "codeType": "Balance",
        "count": 1,
        "codeLength": 8,
        "balanceAmount": 150.0
    })
    assert gen_res.status_code == 200
    code = gen_res.json().get("data", {}).get("codes", [])[0]

    # Student 1 logs in
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Verify initial balance is 0
    bal_res = student.get("/api/student/balance")
    assert bal_res.status_code == 200
    assert bal_res.json().get("data", {}).get("currentBalance") == 0.0

    # Activate code to recharge balance
    act_res = student.post("/api/codes/activate", json={"code": code})
    assert act_res.status_code == 200

    # Verify balance is 150
    bal_res2 = student.get("/api/student/balance")
    assert bal_res2.status_code == 200
    assert bal_res2.json().get("data", {}).get("currentBalance") == 150.0

    # Buy package using balance
    purchase_res = student.post("/api/student/balance/purchase", json={
        "contentType": "Package",
        "contentId": package_id
    })
    assert purchase_res.status_code == 200

    # Verify balance is deducted to 50
    bal_res3 = student.get("/api/student/balance")
    assert bal_res3.status_code == 200
    assert bal_res3.json().get("data", {}).get("currentBalance") == 50.0

    # Student 1 can now access the lesson
    lesson_res = student.get(f"/api/content/lessons/{lesson_id}")
    assert lesson_res.status_code == 200

    # Student 1 can start the exam
    exam_res = student.post(f"/api/exams/{exam_id}/start")
    assert exam_res.status_code == 200
