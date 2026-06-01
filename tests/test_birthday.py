import pytest
import os
import subprocess
from tests.conftest import NaderGorgeClient, BASE_URL

def test_birthday_congratulations_sweep(clean_db):
    # Setup student accounts
    student_client = NaderGorgeClient()
    
    # 1. Register Student A (born Feb 29 - leap year birthday)
    res_a = student_client.post("/api/auth/register", json={
        "fullName": "أحمد محمد محمود علي",
        "phoneNumber": "01099999901",
        "password": "SecurePassword123",
        "dateOfBirth": "2008-02-29T10:00:00Z",
        "gender": 1,
        "nationality": "مصري",
        "governorate": "القاهرة",
        "address": "مصر الجديدة"
    })
    assert res_a.status_code == 201
    user_a_id = res_a.json().get("data", {}).get("userId")
    
    # 2. Register Student B (born March 1st)
    res_b = student_client.post("/api/auth/register", json={
        "fullName": "محمد علي حسن كريم",
        "phoneNumber": "01099999902",
        "password": "SecurePassword123",
        "dateOfBirth": "2008-03-01T10:00:00Z",
        "gender": 1,
        "nationality": "مصري",
        "governorate": "القاهرة",
        "address": "مصر الجديدة"
    })
    assert res_b.status_code == 201
    user_b_id = res_b.json().get("data", {}).get("userId")

    # 3. Register Student C (born June 2nd)
    res_c = student_client.post("/api/auth/register", json={
        "fullName": "حسن محمود أحمد محمد",
        "phoneNumber": "01099999903",
        "password": "SecurePassword123",
        "dateOfBirth": "2008-06-02T10:00:00Z",
        "gender": 1,
        "nationality": "مصري",
        "governorate": "القاهرة",
        "address": "مصر الجديدة"
    })
    assert res_c.status_code == 201
    user_c_id = res_c.json().get("data", {}).get("userId")

    # Run sweep for March 1st on a non-leap year (e.g. 2026)
    # This should congratulate both Student A (Feb 29) and Student B (March 1)
    env = os.environ.copy()
    env["OVERRIDE_DATE"] = "2026-03-01"
    env["DATABASE_URL"] = "postgresql://postgres:postgres@localhost:5435/nadergorge?schema=public"

    worker_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "worker"))
    result = subprocess.run(
        ["npx", "tsx", "src/scripts/birthday-congratulator.ts"],
        env=env,
        capture_output=True,
        text=True,
        cwd=worker_dir
    )
    print("SCRIPT STDOUT:", result.stdout)
    print("SCRIPT STDERR:", result.stderr)
    assert result.returncode == 0
    assert "congratulated" in result.stdout.lower()

    # Query notifications backdoor
    admin = NaderGorgeClient()
    admin.login("20000000000", "password")
    
    notif_res = admin.get("/api/e2e/notifications")
    assert notif_res.status_code == 200
    notifications = notif_res.json()
    print("USER A ID:", user_a_id)
    print("USER B ID:", user_b_id)
    print("NOTIFICATIONS:", notifications)

    # Check that Student A (Feb 29) and Student B (March 1) received notifications
    student_a_notified = any(n.get("userId") == user_a_id and "عيد ميلاد" in n.get("title") for n in notifications)
    student_b_notified = any(n.get("userId") == user_b_id and "عيد ميلاد" in n.get("title") for n in notifications)
    student_c_notified = any(n.get("userId") == user_c_id and "عيد ميلاد" in n.get("title") for n in notifications)

    assert student_a_notified is True
    assert student_b_notified is True
    assert student_c_notified is False

    # Run sweep for June 2nd, which should congratulate Student C
    env_c = os.environ.copy()
    env_c["OVERRIDE_DATE"] = "2026-06-02"
    env_c["DATABASE_URL"] = "postgresql://postgres:postgres@localhost:5435/nadergorge?schema=public"
    
    result_c = subprocess.run(
        ["npx", "tsx", "src/scripts/birthday-congratulator.ts"],
        env=env_c,
        capture_output=True,
        text=True,
        cwd=worker_dir
    )
    assert result_c.returncode == 0

    # Query notifications again
    notif_res2 = admin.get("/api/e2e/notifications")
    assert notif_res2.status_code == 200
    notifications2 = notif_res2.json()

    student_c_notified_now = any(n.get("userId") == user_c_id and "عيد ميلاد" in n.get("title") for n in notifications2)
    assert student_c_notified_now is True
