import hashlib
import json
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
