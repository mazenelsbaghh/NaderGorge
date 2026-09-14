from __future__ import annotations

import json
import os
import socket
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
INVENTORY = json.loads(
    (ROOT / "deploy/production/inventory/production.yml").read_text(encoding="utf-8")
)
INTERNAL_PORTS = (80, 443, 2379, 2380, 5432, 6379, 8008, 8080, 8088, 24007, 26379)


def can_connect(host: str, port: int) -> bool:
    try:
        with socket.create_connection((host, port), timeout=2):
            return True
    except OSError:
        return False


def test_public_origins_expose_only_reviewed_ssh() -> None:
    if os.environ.get("MASSAR_RUN_OUTSIDE_IN_SCAN") != "1":
        pytest.skip("set MASSAR_RUN_OUTSIDE_IN_SCAN=1 from an external runner")
    findings = []
    for node in INVENTORY["nodes"]:
        for port in INTERNAL_PORTS:
            if can_connect(node["public_address"], port):
                findings.append(f"{node['id']}:{port}")
        assert can_connect(node["public_address"], 22)
    assert findings == []
