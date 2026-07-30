from __future__ import annotations

import datetime as dt
import importlib.util
import json
import os
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))


def load(name: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


runner = load("run_live_load")
capacity = sys.modules["capacity_stage_evidence"]


def write(path: Path, value: object) -> Path:
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def plan(path: Path) -> Path:
    return write(path, {
        "schemaVersion": 1,
        "seriesId": "n1-capacity",
        "releaseId": "git-" + "a" * 40,
        "baselineRps": 100,
        "profile": "n-minus-one",
        "excludedNode": "node-1",
        "expectedNodes": ["node-2", "node-3"],
        "stages": [
            {"sequence": 1, "requestedRps": 50, "duration": "5m", "runId": "n1-050"},
            {"sequence": 2, "requestedRps": 100, "duration": "5m", "runId": "n1-100"},
        ],
    })


def healthy_nodes() -> list[dict[str, object]]:
    result = []
    for index, node_id in enumerate(("node-1", "node-2", "node-3")):
        result.append({
            "nodeId": node_id,
            "cpu": {"busyPercent": 40, "iowaitPercent": 1, "stealPercent": 0},
            "memory": {"totalBytes": 1000, "availableBytes": 500},
            "rootDisk": {"totalBytes": 1000, "freeBytes": 500},
            "postgres": {
                "connections": 20,
                "maxConnections": 100,
                "waitingLocks": 0,
                "replicationLagBytes": 0,
            },
            "redis": {
                "used_memory": 20,
                "maxmemory": 100,
                "blocked_clients": 0,
                "aof_delayed_fsync": 0,
            },
            "patroni": {
                "state": "running",
                "role": "master" if index == 0 else "replica",
            },
            "queues": {
                "notifications": {
                    "waiting": 0,
                    "active": 0,
                    "delayed": 0,
                    "failed": 0,
                    "stalled": 0,
                }
            },
        })
    return result


def samples(nodes: list[dict[str, object]]) -> list[dict[str, object]]:
    return [
        {"phase": "before", "capturedAt": "2026-07-27T11:59:30Z", "nodes": nodes},
        {"phase": "during", "capturedAt": "2026-07-27T12:02:00Z", "nodes": nodes},
        {"phase": "after", "capturedAt": "2026-07-27T12:05:20Z", "nodes": nodes},
    ]


def test_runner_is_pinned_hardened_inventory_only_and_bounded() -> None:
    source = (SCRIPTS / "run_live_load.py").read_text(encoding="utf-8")
    for required in (
        "grafana/k6:1.8.0@",
        "sha256:b0982fa7880d4cecc1ab85a89b5f224a1dc88cf406e7999378d8bbe95e4e302b",
        'PLATFORM = "linux/amd64"',
        'ORIGIN = "http://127.0.0.1:8088"',
        "--network\", \"host",
        "--read-only",
        "--cap-drop",
        "no-new-privileges:true",
        "max_bytes=1024 * 1024",
        "StrictSshTransport",
        "collect_snapshot",
        "build_stage_evidence",
    ):
        assert required in source
    assert "docker rm -f" in source
    assert "rm -rf --" in source
    assert "drain" not in source.lower()
    assert "chaos" not in source.lower()


def test_dry_run_validates_but_never_constructs_ssh(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    class ForbiddenTransport:
        def __init__(self, *_args, **_kwargs):
            raise AssertionError("dry-run attempted SSH transport")

    monkeypatch.setattr(runner, "StrictSshTransport", ForbiddenTransport)
    monkeypatch.setattr(sys, "argv", [
        "run_live_load.py",
        "--inventory", str(ROOT / "deploy/production/inventory/production.yml"),
        "--control-node", "node-2",
        "--plan", str(plan(tmp_path / "plan.json")),
        "--evidence-dir", str(tmp_path / "evidence"),
        "--series-output", str(tmp_path / "series.json"),
        "--dry-run",
    ])
    assert runner.main() == 0
    output = json.loads(capsys.readouterr().out)
    assert output["sshExecuted"] is False
    assert output["controlNode"] == "node-2"
    assert output["origin"] == "http://127.0.0.1:8088"


def test_plan_and_environment_are_strict_and_origin_is_not_configurable(
    tmp_path: Path,
) -> None:
    value = runner.validate_plan(plan(tmp_path / "plan.json"))
    environment = runner.build_environment(
        value,
        value["stages"][0],
        websocket_vus=0,
        websocket_hold_ms=10000,
        workflow_rps=0,
        public_asset_path=None,
        protected_asset_path=None,
        upload_probe_path=None,
    )
    assert set(environment) <= runner.ENV_ALLOWLIST
    assert environment["MASSAR_PUBLIC_ORIGIN"] == "http://127.0.0.1:8088"
    assert environment["MASSAR_API_ORIGIN"] == "http://127.0.0.1:8088"
    assert environment["MASSAR_PUBLIC_HOST"] == "massar-academy.net"
    assert environment["MASSAR_API_HOST"] == "api.massar-academy.net"

    invalid = json.loads((tmp_path / "plan.json").read_text(encoding="utf-8"))
    invalid["stages"][1]["requestedRps"] = 50
    write(tmp_path / "invalid.json", invalid)
    with pytest.raises(runner.LiveLoadError, match="increasing"):
        runner.validate_plan(tmp_path / "invalid.json")


def test_auth_tokens_require_exact_0600_and_never_enter_generated_environment(
    tmp_path: Path,
) -> None:
    token = tmp_path / "token"
    token.write_text("opaque-token", encoding="utf-8")
    os.chmod(token, 0o600)
    assert runner.validate_secret_file(token, "token", True) == token.resolve()
    os.chmod(token, 0o640)
    with pytest.raises(runner.LiveLoadError, match="0600"):
        runner.validate_secret_file(token, "token", True)
    assert "MASSAR_WS_ACCESS_TOKEN" not in runner.build_environment(
        runner.validate_plan(plan(tmp_path / "plan.json")),
        runner.validate_plan(tmp_path / "plan.json")["stages"][0],
        websocket_vus=0,
        websocket_hold_ms=10000,
        workflow_rps=0,
        public_asset_path=None,
        protected_asset_path=None,
        upload_probe_path=None,
    )


@pytest.mark.parametrize(
    ("mutation", "expected"),
    [
        (lambda node: node["cpu"].update({"busyPercent": 90}), "cpu-busy"),
        (
            lambda node: node["postgres"].update({"connections": 90}),
            "pg-connection-headroom",
        ),
        (
            lambda node: node["redis"].update({"used_memory": 90}),
            "redis-memory-headroom",
        ),
        (
            lambda node: node["patroni"].update({"state": "stopped"}),
            "patroni-state",
        ),
        (
            lambda node: node["queues"]["notifications"].update({"waiting": 101}),
            "queue:notifications:waiting",
        ),
    ],
)
def test_capacity_stage_fails_on_resource_or_backlog_gate(
    mutation,
    expected: str,
) -> None:
    nodes = healthy_nodes()
    mutation(nodes[1])
    violations = capacity.evaluate_samples(
        samples(nodes),
        capacity.load_thresholds(),
        started_at=dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc),
        completed_at=dt.datetime(2026, 7, 27, 12, 5, tzinfo=dt.timezone.utc),
    )
    assert any(expected in violation for violation in violations)


def test_capacity_stage_rejects_missing_or_stale_during_sample() -> None:
    value = samples(healthy_nodes())
    value[1]["capturedAt"] = "2026-07-27T11:50:00Z"
    violations = capacity.evaluate_samples(
        value,
        capacity.load_thresholds(),
        started_at=dt.datetime(2026, 7, 27, 12, tzinfo=dt.timezone.utc),
        completed_at=dt.datetime(2026, 7, 27, 12, 5, tzinfo=dt.timezone.utc),
    )
    assert "stale:sample-2:during" in violations


def test_live_runner_fails_closed_when_capacity_evidence_fails() -> None:
    source = (SCRIPTS / "run_live_load.py").read_text(encoding="utf-8")
    assert '"capacityStatus": capacity["status"]' in source
    assert '"status": output["status"]' in source
    assert 'return 0 if output["status"] == "success" else 6' in source
