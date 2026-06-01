import pytest
from tests.conftest import NaderGorgeClient

def test_registration_validation(clean_db):
    # Case 1: Less than 4 parts name
    client = NaderGorgeClient()
    payload = {
        "fullName": "أحمد محمد",  # invalid
        "phoneNumber": "01012345678",
        "password": "SecurePassword123",
        "dateOfBirth": "2008-11-20T00:00:00Z",
        "gender": 1,
        "nationality": "مصري",
        "governorate": "القاهرة",
        "address": "مصر الجديدة"
    }
    res = client.post("/api/auth/register", json=payload)
    assert res.status_code == 400

    # Case 2: Invalid Egyptian phone prefix
    payload["fullName"] = "أحمد محمد محمود علي"
    payload["phoneNumber"] = "12345678910"  # invalid
    res = client.post("/api/auth/register", json=payload)
    assert res.status_code == 400

    # Case 3: Valid details
    payload["phoneNumber"] = "01012345678"
    res = client.post("/api/auth/register", json=payload)
    assert res.status_code == 201

def test_device_limits(clean_db):
    # E2e seeds student 2 with phone 20000000002. They have 2 devices already registered
    # Let's test that login fails with a 3rd new fingerprint
    client3 = NaderGorgeClient(fingerprint="dev3")
    res = client3.login("20000000002", "password")
    assert res.status_code == 400 or "limit" in res.text.lower()

    # Log in as Admin to clear student 2 devices
    admin = NaderGorgeClient()
    admin_login = admin.login("20000000000", "password")
    assert admin_login.status_code == 200

    # Let's get student 2's ID. E2e seeds two students. Let's find Student 2 by logging in dev1
    client1 = NaderGorgeClient(fingerprint="e2e-dev1")
    login1 = client1.login("20000000002", "password")
    assert login1.status_code == 200
    user_id = login1.json().get("data", {}).get("user", {}).get("id")
    assert user_id is not None

    # Admin calls DELETE to disconnect all devices for student 2
    del_res = admin.delete(f"/api/admin/users/students/{user_id}/devices")
    assert del_res.status_code == 204

    # Now Student 2 login from dev3 should succeed!
    res_retry = client3.login("20000000002", "password")
    assert res_retry.status_code == 200
