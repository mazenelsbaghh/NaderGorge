#!/usr/bin/env python3
"""Run reviewed k6 stages through one inventory-selected control node."""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import stat
import sys
import tempfile
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any

from acceptance_schema import SchemaError, validate
from capacity_stage_evidence import (
    DEFAULT_THRESHOLDS_PATH,
    build_stage_evidence,
)
from clusterctl import Inventory, Node, load_inventory
from collect_capacity import collect_node
from ssh_transport import SshTarget, StrictSshTransport


ROOT = Path(__file__).resolve().parents[1]
LOAD_SCRIPT = ROOT / "tests/load/cluster-load.js"
LOAD_SCHEMA = ROOT / "evidence/schemas/load.schema.json"
K6_IMAGE = (
    "grafana/k6:1.8.0@"
    "sha256:b0982fa7880d4cecc1ab85a89b5f224a1dc88cf406e7999378d8bbe95e4e302b"
)
PLATFORM = "linux/amd64"
ORIGIN = "http://127.0.0.1:8088"
WS_ORIGIN = "ws://127.0.0.1:8088"
RUN_ID_RE = re.compile(r"[a-z0-9][a-z0-9._-]{0,63}")
RELEASE_RE = re.compile(r"(?:git-[0-9a-f]{7,40}|src-[0-9a-f]{40})")
PATH_RE = re.compile(r"/[A-Za-z0-9/_-]{1,200}")
REMOTE_TMP_RE = re.compile(r"/var/tmp/massar-k6\.[A-Za-z0-9]+")
ALLOWED_PROFILES = {"steady", "n-minus-one", "rolling-deploy", "failover"}
ENV_ALLOWLIST = {
    "MASSAR_LOAD_AUTHORIZED",
    "MASSAR_PUBLIC_ORIGIN",
    "MASSAR_API_ORIGIN",
    "MASSAR_RELEASE_ID",
    "MASSAR_LOAD_RUN_ID",
    "MASSAR_LOAD_EVIDENCE_PATH",
    "MASSAR_EXPECTED_NODES",
    "MASSAR_BASELINE_RPS",
    "MASSAR_LOAD_RATE",
    "MASSAR_LOAD_DURATION",
    "MASSAR_LOAD_PROFILE",
    "MASSAR_EXCLUDED_NODE",
    "MASSAR_PUBLIC_HOST",
    "MASSAR_API_HOST",
    "MASSAR_WS_ORIGIN",
    "MASSAR_WS_HOST",
    "MASSAR_WS_VUS",
    "MASSAR_WS_HOLD_MS",
    "MASSAR_WORKFLOW_RPS",
    "MASSAR_PUBLIC_ASSET_URL",
    "MASSAR_PROTECTED_ASSET_URL",
    "MASSAR_UPLOAD_PROBE_URL",
    "MASSAR_ASSET_HOST",
    "MASSAR_WS_ACCESS_TOKEN",
    "MASSAR_WORKFLOW_ACCESS_TOKEN",
}


class LiveLoadError(RuntimeError):
    pass


def _read_json(path: Path, label: str) -> dict[str, Any]:
    if not path.is_file() or path.is_symlink():
        raise LiveLoadError(f"{label} must be a regular non-symlink JSON file")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise LiveLoadError(f"{label} must be valid JSON") from exc
    if not isinstance(value, dict):
        raise LiveLoadError(f"{label} must contain a JSON object")
    return value


def duration_seconds(value: object) -> int:
    if not isinstance(value, str):
        raise LiveLoadError("stage duration must be a string")
    match = re.fullmatch(r"([1-9][0-9]*)(s|m)", value)
    if not match:
        raise LiveLoadError("stage duration must use bounded seconds or minutes")
    seconds = int(match.group(1)) * (60 if match.group(2) == "m" else 1)
    if seconds < 30 or seconds > 3600:
        raise LiveLoadError("stage duration must be between 30s and 60m")
    return seconds


