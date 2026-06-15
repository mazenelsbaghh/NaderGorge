import pytest
import requests
import os

BASE_URL = os.environ.get("BASE_URL", "http://localhost:5245")
FRONTEND_URL = os.environ.get("FRONTEND_URL", "http://localhost:8738")
E2E_TEST_TOKEN = os.environ.get("E2E_TEST_TOKEN", "E2eOnlyTestTokenValue123456789012345")
INTERNAL_SECRET = os.environ.get("API_CALLBACK_SECRET", "zXcVbNmQweRtyUiOpAsDfGhJkL1234567890ZaSdfGhjKlQwe")


def e2e_headers():
    return {"X-E2E-Token": E2E_TEST_TOKEN}

class NaderGorgeClient:
    def __init__(self, fingerprint="e2e-test-device", device_name="E2E Test Agent"):
        self.session = requests.Session()
        self.fingerprint = fingerprint
        self.device_name = device_name
        self.token = None

    def post(self, path, json=None, headers=None):
        req_headers = {
            "X-Device-Fingerprint": self.fingerprint,
            "X-Device-Name": self.device_name
        }
        if path.startswith("/api/e2e"):
            req_headers.update(e2e_headers())
        if self.token:
            req_headers["Authorization"] = f"Bearer {self.token}"
        if headers:
            req_headers.update(headers)
        return self.session.post(f"{BASE_URL}{path}", json=json, headers=req_headers)

    def get(self, path, params=None, headers=None):
        req_headers = {}
        if path.startswith("/api/e2e"):
            req_headers.update(e2e_headers())
        if self.token:
            req_headers["Authorization"] = f"Bearer {self.token}"
        if headers:
            req_headers.update(headers)
        return self.session.get(f"{BASE_URL}{path}", params=params, headers=req_headers)

    def delete(self, path, headers=None):
        req_headers = {}
        if path.startswith("/api/e2e"):
            req_headers.update(e2e_headers())
        if self.token:
            req_headers["Authorization"] = f"Bearer {self.token}"
        if headers:
            req_headers.update(headers)
        return self.session.delete(f"{BASE_URL}{path}", headers=req_headers)

    def login(self, phone, password, app_surface=None):
        if app_surface is None:
            if phone in ["20000000000", "20000000003", "20000000004"]:
                app_surface = "admin"
            else:
                app_surface = "student"
        res = self.post("/api/auth/login", json={
            "phoneNumber": phone,
            "password": password,
            "deviceFingerprint": self.fingerprint,
            "deviceName": self.device_name
        }, headers={
            "X-App-Surface": app_surface
        })
        if res.status_code == 200:
            data = res.json().get("data", {})
            if data:
                self.token = data.get("accessToken")
        return res

    def login_as(self, phone, password="password", app_surface=None):
        res = self.login(phone, password, app_surface)
        assert res.status_code == 200, f"Login failed for {phone}: {res.text}"
        return res.json().get("data", {}).get("user", {})


def grant_package_to_student(package_id, user_id=None):
    res = requests.post(f"{BASE_URL}/api/e2e/grant-package", json={
        "packageId": package_id,
        "userId": user_id,
    }, headers=e2e_headers())
    assert res.status_code == 200, f"Grant package failed: {res.text}"
    return res.json()

@pytest.fixture(scope="function")
def clean_db():
    # Reset and seed database in E2e mode
    res = requests.post(f"{BASE_URL}/api/e2e/seed", json={
        "clearDatabase": True,
        "seedAdmin": True,
        "seedStudents": True,
        "seedAssistant": True,
        "seedTeacher": True
    }, headers=e2e_headers())
    assert res.status_code == 200
    return res.json()

@pytest.fixture(scope="function")
def mock_package(clean_db):
    # Setup standard package, term, section, lesson, video, exam, and homework
    res = requests.post(f"{BASE_URL}/api/e2e/setup-mock-package", headers=e2e_headers())
    assert res.status_code == 200
    return res.json()
