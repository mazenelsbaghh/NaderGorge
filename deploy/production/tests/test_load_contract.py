from __future__ import annotations

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
LOAD_SCRIPT = ROOT / "deploy/production/tests/load/cluster-load.js"
WORKFLOW_SCRIPT = ROOT / "deploy/production/load/platform-workflows.js"
LOAD_SCHEMA = ROOT / "deploy/production/evidence/schemas/load.schema.json"
PERFORMANCE_MATRIX = ROOT / "deploy/production/config/performance-matrix.json"


def test_load_script_is_fail_closed_and_never_defaults_to_production() -> None:
    source = LOAD_SCRIPT.read_text(encoding="utf-8")
    for required in (
        "MASSAR_LOAD_AUTHORIZED",
        "MASSAR_PUBLIC_ORIGIN",
        "MASSAR_API_ORIGIN",
        "MASSAR_RELEASE_ID",
        "MASSAR_LOAD_RUN_ID",
        "MASSAR_LOAD_EVIDENCE_PATH",
        "MASSAR_EXPECTED_NODES",
        "MASSAR_BASELINE_RPS",
    ):
        assert required in source
    assert "https://massar-academy.net" not in source
    assert "https://api.massar-academy.net" not in source
    assert "[evidencePath]" in source


def test_load_script_binds_results_to_release_nodes_and_threshold_outcomes() -> None:
    source = LOAD_SCRIPT.read_text(encoding="utf-8")
    for evidence_field in (
        "releaseId",
        "runId",
        "capturedAt",
        "durationSeconds",
        "baselineMultiplier",
        "errorRate",
        "p95Milliseconds",
        "p99Milliseconds",
        "droppedIterationRate",
        "expectedNodes",
        "observedNodes",
        "healthyNodeCount",
        "thresholdFailures",
        "nodeRequestCounts",
        "nodeTrafficShares",
        "nodeImbalanceRatio",
        "unexpectedNodeRate",
        "surfaceP95Milliseconds",
        "surfaceP99Milliseconds",
    ):
        assert evidence_field in source
    assert "massar_release_mismatch" in source
    assert "massar_node_hits{node:" in source
    assert "dropped_iterations" in source
    assert "result.ok" in source
    assert "massar_unexpected_node" in source
    assert "node-balance:" in source


def test_progressive_capacity_and_all_three_n_minus_one_runs_are_defined() -> None:
    source = LOAD_SCRIPT.read_text(encoding="utf-8")
    assert "ramping-arrival-rate" not in source
    assert "capacity stages must be separate constant-rate runs with separate evidence" in source
    assert "MASSAR_EXCLUDED_NODE" in source
    assert "n-minus-one requires one excluded node and exactly the other two expected nodes" in source

    matrix = json.loads(
        PERFORMANCE_MATRIX.read_text(encoding="utf-8")
    )
    runs = {row["name"]: row for row in matrix["runs"]}
    for node in ("node-1", "node-2", "node-3"):
        row = runs[f"n-minus-one-{node}"]
        assert row["excludedNode"] == node
        assert set(row["expectedNodes"]) == {"node-1", "node-2", "node-3"} - {node}
        assert row["workflowProbesRequired"] is True
        capacity = runs[f"n-minus-one-capacity-{node}"]
        assert capacity["execution"].endswith("sequential-separate-k6-evidence")
        assert capacity["stopAfterFirstFailure"] is True
        assert capacity["separateEvidencePerStage"] is True
        assert [stage["sequence"] for stage in capacity["stages"]] == list(
            range(1, 9)
        )
    stages = runs["three-node-capacity-series"]["stages"]
    assert [row["baselineMultiplier"] for row in stages] == [
        0.5, 1, 1.5, 2, 2.5, 3, 3.5, 4
    ]
    assert runs["three-node-steady"]["duration"] == "30m"
    assert matrix["capacityAggregation"]["safeOperatingCeilingFactor"] == 0.6
    assert matrix["capacityAggregation"]["requiredNMinusOneSeries"] == [
        "node-1", "node-2", "node-3"
    ]