def validate_plan(path: Path) -> dict[str, Any]:
    plan = _read_json(path, "load series plan")
    if set(plan) != {
        "schemaVersion",
        "seriesId",
        "releaseId",
        "baselineRps",
        "profile",
        "excludedNode",
        "expectedNodes",
        "stages",
    } or plan["schemaVersion"] != 1:
        raise LiveLoadError("load series plan fields do not match the exact v1 contract")
    if not isinstance(plan["seriesId"], str) or not RUN_ID_RE.fullmatch(plan["seriesId"]):
        raise LiveLoadError("seriesId is invalid")
    if not isinstance(plan["releaseId"], str) or not RELEASE_RE.fullmatch(plan["releaseId"]):
        raise LiveLoadError("releaseId must be immutable")
    baseline = plan["baselineRps"]
    if isinstance(baseline, bool) or not isinstance(baseline, (int, float)) or baseline <= 0:
        raise LiveLoadError("baselineRps must be positive")
    profile = plan["profile"]
    if profile not in ALLOWED_PROFILES:
        raise LiveLoadError("profile is invalid")
    expected_nodes = plan["expectedNodes"]
    if (
        not isinstance(expected_nodes, list)
        or len(expected_nodes) != len(set(expected_nodes))
        or any(node not in {"node-1", "node-2", "node-3"} for node in expected_nodes)
    ):
        raise LiveLoadError("expectedNodes is invalid")
    excluded = plan["excludedNode"]
    if profile == "n-minus-one":
        required = [node for node in ("node-1", "node-2", "node-3") if node != excluded]
        if excluded not in {"node-1", "node-2", "node-3"} or expected_nodes != required:
            raise LiveLoadError("N-1 plan must name one exclusion and the ordered remaining pair")
    elif excluded is not None or expected_nodes != ["node-1", "node-2", "node-3"]:
        raise LiveLoadError("non-N-1 plan must expect all three nodes and no exclusion")
    stages = plan["stages"]
    if not isinstance(stages, list) or not stages:
        raise LiveLoadError("plan requires at least one stage")
    previous_rate = 0.0
    seen_ids: set[str] = set()
    for sequence, stage in enumerate(stages, start=1):
        if not isinstance(stage, dict) or set(stage) != {
            "sequence", "requestedRps", "duration", "runId"
        }:
            raise LiveLoadError(f"stage {sequence} fields do not match the exact contract")
        if stage["sequence"] != sequence:
            raise LiveLoadError("stage sequence must be contiguous from 1")
        rate = stage["requestedRps"]
        if (
            isinstance(rate, bool)
            or not isinstance(rate, (int, float))
            or rate <= previous_rate
            or rate > 10000
        ):
            raise LiveLoadError("stage requestedRps must be increasing and no more than 10000")
        previous_rate = float(rate)
        duration_seconds(stage["duration"])
        run_id = stage["runId"]
        if not isinstance(run_id, str) or not RUN_ID_RE.fullmatch(run_id):
            raise LiveLoadError(f"stage {sequence} runId is invalid")
        if run_id in seen_ids:
            raise LiveLoadError("stage runId values must be unique")
        seen_ids.add(run_id)
    return plan


def validate_secret_file(path: Path | None, label: str, required: bool) -> Path | None:
    if path is None:
        if required:
            raise LiveLoadError(f"{label} file is required")
        return None
    candidate = path.expanduser().resolve()
    if candidate.is_symlink() or not candidate.is_file():
        raise LiveLoadError(f"{label} must be a regular non-symlink file")
    metadata = candidate.stat()
    if stat.S_IMODE(metadata.st_mode) != 0o600 or metadata.st_size not in range(1, 8193):
        raise LiveLoadError(f"{label} must be 0600 and between 1 and 8192 bytes")
    value = candidate.read_bytes()
    if b"\n" in value or b"\r" in value or b"\0" in value:
        raise LiveLoadError(f"{label} must contain one non-empty line")
    return candidate


