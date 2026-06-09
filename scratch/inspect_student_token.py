import requests
import json

# Login Student
res_student = requests.post("http://localhost:5245/api/auth/login", json={
    "phoneNumber": "20000000002",
    "password": "password",
    "deviceFingerprint": "e2e-dev1",
    "deviceName": "Python Inspector"
}, headers={
    "X-App-Surface": "student"
})

token_student = res_student.json()["data"]["accessToken"]

# Call /api/v1/assistant/tasks/my as Student
res_my = requests.get("http://localhost:5245/api/v1/assistant/tasks/my", headers={"Authorization": f"Bearer {token_student}"})
print("STUDENT GET STATUS:", res_my.status_code)
print("STUDENT GET HEADERS:", dict(res_my.headers))
print("STUDENT GET RESPONSE:", res_my.text)
