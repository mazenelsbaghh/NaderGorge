#!/usr/bin/env python3
"""Bind resource-capacity samples to one immutable k6 load stage."""

from __future__ import annotations

import datetime as dt
import hashlib
import json
from pathlib import Path
from typing import Any

from acceptance_schema import SchemaError, validate


ROOT = Path(__file__).resolve().parents[1]
LOAD_SCHEMA_PATH = ROOT / "evidence/schemas/load.schema.json"
STAGE_SCHEMA_PATH = ROOT / "evidence/schemas/capacity-stage.schema.json"
DEFAULT_THRESHOLDS_PATH = ROOT / "config/capacity-thresholds.json"
NODES = ("node-1", "node-2", "node-3")


class CapacityStageError(RuntimeError):
    pass


def _read_object(path: Path, label: str) -> tuple[bytes, dict[str, Any]]:
    if not path.is_file() or path.is_symlink():
        raise CapacityStageError(f"{label} must be a regular non-symlink file")
    try:
        raw = path.read_bytes()
        value = json.loads(raw)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise CapacityStageError(f"{label} must be valid JSON") from exc
    if not isinstance(value, dict):
        raise CapacityStageError(f"{label} must be a JSON object")
    return raw, value


def _parse_time(value: object, label: str) -> dt.datetime:
    if not isinstance(value, str):
        raise CapacityStageError(f"{label} must be a date-time")
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise CapacityStageError(f"{label} must be a date-time") from exc
    if parsed.tzinfo is None:
        raise CapacityStageError(f"{label} must include timezone")
    return parsed


def load_thresholds(path: Path = DEFAULT_THRESHOLDS_PATH) -> dict[str, float]:
    _, value = _read_object(path, "capacity thresholds")
    expected = {
        "schemaVersion",
        "cpuBusyPercentMaximum",
        "cpuIowaitPercentMaximum",
        "cpuStealPercentMaximum",
        "memoryAvailablePercentMinimum",
        "diskFreePercentMinimum",
        "postgresConnectionUtilizationPercentMaximum",
        "postgresWaitingLocksMaximum",
        "postgresReplicationLagBytesMaximum",
        "redisMemoryUtilizationPercentMaximum",
        "redisBlockedClientsClusterTotalAbsoluteMaximum",
        "redisBlockedClientsClusterTotalIncreaseMaximum",
        "redisAofDelayedFsyncMaximum",
        "queueWaitingMaximum",
        "queueFailedMaximum",
        "queueStalledMaximum",
        "minimumDuringSamples",
        "maximumSampleAgeSeconds",
    }
    if set(value) != expected or value["schemaVersion"] != 1:
        raise CapacityStageError("capacity threshold fields do not match v1")
    result: dict[str, float] = {}
    for name in expected - {"schemaVersion"}:
        raw = value[name]
        if isinstance(raw, bool) or not isinstance(raw, (int, float)) or raw < 0:
            raise CapacityStageError(f"capacity threshold {name} is invalid")
        result[name] = float(raw)
    for percent in (
        "cpuBusyPercentMaximum",
        "cpuIowaitPercentMaximum",
        "cpuStealPercentMaximum",
        "memoryAvailablePercentMinimum",
        "diskFreePercentMinimum",
        "postgresConnectionUtilizationPercentMaximum",
        "redisMemoryUtilizationPercentMaximum",
    ):
        if result[percent] > 100:
            raise CapacityStageError(f"capacity threshold {percent} exceeds 100")
    if result["minimumDuringSamples"] < 1:
        raise CapacityStageError("minimumDuringSamples must be at least 1")
    if result["maximumSampleAgeSeconds"] < 1:
        raise CapacityStageError("maximumSampleAgeSeconds must be at least 1")
    return result


def _metric(value: object, label: str, violations: list[str]) -> float | None:
    try:
        result = float(value)
    except (TypeError, ValueError):
        violations.append(f"missing:{label}")
        return None
    if result < 0:
        violations.append(f"invalid:{label}")
        return None
    return result