def validate_probe_path(value: str | None, label: str, required: bool) -> str | None:
    if value is None:
        if required:
            raise LiveLoadError(f"{label} is required")
        return None
    if not PATH_RE.fullmatch(value) or "//" in value:
        raise LiveLoadError(f"{label} must be a normalized origin-relative path")
    return value


def build_environment(
    plan: dict[str, Any],
    stage: dict[str, Any],
    *,
    websocket_vus: int,
    websocket_hold_ms: int,
    workflow_rps: int,
    public_asset_path: str | None,
    protected_asset_path: str | None,
    upload_probe_path: str | None,
) -> dict[str, str]:
    values = {
        "MASSAR_LOAD_AUTHORIZED": "1",
        "MASSAR_PUBLIC_ORIGIN": ORIGIN,
        "MASSAR_API_ORIGIN": ORIGIN,
        "MASSAR_RELEASE_ID": str(plan["releaseId"]),
        "MASSAR_LOAD_RUN_ID": str(stage["runId"]),
        "MASSAR_LOAD_EVIDENCE_PATH": "/evidence/load.json",
        "MASSAR_EXPECTED_NODES": ",".join(plan["expectedNodes"]),
        "MASSAR_BASELINE_RPS": str(plan["baselineRps"]),
        "MASSAR_LOAD_RATE": str(stage["requestedRps"]),
        "MASSAR_LOAD_DURATION": str(stage["duration"]),
        "MASSAR_LOAD_PROFILE": str(plan["profile"]),
        "MASSAR_EXCLUDED_NODE": str(plan["excludedNode"] or ""),
        "MASSAR_PUBLIC_HOST": "massar-academy.net",
        "MASSAR_API_HOST": "api.massar-academy.net",
        "MASSAR_WS_ORIGIN": WS_ORIGIN,
        "MASSAR_WS_HOST": "ws.massar-academy.net",
        "MASSAR_WS_VUS": str(websocket_vus),
        "MASSAR_WS_HOLD_MS": str(websocket_hold_ms),
        "MASSAR_WORKFLOW_RPS": str(workflow_rps),
    }
    if workflow_rps:
        values.update({
            "MASSAR_PUBLIC_ASSET_URL": f"{ORIGIN}{public_asset_path}",
            "MASSAR_PROTECTED_ASSET_URL": f"{ORIGIN}{protected_asset_path}",
            "MASSAR_UPLOAD_PROBE_URL": f"{ORIGIN}{upload_probe_path}",
            "MASSAR_ASSET_HOST": "assets.massar-academy.net",
        })
    if not set(values) <= ENV_ALLOWLIST:
        raise LiveLoadError("generated environment escaped the allowlist")
    if any("\n" in value or "\r" in value or "\0" in value for value in values.values()):
        raise LiveLoadError("generated environment contains an invalid value")
    return values


def _target(inventory: Inventory, node_id: str) -> tuple[Node, SshTarget]:
    nodes = [node for node in inventory.nodes if node.id == node_id]
    if len(nodes) != 1:
        raise LiveLoadError("control node must be one exact inventory node")
    node = nodes[0]
    return node, SshTarget(
        node.id,
        node.public_address,
        str(inventory.cluster["ssh_user"]),
    )


def load_runner_inventory(path: Path) -> Inventory:
    """Validate inventory without making dry-run depend on operator SSH files."""
    previous = {
        name: os.environ.get(name)
        for name in ("MASSAR_KNOWN_HOSTS_FILE", "MASSAR_SSH_IDENTITY_FILE")
    }
    try:
        os.environ.setdefault("MASSAR_KNOWN_HOSTS_FILE", "/dev/null")
        os.environ.setdefault("MASSAR_SSH_IDENTITY_FILE", "/dev/null")
        return load_inventory(path, require_operator_files=False)
    finally:
        for name, value in previous.items():
            if value is None:
                os.environ.pop(name, None)
            else:
                os.environ[name] = value


