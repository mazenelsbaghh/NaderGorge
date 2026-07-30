from __future__ import annotations

import datetime as dt
import importlib.util
import json
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "assemble_capacity_ceiling",
    SCRIPTS / "assemble_capacity_ceiling.py",
)
assert SPEC and SPEC.loader
assembler = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = assembler
SPEC.loader.exec_module(assembler)
capacity_stage = sys.modules["capacity_stage_evidence"]

RELEASE = "git-" + "a" * 40
NOW = dt.datetime(2026, 7, 27, 15, tzinfo=dt.timezone.utc)
BASELINE = 100.0
NODES = ("node-1", "node-2", "node-3")


def write(path: Path, value: dict[str, object]) -> Path:
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


def load_evidence(
    path: Path,
    *,
    excluded_node: str,
    requested_rps: float,
    status: str,
    run_id: str,
) -> Path:
    expected_nodes = [node for node in NODES if node != excluded_node]
    return write(path, {
        "schemaVersion": 1,
        "status": status,
        "runId": run_id,
        "releaseId": RELEASE,
        "capturedAt": "2026-07-27T14:10:00Z",
        "startedAt": "2026-07-27T14:05:00Z",
        "completedAt": "2026-07-27T14:10:00Z",
        "requestedRps": requested_rps,
        "achievedRps": requested_rps,
        "durationSeconds": 300,
        "requestedDuration": "5m",
        "baselineRps": BASELINE,
        "baselineMultiplier": requested_rps / BASELINE,
        "profile": "n-minus-one",
        "excludedNode": excluded_node,
        "capacityStages": [],
        "websocketVus": 2,
        "websocketHoldSuccessRate": 1,
        "workflowRps": 1,
        "workflowProbes": [
            "public-asset-read",
            "protected-asset-read",
            "invalid-upload-validation",
        ],
        "workflowSuccessRate": 1,
        "expectedNodes": expected_nodes,
        "observedNodes": expected_nodes,
        "nodeRequestCounts": {node: requested_rps * 150 for node in expected_nodes},
        "nodeTrafficShares": {node: 0.5 for node in expected_nodes},
        "nodeImbalanceRatio": 0,
        "unexpectedNodeRate": 0,
        "healthyNodeCount": 2,
        "errorRate": 0 if status == "success" else 0.02,
        "checkRate": 1 if status == "success" else 0.98,
        "p95Milliseconds": 300 if status == "success" else 1200,
        "p99Milliseconds": 500 if status == "success" else 2200,
        "surfaceP95Milliseconds": {"landing": 300, "apiLive": 200},
        "surfaceP99Milliseconds": {"landing": 500, "apiLive": 400},
        "droppedIterations": 0,
        "droppedIterationRate": 0,
        "thresholdFailures": [] if status == "success" else [
            "http_req_failed:rate<0.01"
        ],
    })


