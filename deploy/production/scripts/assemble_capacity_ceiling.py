#!/usr/bin/env python3
"""Assemble fail-closed N-1 capacity evidence from separate constant-rate runs."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
import sys
import tempfile
from pathlib import Path

from acceptance_schema import SchemaError, validate
from capacity_stage_evidence import evaluate_samples


SCHEMA_PATH = (
    Path(__file__).resolve().parents[1]
    / "evidence/schemas/capacity-ceiling.schema.json"
)
LOAD_SCHEMA_PATH = (
    Path(__file__).resolve().parents[1]
    / "evidence/schemas/load.schema.json"
)
CAPACITY_STAGE_SCHEMA_PATH = (
    Path(__file__).resolve().parents[1]
    / "evidence/schemas/capacity-stage.schema.json"
)
NODES = ("node-1", "node-2", "node-3")
SAFE_CEILING_FACTOR = 0.6


class CapacityAssemblyError(RuntimeError):
    pass


def _read_json(path: Path, label: str) -> tuple[bytes, dict[str, object]]:
    if not path.is_file() or path.is_symlink():
        raise CapacityAssemblyError(f"{label} must be a regular non-symlink file")
    try:
        raw = path.read_bytes()
        value = json.loads(raw)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise CapacityAssemblyError(f"{label} must be valid JSON") from exc
    if not isinstance(value, dict):
        raise CapacityAssemblyError(f"{label} must contain a JSON object")
    return raw, value


def _number(value: object, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise CapacityAssemblyError(f"{label} must be a number")
    result = float(value)
    if not result > 0:
        raise CapacityAssemblyError(f"{label} must be positive")
    return result


def _same_number(left: object, right: float) -> bool:
    return (
        isinstance(left, (int, float))
        and not isinstance(left, bool)
        and abs(float(left) - right) <= 1e-9
    )


def _parse_time(value: object, label: str) -> dt.datetime:
    if not isinstance(value, str):
        raise CapacityAssemblyError(f"{label} must be a date-time")
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise CapacityAssemblyError(f"{label} must be a date-time") from exc
    if parsed.tzinfo is None:
        raise CapacityAssemblyError(f"{label} must include timezone")
    return parsed


def _load_schema(path: Path, label: str) -> dict[str, object]:
    _, schema = _read_json(path, label)
    return schema


def assemble(
    *,
    manifest_path: Path,
    output: Path,
    now: dt.datetime | None = None,
) -> dict[str, object]:
    _, manifest = _read_json(manifest_path, "capacity series manifest")
    required_manifest = {"schemaVersion", "releaseId", "baselineRps", "series"}
    if set(manifest) != required_manifest or manifest["schemaVersion"] != 1:
        raise CapacityAssemblyError(
            "capacity series manifest fields do not match the exact v1 contract"
        )
    release_id = manifest["releaseId"]
    if not isinstance(release_id, str) or not release_id:
        raise CapacityAssemblyError("manifest releaseId must be a non-empty string")
    baseline_rps = _number(manifest["baselineRps"], "manifest baselineRps")
    source_series = manifest["series"]
    if not isinstance(source_series, list) or len(source_series) != 3:
        raise CapacityAssemblyError("manifest must contain exactly three N-1 series")

    load_schema = _load_schema(LOAD_SCHEMA_PATH, "load evidence schema")
    capacity_stage_schema = _load_schema(
        CAPACITY_STAGE_SCHEMA_PATH,
        "capacity stage schema",
    )
    expected_exclusions = set(NODES)
    seen_exclusions: set[str] = set()
    seen_run_ids: set[str] = set()
    assembled_series: list[dict[str, object]] = []

    for series_index, series in enumerate(source_series):
        if not isinstance(series, dict) or set(series) != {
            "seriesId", "excludedNode", "stages"
        }:
            raise CapacityAssemblyError(
                f"series[{series_index}] fields do not match the exact contract"
            )
        series_id = series["seriesId"]
        excluded_node = series["excludedNode"]
        stages = series["stages"]
        if not isinstance(series_id, str) or not series_id:
            raise CapacityAssemblyError(f"series[{series_index}].seriesId is invalid")
        if excluded_node not in expected_exclusions:
            raise CapacityAssemblyError(f"{series_id} excludedNode is invalid")
        if excluded_node in seen_exclusions:
            raise CapacityAssemblyError(f"duplicate N-1 series for {excluded_node}")
        seen_exclusions.add(excluded_node)
        if not isinstance(stages, list) or len(stages) < 2:
            raise CapacityAssemblyError(f"{series_id} requires at least two stages")

        expected_nodes = [node for node in NODES if node != excluded_node]
        assembled_stages: list[dict[str, object]] = []
        previous_rps = 0.0
        first_failure: float | None = None
        maximum_passing: float | None = None

        for stage_index, stage in enumerate(stages, start=1):
            if not isinstance(stage, dict) or set(stage) != {
                "sequence", "requestedRps", "evidencePath", "capacityEvidencePath"
            }:
                raise CapacityAssemblyError(
                    f"{series_id} stage {stage_index} fields do not match the exact contract"
                )
            if stage["sequence"] != stage_index:
                raise CapacityAssemblyError(
                    f"{series_id} stage sequence must be contiguous from 1"
                )
            requested_rps = _number(
                stage["requestedRps"],
                f"{series_id} stage {stage_index} requestedRps",
            )
            if requested_rps <= previous_rps:
                raise CapacityAssemblyError(
                    f"{series_id} stage RPS values must be strictly increasing"
                )
            previous_rps = requested_rps
            evidence_path_value = stage["evidencePath"]
            if not isinstance(evidence_path_value, str) or not evidence_path_value:
                raise CapacityAssemblyError(
                    f"{series_id} stage {stage_index} evidencePath is invalid"
                )
            evidence_path = Path(evidence_path_value)
            if not evidence_path.is_absolute():
                evidence_path = manifest_path.parent / evidence_path
            raw, evidence = _read_json(
                evidence_path,
                f"{series_id} stage {stage_index} evidence",
            )
            try:
                validate(evidence, load_schema, f"${series_id}[{stage_index}]")
            except SchemaError as exc:
                raise CapacityAssemblyError(
                    f"{series_id} stage {stage_index} violates load schema: {exc}"
                ) from exc

            bindings_valid = (
                evidence["releaseId"] == release_id
                and evidence["profile"] == "n-minus-one"
                and evidence["excludedNode"] == excluded_node
                and evidence["expectedNodes"] == expected_nodes
                and evidence["capacityStages"] == []
                and _same_number(evidence["baselineRps"], baseline_rps)
                and _same_number(evidence["requestedRps"], requested_rps)
                and _same_number(
                    evidence["baselineMultiplier"],
                    requested_rps / baseline_rps,
                )
            )
            if not bindings_valid:
                raise CapacityAssemblyError(
                    f"{series_id} stage {stage_index} is not exactly bound "
                    "to release, N-1 target, baseline, and requested RPS"
                )
            run_id = evidence["runId"]
            if run_id in seen_run_ids:
                raise CapacityAssemblyError(f"duplicate load runId {run_id}")
            seen_run_ids.add(run_id)

            capacity_path_value = stage["capacityEvidencePath"]
            if not isinstance(capacity_path_value, str) or not capacity_path_value:
                raise CapacityAssemblyError(
                    f"{series_id} stage {stage_index} capacityEvidencePath is invalid"
                )
            capacity_path = Path(capacity_path_value)
            if not capacity_path.is_absolute():
                capacity_path = manifest_path.parent / capacity_path
            capacity_raw, capacity = _read_json(
                capacity_path,
                f"{series_id} stage {stage_index} capacity evidence",
            )
            try:
                validate(
                    capacity,
                    capacity_stage_schema,
                    f"$capacity-{series_id}[{stage_index}]",
                )
            except SchemaError as exc:
                raise CapacityAssemblyError(
                    f"{series_id} stage {stage_index} violates capacity schema: {exc}"
                ) from exc
            load_sha = hashlib.sha256(raw).hexdigest()
            capacity_bindings_valid = (
                capacity["runId"] == run_id
                and capacity["releaseId"] == release_id
                and capacity["profile"] == "n-minus-one"
                and capacity["excludedNode"] == excluded_node
                and capacity["expectedNodes"] == expected_nodes
                and _same_number(capacity["baselineRps"], baseline_rps)
                and _same_number(capacity["requestedRps"], requested_rps)
                and capacity["loadEvidenceSha256"] == load_sha
            )
            maximum_age = float(
                capacity["thresholds"]["maximumSampleAgeSeconds"]
            )
            recomputed_violations = evaluate_samples(
                capacity["samples"],
                {
                    key: float(value)
                    for key, value in capacity["thresholds"].items()
                    if key != "schemaVersion"
                },
                started_at=_parse_time(evidence["startedAt"], "load.startedAt"),
                completed_at=_parse_time(evidence["completedAt"], "load.completedAt"),
            )
            freshness_seconds = (
                _parse_time(capacity["capturedAt"], "capacity.capturedAt")
                - _parse_time(evidence["completedAt"], "load.completedAt")
            ).total_seconds()
            if (
                not capacity_bindings_valid
                or recomputed_violations != capacity["violations"]
                or freshness_seconds < 0
                or freshness_seconds > maximum_age
            ):
                raise CapacityAssemblyError(
                    f"{series_id} stage {stage_index} capacity evidence is "
                    "missing, stale, or not exactly bound"
                )

            load_status = evidence["status"]
            capacity_status = capacity["status"]
            status = (
                "success"
                if load_status == "success" and capacity_status == "success"
                else "failed"
            )
            failures = evidence["thresholdFailures"]
            if load_status == "success" and failures:
                raise CapacityAssemblyError(
                    f"{series_id} successful load stage has threshold failures"
                )
            if load_status == "failed" and not failures:
                raise CapacityAssemblyError(
                    f"{series_id} failed load stage has no threshold failure evidence"
                )
            if capacity_status == "success" and capacity["violations"]:
                raise CapacityAssemblyError(
                    f"{series_id} successful capacity stage has violations"
                )
            if capacity_status == "failed" and not capacity["violations"]:
                raise CapacityAssemblyError(
                    f"{series_id} failed capacity stage has no violation evidence"
                )
            if status == "success":
                if first_failure is not None:
                    raise CapacityAssemblyError(
                        f"{series_id} contains a stage after its first failure"
                    )
                if (
                    evidence["healthyNodeCount"] != 2
                    or evidence["observedNodes"] != expected_nodes
                    or evidence["unexpectedNodeRate"] != 0
                ):
                    raise CapacityAssemblyError(
                        f"{series_id} successful stage lacks exact two-node evidence"
                    )
                maximum_passing = requested_rps
            else:
                if first_failure is not None:
                    raise CapacityAssemblyError(
                        f"{series_id} contains a stage after its first failure"
                    )
                first_failure = requested_rps

            assembled_stages.append({
                "sequence": stage_index,
                "requestedRps": requested_rps,
                "baselineMultiplier": requested_rps / baseline_rps,
                "status": status,
                "loadStatus": load_status,
                "capacityStatus": capacity_status,
                "runId": run_id,
                "capturedAt": evidence["capturedAt"],
                "evidenceSha256": load_sha,
                "capacityEvidenceSha256": hashlib.sha256(capacity_raw).hexdigest(),
            })

        if maximum_passing is None:
            raise CapacityAssemblyError(
                f"{series_id} has no passing stage before the bottleneck"
            )
        if first_failure is None:
            raise CapacityAssemblyError(
                f"{series_id} has no first failing stage; extend the bounded series"
            )
        assembled_series.append({
            "seriesId": series_id,
            "excludedNode": excluded_node,
            "expectedNodes": expected_nodes,
            "stages": assembled_stages,
            "maximumPassingRps": maximum_passing,
            "firstFailingRps": first_failure,
        })

    if seen_exclusions != expected_exclusions:
        raise CapacityAssemblyError("all three N-1 exclusions are required")

    worst_passing = min(
        float(series["maximumPassingRps"]) for series in assembled_series
    )
    first_failing = min(
        float(series["firstFailingRps"]) for series in assembled_series
    )
    bottleneck_nodes = sorted(
        str(series["excludedNode"])
        for series in assembled_series
        if float(series["maximumPassingRps"]) == worst_passing
    )
    captured_at = now or dt.datetime.now(dt.timezone.utc)
    if captured_at.tzinfo is None:
        raise CapacityAssemblyError("assembly time must include a timezone")
    result: dict[str, object] = {
        "schemaVersion": 1,
        "status": "success",
        "releaseId": release_id,
        "capturedAt": captured_at.astimezone(dt.timezone.utc)
        .isoformat()
        .replace("+00:00", "Z"),
        "baselineRps": baseline_rps,
        "series": assembled_series,
        "firstFailingNMinusOneRps": first_failing,
        "worstPassingNMinusOneRps": worst_passing,
        "bottleneckExcludedNodes": bottleneck_nodes,
        "safeOperatingCeilingFactor": SAFE_CEILING_FACTOR,
        "safeOperatingCeilingRps": round(
            worst_passing * SAFE_CEILING_FACTOR,
            6,
        ),
    }
    output_schema = _load_schema(SCHEMA_PATH, "capacity ceiling schema")
    try:
        validate(result, output_schema, "$capacity-ceiling")
    except SchemaError as exc:
        raise CapacityAssemblyError(
            f"assembled capacity evidence violates schema: {exc}"
        ) from exc
    if output.exists() or output.is_symlink():
        raise CapacityAssemblyError("capacity ceiling output already exists")
    output.parent.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(result, indent=2, sort_keys=True) + "\n").encode()
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{output.name}.",
        dir=output.parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(encoded)
            handle.flush()
            os.fsync(handle.fileno())
        os.chmod(temporary, 0o640)
        os.replace(temporary, output)
    finally:
        if temporary.exists():
            temporary.unlink()
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        result = assemble(manifest_path=args.manifest, output=args.output)
    except (CapacityAssemblyError, OSError, SchemaError, ValueError) as exc:
        print(f"capacity assembly blocked: {exc}", file=sys.stderr)
        return 6
    print(json.dumps({
        "status": result["status"],
        "firstFailingNMinusOneRps": result["firstFailingNMinusOneRps"],
        "worstPassingNMinusOneRps": result["worstPassingNMinusOneRps"],
        "safeOperatingCeilingRps": result["safeOperatingCeilingRps"],
        "output": str(args.output),
    }))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
