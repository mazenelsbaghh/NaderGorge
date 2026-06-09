import json
import subprocess
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
INVENTORY_PATH = ROOT / "tests" / "endpoint_inventory.json"


def load_inventory():
    return json.loads(INVENTORY_PATH.read_text())


def test_endpoint_inventory_is_current():
    result = subprocess.run(
        ["node", "scripts/generate-endpoint-inventory.mjs", "--check"],
        cwd=ROOT,
        text=True,
        capture_output=True,
    )

    assert result.returncode == 0, result.stderr or result.stdout


def test_endpoint_inventory_schema_and_paths():
    inventory = load_inventory()

    assert inventory["endpointCount"] == len(inventory["endpoints"])
    assert inventory["frontendCallCount"] == len(inventory["frontendCalls"])
    assert inventory["missingFrontendRouteCount"] == len(
        [finding for finding in inventory["routeFindings"] if finding["kind"] == "missing-backend-route"]
    )
    assert inventory["endpointCount"] > 0
    assert inventory["frontendCallCount"] > 0
    assert inventory["digest"]

    seen = set()
    for endpoint in inventory["endpoints"]:
        key = (endpoint["method"], endpoint["path"], endpoint["controller"], endpoint["action"])
        assert key not in seen
        seen.add(key)

        assert endpoint["method"] in {"GET", "POST", "PUT", "PATCH", "DELETE"}
        assert endpoint["path"].startswith("/api/")
        assert endpoint["controller"].endswith("Controller")
        assert endpoint["action"]
        assert endpoint["authorization"] in {"anonymous", "authorized", "internal-token", "e2e-token"}
        assert endpoint["source"]["file"].startswith("backend/src/NaderGorge.API/Controllers/")
        assert endpoint["source"]["line"] > 0


def test_frontend_calls_have_matching_backend_routes():
    inventory = load_inventory()

    assert inventory["missingFrontendRouteCount"] == 0, json.dumps(
        inventory["routeFindings"],
        ensure_ascii=False,
        indent=2,
    )

    seen = set()
    for call in inventory["frontendCalls"]:
        key = (call["method"], call["path"], call["origin"], call["source"]["file"], call["source"]["line"])
        assert key not in seen
        seen.add(key)

        assert call["method"] in {"GET", "POST", "PUT", "PATCH", "DELETE", "ANY"}
        assert call["path"].startswith("/") or call["path"] == "<dynamic>"
        assert call["origin"] in {"backend-api", "next-api", "worker-api", "external"}
        assert call["source"]["file"].startswith("frontend/src/")
        assert call["source"]["line"] > 0


def test_endpoint_inventory_markdown_includes_frontend_contract_sections():
    markdown = (ROOT / "tests" / "endpoint_inventory.md").read_text()

    assert "## Route Findings" in markdown
    assert "## Frontend API Calls" in markdown
    assert "No missing frontend-called backend routes." in markdown


def test_internal_and_e2e_routes_are_classified_as_protected():
    endpoints = load_inventory()["endpoints"]

    internal_routes = [e for e in endpoints if e["controller"] == "InternalController"]
    assert internal_routes
    assert all(e["authorization"] == "internal-token" for e in internal_routes)

    e2e_routes = [e for e in endpoints if e["controller"] == "E2eTestingController"]
    assert e2e_routes
    assert all(e["authorization"] == "e2e-token" for e in e2e_routes)

    embed_material = next(
        e for e in endpoints
        if e["controller"] == "VideoSessionController" and e["action"] == "GetEmbedMaterial"
    )
    assert embed_material["authorization"] == "internal-token"
