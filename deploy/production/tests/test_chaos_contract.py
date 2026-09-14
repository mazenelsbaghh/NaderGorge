from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_chaos_matrix_is_complete_bounded_and_serial() -> None:
    value = json.loads(
        (ROOT / "deploy/production/tests/chaos/scenarios.json").read_text(encoding="utf-8")
    )
    assert value["rules"] == {
        "maximumConcurrentFailedNodes": 1,
        "requireHealthyPreState": True,
        "requireHealthyPostState": True,
        "abortOnSecondFailure": True,
    }
    scenarios = {row["name"]: row for row in value["scenarios"]}
    assert set(scenarios) == {"ingress", "app", "postgres", "redis", "files", "worker", "tunnel"}
    assert all(0 < row["maximumSeconds"] <= 120 for row in scenarios.values())
    assert scenarios["postgres"]["target"] == "current-writer"
    assert scenarios["redis"]["target"] == "current-master"
    assert "HAProxy" in scenarios["ingress"]["failure"]
    assert "tunnel connector" in scenarios["tunnel"]["failure"]
