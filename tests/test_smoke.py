import requests
from tests.conftest import BASE_URL

def test_health_endpoint():
    res = requests.get(f"{BASE_URL}/api/health")
    assert res.status_code == 200
    assert res.json().get("status").lower() == "healthy"