def preflight(
    transport: StrictSshTransport,
    target: SshTarget,
    inventory: Inventory,
    release_id: str,
) -> None:
    checks = []
    for node in inventory.nodes:
        checks.append(
            "check() { h=$(mktemp); "
            f"curl --fail --silent --show-error -D \"$h\" -o /dev/null "
            f"-H 'Host: massar-academy.net' http://{node.overlay_address}:8080/__node_ready; "
            f"grep -Fqi 'X-Massar-Node: {node.id}' \"$h\"; "
            f"grep -Fqi 'X-Massar-Release: {release_id}' \"$h\"; rm -f \"$h\"; }}; check"
        )
    checks.extend([
        "curl --fail --silent --show-error -o /dev/null "
        "-H 'Host: massar-academy.net' http://127.0.0.1:8088/__node_ready",
        "curl --fail --silent --show-error -o /dev/null "
        "-H 'Host: api.massar-academy.net' http://127.0.0.1:8088/api/health/ready",
        "curl --fail --silent --show-error -o /dev/null "
        "-H 'Host: ws.massar-academy.net' http://127.0.0.1:8088/api/health/ready",
    ])
    script = "set -euo pipefail; test \"$(cat /etc/massar/cluster-id)\" = massar-production; " + "; ".join(checks)
    completed = transport.run(target, ("bash", "-lc", script), timeout_seconds=90, check=False)
    if completed.returncode:
        raise LiveLoadError("control-node release/three-ingress preflight failed")
    pull = transport.run(
        target,
        (
            "sudo", "docker", "pull", "--platform", PLATFORM, K6_IMAGE,
        ),
        timeout_seconds=300,
        check=False,
    )
    if pull.returncode:
        raise LiveLoadError("pinned linux/amd64 k6 image preflight failed")


def collect_snapshot(
    transport: StrictSshTransport,
    inventory: Inventory,
    phase: str,
) -> dict[str, Any]:
    with ThreadPoolExecutor(max_workers=3) as pool:
        nodes = list(pool.map(
            lambda node: collect_node(
                transport,
                str(inventory.cluster["ssh_user"]),
                node,
            ),
            inventory.nodes,
        ))
    return {
        "phase": phase,
        "capturedAt": dt.datetime.now(dt.timezone.utc)
        .isoformat()
        .replace("+00:00", "Z"),
        "nodes": nodes,
    }