def evaluate_samples(
    samples: list[dict[str, Any]],
    thresholds: dict[str, float],
    *,
    started_at: dt.datetime,
    completed_at: dt.datetime,
) -> list[str]:
    violations: list[str] = []
    phases = [sample.get("phase") for sample in samples]
    if phases.count("before") != 1 or phases.count("after") != 1:
        violations.append("samples:exact-before-and-after-required")
    if phases.count("during") < int(thresholds["minimumDuringSamples"]):
        violations.append("samples:insufficient-during")
    maximum_age = dt.timedelta(seconds=thresholds["maximumSampleAgeSeconds"])
    redis_blocked_totals: list[tuple[str, float] | None] = []

    for sample_index, sample in enumerate(samples):
        label = f"sample-{sample_index + 1}"
        captured = _parse_time(sample.get("capturedAt"), f"{label}.capturedAt")
        phase = sample.get("phase")
        if (
            phase == "before"
            and not (started_at - maximum_age <= captured <= started_at)
        ):
            violations.append(f"stale:{label}:before")
        elif phase == "during" and not (started_at <= captured <= completed_at):
            violations.append(f"stale:{label}:during")
        elif (
            phase == "after"
            and not (completed_at <= captured <= completed_at + maximum_age)
        ):
            violations.append(f"stale:{label}:after")
        nodes = sample.get("nodes")
        if not isinstance(nodes, list) or len(nodes) != 3:
            violations.append(f"{label}:three-nodes-required")
            continue
        node_ids = [node.get("nodeId") for node in nodes if isinstance(node, dict)]
        if set(node_ids) != set(NODES):
            violations.append(f"{label}:node-set")
            continue
        patroni_primary_count = 0
        redis_blocked_total = 0.0
        redis_blocked_complete = True
        for node in nodes:
            node_id = str(node["nodeId"])
            cpu = node.get("cpu", {})
            memory = node.get("memory", {})
            disk = node.get("rootDisk", {})
            postgres = node.get("postgres", {})
            redis = node.get("redis", {})
            patroni = node.get("patroni", {})
            queues = node.get("queues", {})
            checks = (
                ("cpu-busy", _metric(cpu.get("busyPercent"), f"{node_id}:cpu-busy", violations),
                 thresholds["cpuBusyPercentMaximum"], "max"),
                ("cpu-iowait", _metric(cpu.get("iowaitPercent"), f"{node_id}:cpu-iowait", violations),
                 thresholds["cpuIowaitPercentMaximum"], "max"),
                ("cpu-steal", _metric(cpu.get("stealPercent"), f"{node_id}:cpu-steal", violations),
                 thresholds["cpuStealPercentMaximum"], "max"),
            )
            for name, metric, limit, direction in checks:
                if metric is not None and direction == "max" and metric > limit:
                    violations.append(f"{node_id}:{name}:{metric}>{limit}")
            memory_total = _metric(memory.get("totalBytes"), f"{node_id}:memory-total", violations)
            memory_available = _metric(memory.get("availableBytes"), f"{node_id}:memory-available", violations)
            if memory_total and memory_available is not None:
                available_percent = memory_available * 100 / memory_total
                if available_percent < thresholds["memoryAvailablePercentMinimum"]:
                    violations.append(f"{node_id}:memory-headroom")
            disk_total = _metric(disk.get("totalBytes"), f"{node_id}:disk-total", violations)
            disk_free = _metric(disk.get("freeBytes"), f"{node_id}:disk-free", violations)
            if disk_total and disk_free is not None:
                free_percent = disk_free * 100 / disk_total
                if free_percent < thresholds["diskFreePercentMinimum"]:
                    violations.append(f"{node_id}:disk-headroom")
            connections = _metric(postgres.get("connections"), f"{node_id}:pg-connections", violations)
            max_connections = _metric(postgres.get("maxConnections"), f"{node_id}:pg-max-connections", violations)
            locks = _metric(postgres.get("waitingLocks"), f"{node_id}:pg-locks", violations)
            lag = _metric(postgres.get("replicationLagBytes"), f"{node_id}:pg-lag", violations)
            if connections is not None and max_connections:
                if connections * 100 / max_connections > thresholds[
                    "postgresConnectionUtilizationPercentMaximum"
                ]:
                    violations.append(f"{node_id}:pg-connection-headroom")
            if locks is not None and locks > thresholds["postgresWaitingLocksMaximum"]:
                violations.append(f"{node_id}:pg-waiting-locks")
            if lag is not None and lag > thresholds["postgresReplicationLagBytesMaximum"]:
                violations.append(f"{node_id}:pg-replication-lag")
            used_memory = _metric(redis.get("used_memory"), f"{node_id}:redis-used", violations)
            max_memory = _metric(redis.get("maxmemory"), f"{node_id}:redis-max", violations)
            blocked = _metric(redis.get("blocked_clients"), f"{node_id}:redis-blocked", violations)
            aof_delay = _metric(redis.get("aof_delayed_fsync"), f"{node_id}:redis-aof", violations)
            if used_memory is not None and max_memory:
                if used_memory * 100 / max_memory > thresholds[
                    "redisMemoryUtilizationPercentMaximum"
                ]:
                    violations.append(f"{node_id}:redis-memory-headroom")
            if blocked is None:
                redis_blocked_complete = False
            else:
                redis_blocked_total += blocked
            if aof_delay is not None and aof_delay > thresholds["redisAofDelayedFsyncMaximum"]:
                violations.append(f"{node_id}:redis-aof-delayed-fsync")
            if patroni.get("state") != "running":
                violations.append(f"{node_id}:patroni-state")
            if patroni.get("role") in {"leader", "master", "primary"}:
                patroni_primary_count += 1
            if not isinstance(queues, dict) or not queues:
                violations.append(f"missing:{node_id}:queues")
            else:
                for queue_name, counts in queues.items():
                    if not isinstance(counts, dict):
                        violations.append(f"invalid:{node_id}:queue:{queue_name}")
                        continue
                    for field, threshold_name in (
                        ("waiting", "queueWaitingMaximum"),
                        ("failed", "queueFailedMaximum"),
                        ("stalled", "queueStalledMaximum"),
                    ):
                        count = _metric(
                            counts.get(field),
                            f"{node_id}:queue:{queue_name}:{field}",
                            violations,
                        )
                        if count is not None and count > thresholds[threshold_name]:
                            violations.append(f"{node_id}:queue:{queue_name}:{field}")
        if patroni_primary_count != 1:
            violations.append(f"{label}:patroni-primary-count")
        redis_blocked_totals.append(
            (label, redis_blocked_total) if redis_blocked_complete else None
        )

    before_indexes = [
        index for index, sample in enumerate(samples)
        if sample.get("phase") == "before"
    ]
    baseline_total = (
        redis_blocked_totals[before_indexes[0]]
        if len(before_indexes) == 1
        else None
    )
    if baseline_total is not None:
        _, baseline_value = baseline_total
        absolute_maximum = thresholds[
            "redisBlockedClientsClusterTotalAbsoluteMaximum"
        ]
        increase_maximum = thresholds[
            "redisBlockedClientsClusterTotalIncreaseMaximum"
        ]
        for total in redis_blocked_totals:
            if total is None:
                continue
            label, value = total
            if value > absolute_maximum:
                violations.append(
                    f"{label}:redis-blocked-clients-cluster-total:"
                    f"{value}>{absolute_maximum}"
                )
            if value - baseline_value > increase_maximum:
                violations.append(
                    f"{label}:redis-blocked-clients-cluster-increase:"
                    f"{value - baseline_value}>{increase_maximum}"
                )
    return sorted(set(violations))


