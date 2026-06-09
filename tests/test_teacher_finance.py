import pytest
from tests.conftest import NaderGorgeClient

def test_student_and_assistant_blocked_from_teacher_finance(clean_db):
    # Student logs in
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Student trying to access teacher finance should get 403 Forbidden
    res = student.get("/api/teacher/finance/account")
    assert res.status_code == 403

    res = student.get("/api/teacher/finance/transactions")
    assert res.status_code == 403

    res = student.get("/api/teacher/finance/payouts")
    assert res.status_code == 403

    res = student.post("/api/teacher/finance/payouts", json={"amount": 100})
    assert res.status_code == 403

    # Assistant logs in
    assistant = NaderGorgeClient()
    assistant.login("20000000003", "password")

    # Assistant trying to access teacher finance should get 403 Forbidden
    res = assistant.get("/api/teacher/finance/account")
    assert res.status_code == 403

def test_teacher_can_access_own_finance(clean_db):
    # Teacher logs in
    teacher = NaderGorgeClient()
    res_login = teacher.login("20000000004", "password")
    assert res_login.status_code == 200

    # Get Account Summary
    res = teacher.get("/api/teacher/finance/account")
    assert res.status_code == 200
    data = res.json().get("data", {})
    assert data.get("currentBalance") == 0.0
    assert data.get("commissionRate") == 0.20
    assert "E2E Teacher" in data.get("teacherName")

    # Get Transactions
    res = teacher.get("/api/teacher/finance/transactions")
    assert res.status_code == 200
    assert res.json().get("data", {}).get("items") == []

    # Get Payouts
    res = teacher.get("/api/teacher/finance/payouts")
    assert res.status_code == 200
    assert res.json().get("data") == []

def test_teacher_request_payout_validations(clean_db):
    teacher = NaderGorgeClient()
    teacher.login("20000000004", "password")

    # Request negative payout
    res = teacher.post("/api/teacher/finance/payouts", json={"amount": -50})
    assert res.status_code == 400
    assert "أكبر من صفر" in res.text

    # Request payout with zero balance
    res = teacher.post("/api/teacher/finance/payouts", json={"amount": 100})
    assert res.status_code == 400
    assert "لا يكفي" in res.text

def test_admin_and_supervisor_only_endpoints(clean_db):
    # Student and Assistant blocked from Admin Finance
    student = NaderGorgeClient()
    student.login("20000000001", "password")
    assert student.get("/api/admin/finance/payroll").status_code == 403

    assistant = NaderGorgeClient()
    assistant.login("20000000003", "password")
    assert assistant.get("/api/admin/finance/payroll").status_code == 403

    # Teacher blocked from Admin Finance
    teacher = NaderGorgeClient()
    teacher.login("20000000004", "password")
    assert teacher.get("/api/admin/finance/payroll").status_code == 403

    # Admin allowed
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    res = admin.get("/api/admin/finance/payroll", params={"month": 6, "year": 2026})
    assert res.status_code == 200

def test_code_activation_credits_commission(mock_package):
    # Admin logs in to generate code for mock package
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    package_id = mock_package.get("packageId")

    gen_res = admin.post("/api/admin/codes/bulk-generate", json={
        "groupName": "Teacher Commission Test Course Batch",
        "codeType": "Package",
        "count": 1,
        "codeLength": 8,
        "packageId": package_id,
        "discountPercentage": 10.0 # 10% discount
    })
    assert gen_res.status_code == 200
    code = gen_res.json().get("data", {}).get("codes", [])[0]

    # Student logs in and redeems code
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    act_res = student.post("/api/codes/activate", json={"code": code})
    assert act_res.status_code == 200

    # Teacher logs in and checks balance
    # Package price is 100, net price = 90, commission = 20% = 18 EGP
    teacher = NaderGorgeClient()
    teacher.login("20000000004", "password")

    acc_res = teacher.get("/api/teacher/finance/account")
    assert acc_res.status_code == 200
    data = acc_res.json().get("data", {})
    assert data.get("currentBalance") == 18.0
    assert data.get("totalEarnings") == 18.0

    # Check transaction ledger contains the code activation
    tx_res = teacher.get("/api/teacher/finance/transactions")
    assert tx_res.status_code == 200
    txs = tx_res.json().get("data", {}).get("items", [])
    assert len(txs) == 1
    assert txs[0].get("price") == 90.0
    assert txs[0].get("commissionRate") == 0.20
    assert txs[0].get("commissionEarned") == 18.0

    # Request a payout of 10 EGP
    req_res = teacher.post("/api/teacher/finance/payouts", json={"amount": 10.0})
    assert req_res.status_code == 200
    payout_id = req_res.json().get("data", {}).get("id")
    assert payout_id is not None

    # Teacher balance is still 18 EGP (since payout is pending)
    acc_res2 = teacher.get("/api/teacher/finance/account")
    assert acc_res2.json().get("data", {}).get("currentBalance") == 18.0

    # Admin resolves payout to Paid
    res_res = admin.post(f"/api/admin/finance/payouts/{payout_id}/resolve", json={
        "status": "Paid"
    })
    assert res_res.status_code == 200

    # Teacher balance is now 8 EGP
    acc_res3 = teacher.get("/api/teacher/finance/account")
    assert acc_res3.json().get("data", {}).get("currentBalance") == 8.0