def _write_json_atomic(path: Path, value: object) -> None:
    if path.exists() or path.is_symlink():
        raise LiveLoadError(f"refusing to overwrite {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.",
        dir=path.parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
            json.dump(value, handle, indent=2, sort_keys=True)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.chmod(temporary, 0o640)
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def run_series(
    *,
    inventory: Inventory,
    transport: StrictSshTransport,
    control_node: str,
    plan: dict[str, Any],
    evidence_dir: Path,
    series_output: Path,
    thresholds_path: Path,
    websocket_vus: int,
    websocket_hold_ms: int,
    workflow_rps: int,
    public_asset_path: str | None,
    protected_asset_path: str | None,
    upload_probe_path: str | None,
    websocket_token_file: Path | None,
    workflow_token_file: Path | None,
    sample_interval_seconds: int,
) -> dict[str, Any]:
    _, target = _target(inventory, control_node)
    preflight(transport, target, inventory, str(plan["releaseId"]))
    created = transport.run(
        target,
        (
            "bash", "-lc",
            "set -euo pipefail; umask 077; "
            "d=$(mktemp -d /var/tmp/massar-k6.XXXXXX); "
            "chmod 700 \"$d\"; printf '%s' \"$d\"",
        ),
        check=False,
    )
    remote_root = created.stdout.strip()
    if created.returncode or not REMOTE_TMP_RE.fullmatch(remote_root):
        raise LiveLoadError("failed to create a validated remote load directory")
    remote_nonce = remote_root.rsplit(".", 1)[-1]
    container_name = ""
    assembled_stages: list[dict[str, Any]] = []
    try:
        remote_script = f"{remote_root}/cluster-load.js"
        transport.copy(target, LOAD_SCRIPT, remote_script)
        transport.run(target, ("chmod", "0444", remote_script))
        remote_ws_token = f"{remote_root}/ws-token"
        remote_workflow_token = f"{remote_root}/workflow-token"
        if websocket_token_file:
            transport.copy(target, websocket_token_file, remote_ws_token)
            transport.run(target, ("chmod", "0600", remote_ws_token))
        if workflow_token_file:
            transport.copy(target, workflow_token_file, remote_workflow_token)
            transport.run(target, ("chmod", "0600", remote_workflow_token))

        for stage in plan["stages"]:
            run_id = str(stage["runId"])
            container_name = f"massar-k6-{run_id}-{remote_nonce}"
            remote_stage = f"{remote_root}/stage-{stage['sequence']}"
            remote_evidence = f"{remote_stage}/evidence/load.json"
            remote_env = f"{remote_stage}/runtime.env"
            transport.run(
                target,
                (
                    "bash", "-lc",
                    f"set -euo pipefail; "
                    f"test -z \"$(sudo docker ps -aq --filter name=^/{container_name}$)\"; "
                    f"umask 077; mkdir -p {remote_stage}/evidence; "
                    f"chmod 0733 {remote_stage}/evidence",
                ),
            )
            environment = build_environment(
                plan,
                stage,
                websocket_vus=websocket_vus,
                websocket_hold_ms=websocket_hold_ms,
                workflow_rps=workflow_rps,
                public_asset_path=public_asset_path,
                protected_asset_path=protected_asset_path,
                upload_probe_path=upload_probe_path,
            )
            with tempfile.NamedTemporaryFile(
                mode="w",
                encoding="utf-8",
                prefix="massar-k6-env-",
                delete=False,
            ) as handle:
                local_env = Path(handle.name)
                for key in sorted(environment):
                    handle.write(f"{key}={environment[key]}\n")
            try:
                os.chmod(local_env, 0o600)
                remote_nonsecret_env = f"{remote_stage}/nonsecret.env"
                transport.copy(target, local_env, remote_nonsecret_env)
            finally:
                local_env.unlink(missing_ok=True)
            secret_lines = []
            if websocket_token_file:
                secret_lines.append(
                    f"printf 'MASSAR_WS_ACCESS_TOKEN=%s\\n' \"$(cat {remote_ws_token})\""
                )
            if workflow_token_file:
                secret_lines.append(
                    f"printf 'MASSAR_WORKFLOW_ACCESS_TOKEN=%s\\n' \"$(cat {remote_workflow_token})\""
                )
            env_commands = [
                "set -euo pipefail",
                "umask 077",
                f"cp {remote_nonsecret_env} {remote_env}",
                *(f"{line} >> {remote_env}" for line in secret_lines),
                f"chmod 0600 {remote_env}",
            ]
            assemble_env = "; ".join(env_commands)
            transport.run(target, ("bash", "-lc", assemble_env))

            samples = [collect_snapshot(transport, inventory, "before")]
            result_holder: dict[str, Any] = {}

            def run_k6() -> None:
                try:
                    result_holder["completed"] = transport.run(
                        target,
                        (
                            "sudo", "docker", "run", "--rm",
                            "--name", container_name,
                            "--platform", PLATFORM,
                            "--network", "host",
                            "--read-only",
                            "--cap-drop", "ALL",
                            "--security-opt", "no-new-privileges:true",
                            "--pids-limit", "256",
                            "--user", "65534:65534",
                            "--tmpfs", "/tmp:rw,noexec,nosuid,nodev,size=16m",
                            "--env-file", remote_env,
                            "--mount", f"type=bind,src={remote_script},dst=/work/cluster-load.js,readonly",
                            "--mount", f"type=bind,src={remote_stage}/evidence,dst=/evidence",
                            K6_IMAGE,
                            "run", "/work/cluster-load.js",
                        ),
                        timeout_seconds=duration_seconds(stage["duration"]) + 180,
                        check=False,
                    )
                except Exception:
                    result_holder["transportError"] = True

            worker = threading.Thread(target=run_k6, daemon=False)
            worker.start()
            try:
                time.sleep(2)
                while worker.is_alive():
                    samples.append(collect_snapshot(transport, inventory, "during"))
                    worker.join(timeout=sample_interval_seconds)
            finally:
                worker.join()
            samples.append(collect_snapshot(transport, inventory, "after"))
            if result_holder.get("transportError"):
                raise LiveLoadError("remote k6 transport failed")

            load_path = evidence_dir / f"{run_id}.load.json"
            try:
                transport.fetch(
                    target,
                    remote_evidence,
                    load_path,
                    timeout_seconds=90,
                    max_bytes=1024 * 1024,
                )
            finally:
                transport.run(
                    target,
                    (
                        "bash", "-lc",
                        f"sudo docker rm -f {container_name} >/dev/null 2>&1 || true; "
                        f"rm -rf -- {remote_stage}",
                    ),
                    check=False,
                )
                container_name = ""
            load = _read_json(load_path, "fetched load evidence")
            load_schema = _read_json(LOAD_SCHEMA, "load schema")
            try:
                validate(load, load_schema, "$load")
            except SchemaError as exc:
                raise LiveLoadError(f"fetched load evidence violates schema: {exc}") from exc
            if (
                load["runId"] != run_id
                or load["releaseId"] != plan["releaseId"]
                or float(load["requestedRps"]) != float(stage["requestedRps"])
                or load["expectedNodes"] != plan["expectedNodes"]
            ):
                raise LiveLoadError("fetched load evidence is not bound to the requested stage")
            capacity = build_stage_evidence(
                load_path=load_path,
                samples=samples,
                thresholds_path=thresholds_path,
            )
            capacity_path = evidence_dir / f"{run_id}.capacity.json"
            _write_json_atomic(capacity_path, capacity)
            assembled_stages.append({
                "sequence": stage["sequence"],
                "requestedRps": stage["requestedRps"],
                "runId": run_id,
                "loadStatus": load["status"],
                "capacityStatus": capacity["status"],
                "evidencePath": str(load_path.resolve()),
                "capacityEvidencePath": str(capacity_path.resolve()),
            })
            if load["status"] != "success" or capacity["status"] != "success":
                break
    finally:
        cleanup_container = (
            f"sudo docker rm -f {container_name} >/dev/null 2>&1 || true; "
            if container_name else ""
        )
        transport.run(
            target,
            (
                "bash", "-lc",
                f"{cleanup_container}rm -rf -- {remote_root}",
            ),
            check=False,
        )
    failed_stage = next(
        (
            stage
            for stage in assembled_stages
            if stage["loadStatus"] != "success"
            or stage["capacityStatus"] != "success"
        ),
        None,
    )
    complete = len(assembled_stages) == len(plan["stages"])
    output = {
        "seriesId": plan["seriesId"],
        "excludedNode": plan["excludedNode"],
        "status": "success" if complete and failed_stage is None else "failed",
        "failedStageRunId": failed_stage["runId"] if failed_stage else None,
        "stages": assembled_stages,
    }
    _write_json_atomic(series_output, output)
    return output


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--control-node", required=True, choices=("node-1", "node-2", "node-3"))
    parser.add_argument("--plan", required=True, type=Path)
    parser.add_argument("--evidence-dir", required=True, type=Path)
    parser.add_argument("--series-output", required=True, type=Path)
    parser.add_argument("--known-hosts", type=Path)
    parser.add_argument("--identity", type=Path)
    parser.add_argument("--capacity-thresholds", type=Path, default=DEFAULT_THRESHOLDS_PATH)
    parser.add_argument("--sample-interval-seconds", type=int, default=30)
    parser.add_argument("--websocket-vus", type=int, default=0)
    parser.add_argument("--websocket-hold-ms", type=int, default=10000)
    parser.add_argument("--workflow-rps", type=int, default=0)
    parser.add_argument("--public-asset-path")
    parser.add_argument("--protected-asset-path")
    parser.add_argument("--upload-probe-path")
    parser.add_argument("--websocket-token-file", type=Path)
    parser.add_argument("--workflow-token-file", type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    args = parser.parse_args()
    try:
        plan = validate_plan(args.plan)
        inventory = load_runner_inventory(args.inventory)
        _target(inventory, args.control_node)
        if not 10 <= args.sample_interval_seconds <= 60:
            raise LiveLoadError("sample interval must be between 10 and 60 seconds")
        if args.websocket_vus < 0 or args.websocket_vus > 1000:
            raise LiveLoadError("websocket VUs must be between 0 and 1000")
        if args.websocket_hold_ms < 1000 or args.websocket_hold_ms > 60000:
            raise LiveLoadError("websocket hold must be between 1000 and 60000 ms")
        if args.workflow_rps < 0 or args.workflow_rps > 100:
            raise LiveLoadError("workflow RPS must be between 0 and 100")
        ws_token = validate_secret_file(
            args.websocket_token_file,
            "WebSocket token",
            args.websocket_vus > 0,
        )
        workflow_token = validate_secret_file(
            args.workflow_token_file,
            "workflow token",
            args.workflow_rps > 0,
        )
        public_path = validate_probe_path(
            args.public_asset_path, "public asset path", args.workflow_rps > 0
        )
        protected_path = validate_probe_path(
            args.protected_asset_path, "protected asset path", args.workflow_rps > 0
        )
        upload_path = validate_probe_path(
            args.upload_probe_path, "upload probe path", args.workflow_rps > 0
        )
        if args.dry_run:
            print(json.dumps({
                "status": "dry-run",
                "sshExecuted": False,
                "controlNode": args.control_node,
                "releaseId": plan["releaseId"],
                "seriesId": plan["seriesId"],
                "stageCount": len(plan["stages"]),
                "image": K6_IMAGE,
                "platform": PLATFORM,
                "origin": ORIGIN,
                "websocketCredentialsConfigured": ws_token is not None,
                "workflowCredentialsConfigured": workflow_token is not None,
            }))
            return 0
        if args.known_hosts is None or args.identity is None:
            raise LiveLoadError("--known-hosts and --identity are required with --yes")
        transport = StrictSshTransport(args.known_hosts, args.identity)
        output = run_series(
            inventory=inventory,
            transport=transport,
            control_node=args.control_node,
            plan=plan,
            evidence_dir=args.evidence_dir,
            series_output=args.series_output,
            thresholds_path=args.capacity_thresholds,
            websocket_vus=args.websocket_vus,
            websocket_hold_ms=args.websocket_hold_ms,
            workflow_rps=args.workflow_rps,
            public_asset_path=public_path,
            protected_asset_path=protected_path,
            upload_probe_path=upload_path,
            websocket_token_file=ws_token,
            workflow_token_file=workflow_token,
            sample_interval_seconds=args.sample_interval_seconds,
        )
        print(json.dumps({
            "status": output["status"],
            "seriesId": output["seriesId"],
            "completedStages": len(output["stages"]),
            "failedStageRunId": output["failedStageRunId"],
            "seriesOutput": str(args.series_output),
        }))
        return 0 if output["status"] == "success" else 6
    except (
        LiveLoadError,
        OSError,
        ValueError,
        SchemaError,
    ) as exc:
        print(f"live load runner blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