def test_signalr_load_requires_auth_and_records_upgrade_and_handshake() -> None:
    source = LOAD_SCRIPT.read_text(encoding="utf-8")
    assert "MASSAR_WS_ACCESS_TOKEN" in source
    assert "Authorization: `Bearer ${__ENV.MASSAR_WS_ACCESS_TOKEN}`" in source
    assert "access_token=" not in source
    assert "massar_signalr_upgrade_success" in source
    assert "massar_signalr_handshake_success" in source
    assert "massar_signalr_hold_success" in source
    assert "Date.now() - openedAt >= websocketHoldMs * 0.90" in source


def test_assets_upload_and_continuity_probes_are_explicit_and_non_publishing() -> None:
    source = LOAD_SCRIPT.read_text(encoding="utf-8")
    for required in (
        "MASSAR_PUBLIC_ASSET_URL",
        "MASSAR_PROTECTED_ASSET_URL",
        "MASSAR_UPLOAD_PROBE_URL",
        "MASSAR_WORKFLOW_ACCESS_TOKEN",
    ):
        assert required in source
    assert "load-probe-invalid.png" in source
    assert "expected = response.status === 400" in source
    assert "massar_workflow_success" in source

    matrix = json.loads(
        PERFORMANCE_MATRIX.read_text(encoding="utf-8")
    )
    runs = {row["name"]: row for row in matrix["runs"]}
    assert runs["rolling-deploy-continuity"]["workflowProbesRequired"] is True
    assert runs["failover-continuity"]["workflowProbesRequired"] is True
    assert all(row["workflowProbesRequired"] is True for row in matrix["runs"])
    contract = matrix["workflowProbeContract"]
    assert contract["mode"] == "authenticated-real-workflows"
    assert contract["runner"] == "deploy/production/load/platform-workflows.js"
    assert contract["syntheticHealthOnlyAccepted"] is False
    assert contract["minimumWorkflowRps"] >= 1
    assert {
        "login",
        "student-dashboard",
        "student-packages",
        "admin-search",
        "live-support",
        "signalr-reconnect",
    } <= set(contract["requiredWorkflows"])
    assert matrix["manualChecks"]

    workflow_source = WORKFLOW_SCRIPT.read_text(encoding="utf-8")
    for journey in contract["requiredWorkflows"]:
        exported_name = {
            "login": "loginJourney",
            "student-dashboard": "studentDashboardJourney",
            "student-packages": "studentPackagesJourney",
            "admin-search": "adminSearchJourney",
            "live-support": "liveSupportJourney",
            "signalr-reconnect": "signalRReconnectJourney",
        }[journey]
        assert f"export function {exported_name}" in workflow_source


def test_load_evidence_schema_matches_normalized_summary_contract() -> None:
    schema = json.loads(LOAD_SCHEMA.read_text(encoding="utf-8"))
    assert schema["additionalProperties"] is False
    required = set(schema["required"])
    assert {
        "releaseId",
        "runId",
        "capturedAt",
        "durationSeconds",
        "baselineMultiplier",
        "errorRate",
        "p95Milliseconds",
        "p99Milliseconds",
        "droppedIterationRate",
        "healthyNodeCount",
        "thresholdFailures",
        "profile",
        "capacityStages",
        "nodeRequestCounts",
        "nodeTrafficShares",
        "nodeImbalanceRatio",
        "unexpectedNodeRate",
        "websocketHoldSuccessRate",
        "workflowSuccessRate",
        "surfaceP95Milliseconds",
        "surfaceP99Milliseconds",
    } <= required
    assert schema["properties"]["expectedNodes"]["items"]["enum"] == [
        "node-1",
        "node-2",
        "node-3",
    ]
    assert "capacity-ladder" not in schema["properties"]["profile"]["enum"]
