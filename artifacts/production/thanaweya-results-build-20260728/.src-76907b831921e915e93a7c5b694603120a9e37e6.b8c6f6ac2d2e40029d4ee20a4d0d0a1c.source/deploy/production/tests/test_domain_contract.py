from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
INVENTORY = json.loads(
    (ROOT / "deploy/production/inventory/production.yml").read_text(encoding="utf-8")
)


def test_exact_domain_surface_contract_is_consistent_everywhere() -> None:
    hosts = INVENTORY["hostnames"]
    assert hosts == {
        "massar-academy.net": "landing",
        "app.massar-academy.net": "student",
        "admin.massar-academy.net": "admin",
        "teacher.massar-academy.net": "teacher",
        "staff.massar-academy.net": "staff",
        "api.massar-academy.net": "api",
        "ws.massar-academy.net": "websocket",
        "assets.massar-academy.net": "assets",
    }
    cloudflared = (
        ROOT / "deploy/production/config/cloudflared/config.yml.tmpl"
    ).read_text(encoding="utf-8")
    nginx = (
        ROOT / "deploy/production/config/nginx/massar-node.conf.template"
    ).read_text(encoding="utf-8")
    for host in hosts:
        assert f"hostname: {host}" in cloudflared
        assert f"server_name {host};" in nginx


def test_cookie_cors_and_canonical_origins_cover_only_browser_surfaces() -> None:
    environment = (
        ROOT / "deploy/production/config/env.production.example"
    ).read_text(encoding="utf-8")
    assert "COOKIE_DOMAIN=.massar-academy.net" in environment
    cors_line = next(line for line in environment.splitlines() if line.startswith("CORS_ALLOWED_ORIGINS="))
    assert "https://massar-academy.net" in cors_line
    assert "https://app.massar-academy.net" in cors_line
    assert "https://admin.massar-academy.net" in cors_line
    assert "https://api.massar-academy.net" not in cors_line
    assert "localhost" not in environment


def test_domain_rehearsal_covers_authenticated_websocket_cookie_and_safe_upload() -> None:
    source = (
        ROOT / "frontend/tests/e2e/production-domain.spec.ts"
    ).read_text(encoding="utf-8")
    assert "/hubs/platform/negotiate?negotiateVersion=1" in source
    assert "new WebSocket(" in source
    assert "ng_refresh=" in source
    assert "multipart:" in source
    assert "rehearsal-invalid.png" in source
    assert "trace: 'off'" in source