def capacity_nodes(
    *,
    cpu_busy: float = 40,
    redis_blocked_by_node: dict[str, object] | None = None,
) -> list[dict[str, object]]:
    nodes = []
    for index, node in enumerate(NODES):
        nodes.append({
            "nodeId": node,
            "cpu": {
                "busyPercent": cpu_busy,
                "iowaitPercent": 1,
                "stealPercent": 0,
            },
            "memory": {
                "totalBytes": 1000,
                "availableBytes": 500,
            },
            "rootDisk": {
                "totalBytes": 1000,
                "freeBytes": 500,
            },
            "postgres": {
                "connections": 20,
                "maxConnections": 100,
                "waitingLocks": 0,
                "replicationLagBytes": 0,
            },
            "redis": {
                "used_memory": 20,
                "maxmemory": 100,
                "blocked_clients": (
                    redis_blocked_by_node.get(node, 0)
                    if redis_blocked_by_node is not None
                    else 0
                ),
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
    return nodes


def evaluate_redis_blocked_samples(
    before: dict[str, object],
    during: dict[str, object],
    after: dict[str, object] | None = None,
) -> list[str]:
    started = dt.datetime(2026, 7, 27, 14, 5, tzinfo=dt.timezone.utc)
    completed = dt.datetime(2026, 7, 27, 14, 10, tzinfo=dt.timezone.utc)
    samples = [
        {
            "phase": "before",
            "capturedAt": "2026-07-27T14:04:30Z",
            "nodes": capacity_nodes(redis_blocked_by_node=before),
        },
        {
            "phase": "during",
            "capturedAt": "2026-07-27T14:07:00Z",
            "nodes": capacity_nodes(redis_blocked_by_node=during),
        },
        {
            "phase": "after",
            "capturedAt": "2026-07-27T14:10:20Z",
            "nodes": capacity_nodes(
                redis_blocked_by_node=after if after is not None else during
            ),
        },
    ]
    return capacity_stage.evaluate_samples(
        samples,
        capacity_stage.load_thresholds(),
        started_at=started,
        completed_at=completed,
    )


def test_stable_eighteen_blocking_queue_consumers_pass() -> None:
    values = {"node-1": 0, "node-2": 0, "node-3": 18}
    assert evaluate_redis_blocked_samples(values, values) == []


def test_cluster_total_increase_over_five_fails() -> None:
    violations = evaluate_redis_blocked_samples(
        {"node-1": 0, "node-2": 0, "node-3": 18},
        {"node-1": 0, "node-2": 0, "node-3": 24},
    )
    assert any("redis-blocked-clients-cluster-increase:6.0>5.0" in item for item in violations)


def test_cluster_total_twenty_five_fails_absolute_budget() -> None:
    violations = evaluate_redis_blocked_samples(
        {"node-1": 0, "node-2": 0, "node-3": 18},
        {"node-1": 0, "node-2": 0, "node-3": 25},
    )
    assert any("redis-blocked-clients-cluster-total:25.0>24.0" in item for item in violations)


def test_master_relocation_with_stable_cluster_total_passes() -> None:
    assert evaluate_redis_blocked_samples(
        {"node-1": 0, "node-2": 0, "node-3": 18},
        {"node-1": 0, "node-2": 18, "node-3": 0},
    ) == []


@pytest.mark.parametrize("invalid", [None, "not-a-number"])
def test_missing_or_non_numeric_blocked_clients_fail_closed(invalid: object) -> None:
    violations = evaluate_redis_blocked_samples(
        {"node-1": 0, "node-2": 0, "node-3": 18},
        {"node-1": invalid, "node-2": 0, "node-3": 18},
    )
    assert "missing:node-1:redis-blocked" in violations


def capacity_evidence(
    path: Path,
    *,
    load_path: Path,
    cpu_busy: float = 40,
) -> Path:
    samples = [
        {
            "phase": "before",
            "capturedAt": "2026-07-27T14:04:30Z",
            "nodes": capacity_nodes(cpu_busy=cpu_busy),
        },
        {
            "phase": "during",
            "capturedAt": "2026-07-27T14:07:00Z",
            "nodes": capacity_nodes(cpu_busy=cpu_busy),
        },
        {
            "phase": "after",
            "capturedAt": "2026-07-27T14:10:20Z",
            "nodes": capacity_nodes(cpu_busy=cpu_busy),
        },
    ]
    value = capacity_stage.build_stage_evidence(
        load_path=load_path,
        samples=samples,
        now=dt.datetime(2026, 7, 27, 14, 10, 30, tzinfo=dt.timezone.utc),
    )
    return write(path, value)


def series(
    tmp_path: Path,
    *,
    excluded_node: str,
    passing: list[float],
    failing: float | None,
) -> dict[str, object]:
    stages: list[dict[str, object]] = []
    for requested_rps in passing:
        sequence = len(stages) + 1
        evidence = load_evidence(
            tmp_path / f"{excluded_node}-{sequence}.json",
            excluded_node=excluded_node,
            requested_rps=requested_rps,
            status="success",
            run_id=f"{excluded_node}-stage-{sequence}",
        )
        capacity = capacity_evidence(
            tmp_path / f"{excluded_node}-{sequence}.capacity.json",
            load_path=evidence,
        )
        stages.append({
            "sequence": sequence,
            "requestedRps": requested_rps,
            "evidencePath": evidence.name,
            "capacityEvidencePath": capacity.name,
        })
    if failing is not None:
        sequence = len(stages) + 1
        evidence = load_evidence(
            tmp_path / f"{excluded_node}-{sequence}.json",
            excluded_node=excluded_node,
            requested_rps=failing,
            status="failed",
            run_id=f"{excluded_node}-stage-{sequence}",
        )
        capacity = capacity_evidence(
            tmp_path / f"{excluded_node}-{sequence}.capacity.json",
            load_path=evidence,
        )
        stages.append({
            "sequence": sequence,
            "requestedRps": failing,
            "evidencePath": evidence.name,
            "capacityEvidencePath": capacity.name,
        })
    return {
        "seriesId": f"capacity-{excluded_node}",
        "excludedNode": excluded_node,
        "stages": stages,
    }


def manifest(
    tmp_path: Path,
    rows: list[dict[str, object]] | None = None,
) -> Path:
    return write(tmp_path / "manifest.json", {
        "schemaVersion": 1,
        "releaseId": RELEASE,
        "baselineRps": BASELINE,
        "series": rows or [
            series(
                tmp_path,
                excluded_node="node-1",
                passing=[50, 100, 150],
                failing=200,
            ),
            series(
                tmp_path,
                excluded_node="node-2",
                passing=[50, 100, 125],
                failing=150,
            ),
            series(
                tmp_path,
                excluded_node="node-3",
                passing=[50, 100, 140],
                failing=160,
            ),
        ],
    })


def test_assembles_first_failure_and_sixty_percent_worst_n_minus_one(
    tmp_path: Path,
) -> None:
    output = tmp_path / "capacity-ceiling.json"
    result = assembler.assemble(
        manifest_path=manifest(tmp_path),
        output=output,
        now=NOW,
    )

    assert result["firstFailingNMinusOneRps"] == 150
    assert result["worstPassingNMinusOneRps"] == 125
    assert result["bottleneckExcludedNodes"] == ["node-2"]
    assert result["safeOperatingCeilingFactor"] == 0.6
    assert result["safeOperatingCeilingRps"] == 75
    by_node = {row["excludedNode"]: row for row in result["series"]}
    assert by_node["node-1"]["firstFailingRps"] == 200
    assert by_node["node-2"]["maximumPassingRps"] == 125
    assert all(
        len(stage["evidenceSha256"]) == 64
        for row in result["series"]
        for stage in row["stages"]
    )
    schema = json.loads(
        (
            ROOT
            / "deploy/production/evidence/schemas/capacity-ceiling.schema.json"
        ).read_text(encoding="utf-8")
    )
    assembler.validate(json.loads(output.read_text(encoding="utf-8")), schema)


def test_blocks_series_without_first_failure(tmp_path: Path) -> None:
    rows = [
        series(
            tmp_path,
            excluded_node=node,
            passing=[50, 100],
            failing=None if node == "node-2" else 150,
        )
        for node in NODES
    ]
    with pytest.raises(
        assembler.CapacityAssemblyError,
        match="no first failing stage",
    ):
        assembler.assemble(
            manifest_path=manifest(tmp_path, rows),
            output=tmp_path / "output.json",
            now=NOW,
        )


def test_blocks_evidence_not_bound_to_stage_rps(tmp_path: Path) -> None:
    manifest_path = manifest(tmp_path)
    value = json.loads(manifest_path.read_text(encoding="utf-8"))
    value["series"][0]["stages"][0]["requestedRps"] = 51
    manifest_path.write_text(json.dumps(value), encoding="utf-8")

    with pytest.raises(
        assembler.CapacityAssemblyError,
        match="not exactly bound",
    ):
        assembler.assemble(
            manifest_path=manifest_path,
            output=tmp_path / "output.json",
            now=NOW,
        )


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("loadEvidenceSha256", "0" * 64),
        ("capturedAt", "2026-07-27T14:20:30Z"),
    ],
)
def test_blocks_unbound_or_stale_capacity_evidence(
    tmp_path: Path,
    field: str,
    value: object,
) -> None:
    manifest_path = manifest(tmp_path)
    manifest_value = json.loads(manifest_path.read_text(encoding="utf-8"))
    capacity_path = tmp_path / manifest_value["series"][0]["stages"][0][
        "capacityEvidencePath"
    ]
    capacity_value = json.loads(capacity_path.read_text(encoding="utf-8"))
    capacity_value[field] = value
    capacity_path.write_text(json.dumps(capacity_value), encoding="utf-8")

    with pytest.raises(
        assembler.CapacityAssemblyError,
        match="missing, stale, or not exactly bound",
    ):
        assembler.assemble(
            manifest_path=manifest_path,
            output=tmp_path / "output.json",
            now=NOW,
        )


def test_resource_failure_is_the_first_failing_stage_and_reduces_ceiling(
    tmp_path: Path,
) -> None:
    manifest_path = manifest(tmp_path)
    manifest_value = json.loads(manifest_path.read_text(encoding="utf-8"))
    node_two = manifest_value["series"][1]
    resource_failure_stage = node_two["stages"][2]
    load_path = tmp_path / resource_failure_stage["evidencePath"]
    capacity_path = tmp_path / resource_failure_stage["capacityEvidencePath"]
    capacity_evidence(capacity_path, load_path=load_path, cpu_busy=90)
    node_two["stages"] = node_two["stages"][:3]
    manifest_path.write_text(json.dumps(manifest_value), encoding="utf-8")

    result = assembler.assemble(
        manifest_path=manifest_path,
        output=tmp_path / "output.json",
        now=NOW,
    )

    node_two_result = next(
        row for row in result["series"] if row["excludedNode"] == "node-2"
    )
    assert node_two_result["maximumPassingRps"] == 100
    assert node_two_result["firstFailingRps"] == 125
    assert node_two_result["stages"][-1]["loadStatus"] == "success"
    assert node_two_result["stages"][-1]["capacityStatus"] == "failed"
    assert result["safeOperatingCeilingRps"] == 60


def test_blocks_any_stage_after_first_failure(tmp_path: Path) -> None:
    rows = [
        series(tmp_path, excluded_node=node, passing=[50, 100], failing=150)
        for node in NODES
    ]
    extra_path = load_evidence(
        tmp_path / "node-1-extra.json",
        excluded_node="node-1",
        requested_rps=200,
        status="failed",
        run_id="node-1-extra",
    )
    extra_capacity = capacity_evidence(
        tmp_path / "node-1-extra.capacity.json",
        load_path=extra_path,
    )
    rows[0]["stages"].append({
        "sequence": 4,
        "requestedRps": 200,
        "evidencePath": extra_path.name,
        "capacityEvidencePath": extra_capacity.name,
    })

    with pytest.raises(
        assembler.CapacityAssemblyError,
        match="after its first failure",
    ):
        assembler.assemble(
            manifest_path=manifest(tmp_path, rows),
            output=tmp_path / "output.json",
            now=NOW,
        )