def build_stage_evidence(
    *,
    load_path: Path,
    samples: list[dict[str, Any]],
    thresholds_path: Path = DEFAULT_THRESHOLDS_PATH,
    now: dt.datetime | None = None,
) -> dict[str, Any]:
    load_raw, load = _read_object(load_path, "load evidence")
    _, load_schema = _read_object(LOAD_SCHEMA_PATH, "load schema")
    try:
        validate(load, load_schema, "$load")
    except SchemaError as exc:
        raise CapacityStageError(f"load evidence violates schema: {exc}") from exc
    started_at = _parse_time(load["startedAt"], "load.startedAt")
    completed_at = _parse_time(load["completedAt"], "load.completedAt")
    thresholds = load_thresholds(thresholds_path)
    violations = evaluate_samples(
        samples,
        thresholds,
        started_at=started_at,
        completed_at=completed_at,
    )
    captured_at = now or dt.datetime.now(dt.timezone.utc)
    result = {
        "schemaVersion": 1,
        "status": "success" if not violations else "failed",
        "runId": load["runId"],
        "releaseId": load["releaseId"],
        "requestedRps": load["requestedRps"],
        "baselineRps": load["baselineRps"],
        "profile": load["profile"],
        "excludedNode": load["excludedNode"],
        "expectedNodes": load["expectedNodes"],
        "startedAt": load["startedAt"],
        "completedAt": load["completedAt"],
        "capturedAt": captured_at.astimezone(dt.timezone.utc)
        .isoformat()
        .replace("+00:00", "Z"),
        "loadEvidenceSha256": hashlib.sha256(load_raw).hexdigest(),
        "thresholds": {"schemaVersion": 1, **thresholds},
        "samples": samples,
        "violations": violations,
    }
    _, stage_schema = _read_object(STAGE_SCHEMA_PATH, "capacity stage schema")
    try:
        validate(result, stage_schema, "$capacity-stage")
    except SchemaError as exc:
        raise CapacityStageError(f"capacity stage violates schema: {exc}") from exc
    return result
