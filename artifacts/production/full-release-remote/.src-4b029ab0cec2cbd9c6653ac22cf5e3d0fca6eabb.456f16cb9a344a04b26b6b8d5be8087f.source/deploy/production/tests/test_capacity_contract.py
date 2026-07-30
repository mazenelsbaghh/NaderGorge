from __future__ import annotations

from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


def test_capacity_snapshot_covers_resource_and_data_ceiling_signals() -> None:
    source = (
        ROOT / "deploy/production/scripts/collect_capacity.py"
    ).read_text(encoding="utf-8")
    for signal in (
        "busyPercent",
        "iowaitPercent",
        "stealPercent",
        "availableBytes",
        "swapFreeBytes",
        "containers",
        "releaseId",
        "connections",
        "waitingLocks",
        "maxConnections",
        "replicationLagBytes",
        "used_memory",
        "blocked_clients",
        "instantaneous_ops_per_sec",
        "aof_delayed_fsync",
        "queues",
        "waiting",
        "failed",
        "stalled",
    ):
        assert signal in source
    assert "ThreadPoolExecutor(max_workers=3)" in source
    assert "/etc/massar/secrets/postgres-app-password:/run/secrets/pgapp:ro" in source
    assert '"PGPASSWORD":' not in source


def test_capacity_stage_contract_gates_resources_and_binds_load() -> None:
    evaluator = (
        ROOT / "deploy/production/scripts/capacity_stage_evidence.py"
    ).read_text(encoding="utf-8")
    assembler = (
        ROOT / "deploy/production/scripts/assemble_capacity_ceiling.py"
    ).read_text(encoding="utf-8")
    for required in (
        "cpuBusyPercentMaximum",
        "cpuIowaitPercentMaximum",
        "cpuStealPercentMaximum",
        "memoryAvailablePercentMinimum",
        "diskFreePercentMinimum",
        "postgresConnectionUtilizationPercentMaximum",
        "postgresReplicationLagBytesMaximum",
        "postgresWaitingLocksMaximum",
        "redisMemoryUtilizationPercentMaximum",
        "redisBlockedClientsClusterTotalAbsoluteMaximum",
        "redisBlockedClientsClusterTotalIncreaseMaximum",
        "redisAofDelayedFsyncMaximum",
        "queueWaitingMaximum",
        "queueFailedMaximum",
        "queueStalledMaximum",
        "minimumDuringSamples",
        "maximumSampleAgeSeconds",
    ):
        assert required in evaluator
    assert "loadEvidenceSha256" in evaluator
    assert "capacityEvidencePath" in assembler
    assert "recomputed_violations" in assembler
    assert "missing, stale, or not exactly bound" in assembler


def test_load_profile_is_opt_in_release_bound_and_has_stop_thresholds() -> None:
    source = (
        ROOT / "deploy/production/tests/load/cluster-load.js"
    ).read_text(encoding="utf-8")
    assert "MASSAR_LOAD_AUTHORIZED" in source
    assert "Production domains are never implicit defaults" in source
    assert "MASSAR_RELEASE_ID" in source
    assert "dropped_iterations" in source
    assert "p(95)<1000" in source
    assert "p(99)<2000" in source
    assert "massar_release_mismatch" in source
    assert "MASSAR_LOAD_EVIDENCE_PATH" in source
    assert "[evidencePath]" in source
    assert "thresholdFailures" in source


def test_connection_budgets_leave_database_and_redis_headroom() -> None:
    app_env = (
        ROOT / "deploy/production/scripts/build_app_env.py"
    ).read_text(encoding="utf-8")
    redis = (
        ROOT / "deploy/production/config/redis/redis.conf.tmpl"
    ).read_text(encoding="utf-8")
    assert "Maximum Pool Size=50" in app_env
    assert "maxmemory 4gb" in redis
    assert "maxmemory-policy noeviction" in redis
