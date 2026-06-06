import pytest
from concurrent.futures import ThreadPoolExecutor
from tests.conftest import FRONTEND_URL, NaderGorgeClient

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

def test_concurrent_duplicate_package_code_redemption_allows_one_success(mock_package):
    package_id = mock_package.get("packageId")
    assert package_id is not None

    admin = NaderGorgeClient()
    admin.login("20000000000", "password")

    gen_res = admin.post("/api/admin/codes/bulk-generate", json={
        "groupName": "E2E Concurrent Package Batch",
        "codeType": "Package",
        "count": 1,
        "codeLength": 8,
        "packageId": package_id
    })
    assert gen_res.status_code == 200
    code = gen_res.json().get("data", {}).get("codes", [])[0]

    student = NaderGorgeClient(fingerprint="e2e-concurrent-code-login")
    login_res = student.login("20000000001", "password")
    assert login_res.status_code == 200

    students = [NaderGorgeClient(fingerprint=f"e2e-concurrent-code-{i}") for i in range(2)]
    for client in students:
        client.token = student.token

    with ThreadPoolExecutor(max_workers=2) as executor:
        results = list(executor.map(
            lambda client: client.post("/api/codes/activate", json={"code": code}).status_code,
            students,
        ))

    assert results.count(200) == 1
    assert all(status in {200, 400, 404} for status in results)

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

    # Scanned QR code now redirects to a browser-authenticated redemption page.
    qr_url = f"{FRONTEND_URL}/api/qr/{code}"
    res = student.session.get(qr_url, allow_redirects=False)

    assert res.status_code in [302, 307]
    location = res.headers.get("Location", "")
    assert "error" not in location
    assert f"/qr/{code}" in location

    # The API redirect must not activate the code server-side.
    bal_res = student.get("/api/student/balance")
    assert bal_res.status_code == 200
    assert bal_res.json().get("data", {}).get("currentBalance") == 0.0
