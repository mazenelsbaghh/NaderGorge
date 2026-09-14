from __future__ import annotations

import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MATRIX_PATH = (
    ROOT / "deploy/production/tests/pre-dns-workflow-matrix.json"
)
MATRIX = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))


def test_pre_dns_matrix_has_exact_surfaces_and_required_workflows() -> None:
    assert MATRIX["schemaVersion"] == 1
    assert MATRIX["surfaces"] == {
        "public": "massar-academy.net",
        "student": "app.massar-academy.net",
        "admin": "admin.massar-academy.net",
        "teacher": "teacher.massar-academy.net",
        "staff": "staff.massar-academy.net",
        "api": "api.massar-academy.net",
        "websocket": "ws.massar-academy.net",
        "assets": "assets.massar-academy.net",
    }
    workflow_ids = {workflow["id"] for workflow in MATRIX["workflows"]}
    assert workflow_ids == {
        "all-eight-host-ingress",
        "public-landing-navigation",
        "student-login-navigation",
        "admin-login-authorized-flow",
        "teacher-login-authorized-flow",
        "staff-login-authorized-flow",
        "api-live-ready-cors-identity",
        "api-authenticated-write-read-cross-node",
        "websocket-authenticated-handshake-reconnect",
        "websocket-cross-node-event",
        "public-asset-read",
        "protected-asset-authorization",
        "valid-upload-cross-node-read-delete",
        "invalid-upload-not-published",
        "wrong-role-and-permission-denial",
        "three-node-distribution-and-drain",
        "single-node-and-data-leader-loss-continuity",
        "external-port-origin-wrong-host-denial",
        "admin-bootstrap-no-stale-material",
    }


def test_every_workflow_names_real_automated_coverage() -> None:
    allowed_keys = {
        "id",
        "surface",
        "description",
        "localAutomated",
        "credentialGated",
        "infrastructureGated",
        "coverage",
    }
    for workflow in MATRIX["workflows"]:
        assert set(workflow) == allowed_keys
        assert workflow["surface"] in {*MATRIX["surfaces"], "all"}
        assert workflow["localAutomated"] is True
        assert workflow["coverage"]
        for coverage in workflow["coverage"]:
            assert set(coverage) == {"path", "marker"}
            source_path = ROOT / coverage["path"]
            assert source_path.is_file(), coverage
            assert coverage["marker"] in source_path.read_text(
                encoding="utf-8"
            ), coverage


def test_local_coverage_is_not_misreported_as_live_acceptance() -> None:
    by_id = {
        workflow["id"]: workflow
        for workflow in MATRIX["workflows"]
    }
    credential_gated = {
        workflow_id
        for workflow_id, workflow in by_id.items()
        if workflow["credentialGated"]
    }
    assert credential_gated == {
        "student-login-navigation",
        "admin-login-authorized-flow",
        "teacher-login-authorized-flow",
        "staff-login-authorized-flow",
        "api-authenticated-write-read-cross-node",
        "websocket-authenticated-handshake-reconnect",
        "websocket-cross-node-event",
        "protected-asset-authorization",
        "valid-upload-cross-node-read-delete",
        "invalid-upload-not-published",
        "single-node-and-data-leader-loss-continuity",
    }
    infrastructure_gated = {
        workflow_id
        for workflow_id, workflow in by_id.items()
        if workflow["infrastructureGated"]
    }
    assert infrastructure_gated == set(by_id) - {
        "wrong-role-and-permission-denial",
        "admin-bootstrap-no-stale-material",
    }


def _service_block(compose: str, service: str, following: str) -> str:
    pattern = rf"(?ms)^  {re.escape(service)}:\n(.*?)(?=^  {re.escape(following)}:\n)"
    match = re.search(pattern, compose)
    assert match, service
    return match.group(1)


def test_each_frontend_container_has_the_intended_surface_identity() -> None:
    compose = (
        ROOT / "deploy/production/compose/compose.app.yml"
    ).read_text(encoding="utf-8")
    expected = (
        ("landing", "student", "landing"),
        ("student", "admin", "student"),
        ("admin", "teacher", "admin"),
        ("teacher", "staff", "teacher"),
        ("staff", "gateway", "assistant"),
    )
    for service, following, surface in expected:
        block = _service_block(compose, service, following)
        assert f"APP_SURFACE: {surface}" in block
        assert f"NEXT_PUBLIC_APP_SURFACE: {surface}" in block


def test_asset_gateway_exposes_only_public_files_with_exact_browser_cors() -> None:
    nginx = (
        ROOT / "deploy/production/config/nginx/massar-node.conf.template"
    ).read_text(encoding="utf-8")
    assets = nginx.split(
        "server_name assets.massar-academy.net;",
        1,
    )[1]
    for denied in ("protected", "private", ".tmp"):
        assert f"location ^~ /{denied}/ {{ return 404; }}" in assets
    assert "try_files $uri =404;" in assets
    assert (
        'Cache-Control "public, max-age=86400, must-revalidate, no-transform" always'
        in assets
    )
    assert 'Access-Control-Allow-Origin "$massar_cors_origin"' in assets
    cors = nginx.split("map $http_origin $massar_cors_origin", 1)[1].split(
        "}",
        1,
    )[0]
    assert "*" not in cors
    for origin in (
        "app",
        "admin",
        "teacher",
        "staff",
    ):
        assert origin in cors
    assert "https://massar-academy.net" in cors


def test_local_role_and_realtime_specs_cover_positive_and_negative_boundaries() -> None:
    auth = (
        ROOT / "frontend/tests/e2e/auth.spec.ts"
    ).read_text(encoding="utf-8")
    staff = (
        ROOT / "frontend/tests/e2e/assistant-dashboard.spec.ts"
    ).read_text(encoding="utf-8")
    realtime = (
        ROOT / "frontend/tests/e2e/signalr-events.spec.ts"
    ).read_text(encoding="utf-8")
    for marker in (
        "Successful login with valid seeded student",
        "Prevent Student from accessing Admin",
        "Prevent Teacher from accessing Admin",
        "Prevent Assistant from accessing Admin",
    ):
        assert marker in auth
    assert "Assistant resolves a pending submission" in staff
    assert "Assistant without crm.manage is blocked" in staff
    assert "student reconnects and rejoins the active lesson group" in realtime
    assert "unmounting a hook does not remove handlers" in realtime
