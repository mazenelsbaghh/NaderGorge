import pytest
from tests.conftest import NaderGorgeClient

def test_package_code_redemption(mock_package):
    package_id = mock_package.get("packageId")
    assert package_id is not None

    # Log in as Admin to generate the code
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    gen_res = admin.post("/api/admin/codes/bulk-generate", json={
        "groupName": "E2E Package Batch",
        "codeType": "Package",
        "count": 1,
        "codeLength": 8,
        "packageId": package_id
    })
    assert gen_res.status_code == 200
    codes = gen_res.json().get("data", {}).get("codes", [])
    assert len(codes) == 1
    code = codes[0]

    # Log in as Student 1
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Redeem the code
    act_res = student.post("/api/codes/activate", json={"code": code})
    assert act_res.status_code == 200

    # Redeem again should fail (double redemption)
    act_res_retry = student.post("/api/codes/activate", json={"code": code})
    assert act_res_retry.status_code == 404 or act_res_retry.status_code == 400

def test_balance_code_and_purchase(mock_package):
    # Log in as Admin to generate a balance code
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    gen_res = admin.post("/api/admin/codes/bulk-generate", json={
        "groupName": "E2E Balance Batch",
        "codeType": "Balance",
        "count": 1,
        "codeLength": 8,
        "balanceAmount": 150.0
    })
    assert gen_res.status_code == 200
    code = gen_res.json().get("data", {}).get("codes", [])[0]

    # Log in as Student 1
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Check initial balance is 0
    bal_res = student.get("/api/student/balance")
    assert bal_res.status_code == 200
    assert bal_res.json().get("data", {}).get("currentBalance") == 0.0

    # Redeem balance code
    act_res = student.post("/api/codes/activate", json={"code": code})
    assert act_res.status_code == 200

    # Verify balance is credited
    bal_res2 = student.get("/api/student/balance")
    assert bal_res2.status_code == 200
    assert bal_res2.json().get("data", {}).get("currentBalance") == 150.0

    # Purchase content using balance
    package_id = mock_package.get("packageId")
    purchase_res = student.post("/api/student/balance/purchase", json={
        "contentType": "Package",
        "contentId": package_id
    })
    assert purchase_res.status_code == 200

    # Verify balance is deducted (price of package in e2e setup-mock-package is 100 EGP)
    bal_res3 = student.get("/api/student/balance")
    assert bal_res3.json().get("data", {}).get("currentBalance") == 50.0

def test_qr_code_balance_recharge(clean_db):
    # Log in as Admin to generate a balance code
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    gen_res = admin.post("/api/admin/codes/bulk-generate", json={
        "groupName": "E2E Balance QR Batch",
        "codeType": "Balance",
        "count": 1,
        "codeLength": 8,
        "balanceAmount": 250.0
    })
    assert gen_res.status_code == 200
    code = gen_res.json().get("data", {}).get("codes", [])[0]

    # Log in as Student 1
    student = NaderGorgeClient()
    student.login("20000000001", "password")

    # Set JWT token in cookie for the requests session so Next.js endpoint receives it
    student.session.cookies.set("token", student.token)

    # Scanned QR code hit Next.js route: GET http://localhost:8738/api/qr/{code}
    # We use allow_redirects=False to inspect the 302/307 redirect response
    qr_url = f"http://localhost:8738/api/qr/{code}"
    res = student.session.get(qr_url, allow_redirects=False)
    
    # Assert redirect to /student or similar
    assert res.status_code in [302, 307]
    location = res.headers.get("Location", "")
    assert "error" not in location
    assert "/student" in location

    # Check student balance is now 250.0
    bal_res = student.get("/api/student/balance")
    assert bal_res.status_code == 200
    assert bal_res.json().get("data", {}).get("currentBalance") == 250.0

