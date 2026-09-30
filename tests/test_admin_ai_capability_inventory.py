import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BASELINE = ROOT / "tests" / "admin_ai_capability_baseline.json"
SCHEMA = ROOT / "tests" / "admin_ai_capability_manifest.schema.json"


def _canonical_digest(payload):
    copy = dict(payload)
    copy.pop("digest", None)
    return hashlib.sha256(
        json.dumps(copy, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
    ).hexdigest()


def test_baseline_has_closed_manifest_shape_and_deterministic_digest():
    schema = json.loads(SCHEMA.read_text())
    baseline = json.loads(BASELINE.read_text())

    assert schema["properties"]["schemaVersion"]["const"] == baseline["schemaVersion"]
    assert baseline["digest"] == _canonical_digest(baseline)
    assert baseline["activation"] in {"blocked", "reviewed", "active", "superseded"}
    assert baseline["sources"]["semantic"]["digest"] == hashlib.sha256(
        (ROOT / "scripts/generate-admin-ai-capability-baseline.mjs").read_bytes()
    ).hexdigest()


def test_baseline_has_one_disposition_per_item_without_duplicate_id_or_route_method():
    baseline = json.loads(BASELINE.read_text())
    items = baseline["items"]
    ids = [item["id"] for item in items]
    route_methods = [(item["kind"], item["method"], item["route"], item["source"]["file"], item["source"]["line"]) for item in items]

    assert len(ids) == len(set(ids))
    assert len(route_methods) == len(set(route_methods))
    assert all(item["status"] != "excluded" for item in items)
    assert all(item["status"] != "blocked" or item.get("blocker") for item in items)


def test_baseline_uses_only_approved_exclusion_reasons():
    baseline = json.loads(BASELINE.read_text())
    allowed = set(json.loads(SCHEMA.read_text())["$defs"]["exclusion"]["properties"]["reason"]["enum"])

    assert all(exclusion["reason"] in allowed for exclusion in baseline.get("exclusions", []))


def test_frontend_calls_with_exact_backend_routes_share_the_authoritative_operation():
    items = json.loads(BASELINE.read_text())["items"]

    def route_key(item):
        route = re.sub(r"\{[^}]+\}", "{}", item["route"].split("?", 1)[0].lower())
        return item["method"], re.sub(r"^/api(?=/)", "", route)

    backend = {route_key(item): item for item in items if item["kind"] == "backend-endpoint"}
    matched = [item for item in items if item["kind"] == "frontend-call" and route_key(item) in backend]

    assert matched
    assert all(item["authoritativeOperation"] == backend[route_key(item)]["authoritativeOperation"]
               for item in matched)
    for item in matched:
        source = backend[route_key(item)]
        for field in ("effect", "domain", "risk", "confirmation", "refreshScopes"):
            assert item[field] == source[field], (item["route"], field)


def test_unresolved_frontend_deletes_require_strong_confirmation():
    items = json.loads(BASELINE.read_text())["items"]
    deletes = [item for item in items if item["kind"] == "frontend-call"
               and item["method"] == "DELETE" and item["authoritativeOperation"].startswith("unresolved:")]

    assert deletes
    assert all(item["risk"] == "strong" and item["confirmation"] == "strong"
               for item in deletes)


def test_admin_routes_are_inventoried_even_when_controller_name_is_not_admin():
    items = json.loads(BASELINE.read_text())["items"]
    backend = [item for item in items if item["kind"] == "backend-endpoint"]
    assessment = [item for item in backend
                  if item["authoritativeOperation"].startswith("diagnostic:AssessmentReviewController.")]

    assert len(assessment) == 14
    assert all(item["route"].startswith("/api/admin/") for item in assessment)
    assert any(item["authoritativeOperation"] == "diagnostic:AssessmentReviewController.GradeHomework"
               and item["risk"] == "ordinary" for item in assessment)


def test_literal_admin_and_hr_frontend_routes_resolve_to_backend_operations():
    items = json.loads(BASELINE.read_text())["items"]
    calls = [item for item in items if item["kind"] == "frontend-call"
             and item["route"].startswith(("/admin/", "/hr/"))]

    assert calls
    assert all(item["authoritativeOperation"].startswith("diagnostic:") for item in calls)
    subscribers = [item for item in calls if "/subscribers" in item["route"]]
    assert len(subscribers) == 8
    assert len({item["authoritativeOperation"] for item in subscribers}) == 8


def test_watch_request_approval_requires_strong_confirmation_on_both_surfaces():
    items = json.loads(BASELINE.read_text())["items"]
    approvals = [item for item in items
                 if item["method"] == "POST"
                 and re.sub(r"\{[^}]+\}", "{}", item["route"].lower())
                 in {"/api/admin/watch-requests/{}/approve", "/admin/watch-requests/{}/approve"}]

    assert len(approvals) == 2
    assert all(item["risk"] == "strong" and item["confirmation"] == "strong"
               for item in approvals)
    assert all(item["domain"] == "identity" for item in approvals)
