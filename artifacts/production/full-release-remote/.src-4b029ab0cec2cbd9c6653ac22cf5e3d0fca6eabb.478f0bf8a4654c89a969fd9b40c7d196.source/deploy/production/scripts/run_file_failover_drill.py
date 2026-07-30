#!/usr/bin/env python3
"""Run one bounded Gluster data-brick isolation drill with fail-closed recovery."""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import hashlib
import json
import os
import re
import sys
import time
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

from clusterctl import Inventory, Node, reject_secret_keys
from ssh_transport import SshTarget, StrictSshTransport


VOLUME = "massar-shared"
SHARED_ROOT = Path("/srv/massar-shared")
DRILL_TABLE = "massar_file_drill"
DATA_ROLES = frozenset({"file-data-primary", "file-data-standby"})
MAXIMUM_OUTAGE_MIN_SECONDS = 30
MAXIMUM_OUTAGE_MAX_SECONDS = 180
HEAL_TIMEOUT_SECONDS = 300
NODE_HOST = re.compile(r"^(node-[123])(?:\.cluster\.internal)?$")


class FileFailoverError(RuntimeError):
    """Raised when the drill cannot prove that one-brick isolation is safe."""


@dataclass(frozen=True)
class BrickState:
    node_id: str
    online: bool
    port: int


@dataclass(frozen=True)
class DrillOutcome:
    evidence: dict[str, object]
    error: BaseException | None


def utc_now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def iso8601(value: dt.datetime) -> str:
    return value.isoformat().replace("+00:00", "Z")


def parse_brick_status(xml_text: str) -> dict[str, BrickState]:
    """Return the three configured brick states from Gluster's XML status."""
    try:
        root = ET.fromstring(xml_text)
    except ET.ParseError as exc:
        raise FileFailoverError("Gluster returned invalid status XML.") from exc

    result: dict[str, BrickState] = {}
    for row in root.findall(".//node"):
        hostname = (row.findtext("hostname") or "").strip()
        path = (row.findtext("path") or "").strip()
        port_text = (row.findtext("port") or "").strip()
        status = (row.findtext("status") or "").strip()
        match = NODE_HOST.fullmatch(hostname)
        if match and path == "/srv/gluster/massar/brick":
            if not port_text.isdigit() or not 1024 <= int(port_text) <= 65535:
                raise FileFailoverError("Gluster returned an invalid brick port.")
            node_id = match.group(1)
            result[node_id] = BrickState(
                node_id=node_id,
                online=status == "1",
                port=int(port_text),
            )

    if set(result) != {"node-1", "node-2", "node-3"}:
        raise FileFailoverError("Gluster status did not contain the exact three approved bricks.")
    return result


def load_drill_inventory(path: Path) -> Inventory:
    """Load the secret-free inventory without resolving unrelated operator refs."""
    if not path.is_file():
        raise FileFailoverError(f"inventory not found: {path}")
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise FileFailoverError("inventory must use the JSON-compatible YAML subset") from exc
    if not isinstance(raw, dict):
        raise FileFailoverError("inventory root must be a mapping")
    reject_secret_keys(raw)
    if set(raw) != {"cluster", "nodes", "hostnames"}:
        raise FileFailoverError("inventory must contain only cluster, nodes, and hostnames")
    cluster = dict(raw["cluster"])
    if cluster.get("name") != "massar-production":
        raise FileFailoverError("inventory cluster name must be massar-production")
    if cluster.get("ssh_user") != "massar-ops":
        raise FileFailoverError("file drills require the approved massar-ops SSH user")
    nodes_raw = raw.get("nodes")
    if not isinstance(nodes_raw, list) or len(nodes_raw) != 3:
        raise FileFailoverError("inventory must define exactly three nodes")
    nodes = tuple(
        Node(
            id=str(item["id"]),
            ssh_alias=str(item["ssh_alias"]),
            public_address=str(item["public_address"]),
            overlay_address=str(item["overlay_address"]),
            roles=tuple(item["roles"]),
        )
        for item in nodes_raw
    )
    if tuple(node.id for node in nodes) != ("node-1", "node-2", "node-3"):
        raise FileFailoverError("nodes must be ordered node-1, node-2, node-3")
    if len({node.public_address for node in nodes}) != 3:
        raise FileFailoverError("node public addresses must be unique")
    if len({node.overlay_address for node in nodes}) != 3:
        raise FileFailoverError("node overlay addresses must be unique")
    return Inventory(
        path=path,
        cluster=cluster,
        nodes=nodes,
        hostnames=dict(raw["hostnames"]),
    )


def validate_target(inventory: Inventory, node_id: str) -> Node:
    by_id = {node.id: node for node in inventory.nodes}
    node = by_id.get(node_id)
    if node is None:
        raise FileFailoverError("The target must be one approved inventory node.")
    roles = set(node.roles)
    if not roles.intersection(DATA_ROLES) or "file-arbiter" in roles:
        raise FileFailoverError("Only one full data-brick node may be isolated; the arbiter is forbidden.")
    return node


def validate_healthy_prestate(states: dict[str, BrickState], target_node: str) -> None:
    if set(states) != {"node-1", "node-2", "node-3"}:
        raise FileFailoverError("Exactly three brick states are required.")
    offline = sorted(node_id for node_id, state in states.items() if not state.online)
    if offline:
        raise FileFailoverError(
            "The drill refuses to start while any brick is already unavailable: "
            + ",".join(offline)
        )
    if target_node not in states:
        raise FileFailoverError("The selected data brick is absent from Gluster status.")


def write_evidence(path: Path, payload: dict[str, object]) -> None:
    path = path.expanduser().resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(f".{path.name}.{uuid.uuid4().hex}.tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    os.chmod(temporary, 0o640)
    os.replace(temporary, path)


class FileFailoverDrill:
    """Orchestrates one data-brick isolation and always attempts full recovery."""

    def __init__(
        self,
        inventory: Inventory,
        transport: StrictSshTransport,
        *,
        maximum_outage_seconds: int,
        sleep: Callable[[float], None] = time.sleep,
        monotonic: Callable[[], float] = time.monotonic,
        now: Callable[[], dt.datetime] = utc_now,
    ) -> None:
        if not MAXIMUM_OUTAGE_MIN_SECONDS <= maximum_outage_seconds <= MAXIMUM_OUTAGE_MAX_SECONDS:
            raise FileFailoverError(
                f"maximum outage must be between {MAXIMUM_OUTAGE_MIN_SECONDS} "
                f"and {MAXIMUM_OUTAGE_MAX_SECONDS} seconds"
            )
        self.inventory = inventory
        self.transport = transport
        self.maximum_outage_seconds = maximum_outage_seconds
        self.sleep = sleep
        self.monotonic = monotonic
        self.now = now
        self.by_id = {node.id: node for node in inventory.nodes}

    def target(self, node: Node) -> SshTarget:
        return SshTarget(
            node.id,
            node.public_address,
            str(self.inventory.cluster["ssh_user"]),
        )

    def remote(
        self,
        node: Node,
        script: str,
        *,
        timeout_seconds: int = 60,
        check: bool = True,
    ) -> str:
        completed = self.transport.run(
            self.target(node),
            ("bash", "-lc", script),
            timeout_seconds=timeout_seconds,
            check=check,
        )
        return completed.stdout.strip()

    def control_node(self, isolated_node: str) -> Node:
        candidates = [
            node
            for node in self.inventory.nodes
            if node.id != isolated_node and set(node.roles).intersection(DATA_ROLES)
        ]
        if len(candidates) != 1:
            raise FileFailoverError("Exactly one non-target data node must remain as drill control.")
        return candidates[0]

    def inspect_node(self, node: Node) -> None:
        self.remote(
            node,
            f"""
set -euo pipefail
test "$(cat /etc/massar/cluster-id)" = "massar-production"
systemctl is-active --quiet glusterd
mountpoint -q {SHARED_ROOT}
test ! -e /run/massar-file-drill-active
test ! -e /run/massar-file-drill-recovery-required
""",
        )

    def brick_states(self, control: Node) -> dict[str, BrickState]:
        xml_text = self.remote(
            control,
            f"sudo gluster volume status {VOLUME} --xml",
        )
        return parse_brick_status(xml_text)

    def acquire_shared_lock(self, control: Node, operation_id: str) -> None:
        self.remote(
            control,
            f"""
set -euo pipefail
lock={SHARED_ROOT}/.cluster-health/file-failover.lock
mkdir "$lock"
printf '%s\n' {operation_id} > "$lock/operation-id"
sync "$lock/operation-id"
""",
        )

    def release_shared_lock(self, control: Node) -> None:
        self.remote(
            control,
            f"rm -rf {SHARED_ROOT}/.cluster-health/file-failover.lock",
            check=False,
        )

    def write_probe(self, control: Node, relative_path: str, content: bytes) -> str:
        encoded = base64.b64encode(content).decode("ascii")
        expected = hashlib.sha256(content).hexdigest()
        output = self.remote(
            control,
            f"""
set -euo pipefail
destination={SHARED_ROOT}/{relative_path}
install -d -m 0775 "$(dirname "$destination")"
temporary="$destination.{uuid.uuid4().hex}.tmp"
printf '%s' {encoded} | base64 -d > "$temporary"
sync "$temporary"
mv "$temporary" "$destination"
sync "$(dirname "$destination")"
sha256sum "$destination" | awk '{{print $1}}'
""",
        )
        if output != expected:
            raise FileFailoverError("The acknowledged probe checksum differs at write time.")
        return expected

    def verify_probe(
        self,
        node_ids: tuple[str, ...],
        relative_path: str,
        expected_checksum: str,
    ) -> None:
        for node_id in node_ids:
            actual = self.remote(
                self.by_id[node_id],
                f"sha256sum {SHARED_ROOT}/{relative_path} | awk '{{print $1}}'",
            )
            if actual != expected_checksum:
                raise FileFailoverError(f"Probe checksum mismatch on {node_id}.")

    def cleanup_probes(self, control: Node, operation_id: str) -> None:
        self.remote(
            control,
            f"rm -rf {SHARED_ROOT}/.cluster-health/file-drills/{operation_id}",
            check=False,
        )

    def apply_single_brick_isolation(
        self,
        target: Node,
        operation_id: str,
        brick_port: int,
    ) -> None:
        """Block only Gluster overlay ports on one data node, never cluster quorum ports."""
        if not 1024 <= brick_port <= 65535:
            raise FileFailoverError("The resolved Gluster brick port is invalid.")
        self.remote(
            target,
            f"""
set -euo pipefail
test ! -e /run/massar-file-drill-active
test ! -e /run/massar-file-drill-recovery-required
if sudo nft list table inet {DRILL_TABLE} >/dev/null 2>&1; then
  echo 'a pre-existing file-drill nft table blocks safe execution' >&2
  exit 41
fi
printf '%s\n' {operation_id} | sudo tee /run/massar-file-drill-recovery-required >/dev/null
sudo chmod 0600 /run/massar-file-drill-recovery-required
printf '%s\n' {operation_id} | sudo tee /run/massar-file-drill-active >/dev/null
sudo chmod 0600 /run/massar-file-drill-active
sudo nft add table inet {DRILL_TABLE}
sudo nft 'add chain inet {DRILL_TABLE} input {{ type filter hook input priority -100; policy accept; }}'
sudo nft 'add rule inet {DRILL_TABLE} input iifname "wg0" tcp dport {{ 24007, 24008, {brick_port} }} reject with tcp reset'
""",
        )

    def wait_for_direct_port_isolation(
        self,
        control: Node,
        target: Node,
        *,
        target_port: int,
        control_port: int,
        timeout_seconds: int = 15,
    ) -> None:
        """Prove the target brick port is blocked while the control brick remains reachable."""
        deadline = self.monotonic() + timeout_seconds
        while self.monotonic() <= deadline:
            completed = self.transport.run(
                self.target(control),
                (
                    "bash",
                    "-lc",
                    f"""
set -euo pipefail
timeout 3 bash -lc 'exec 3<>/dev/tcp/{control.overlay_address}/{control_port}'
if timeout 3 bash -lc 'exec 3<>/dev/tcp/{target.overlay_address}/{target_port}' 2>/dev/null; then
  exit 41
fi
""",
                ),
                timeout_seconds=10,
                check=False,
            )
            if completed.returncode == 0:
                return
            self.sleep(1)
        raise FileFailoverError(
            "The target brick port did not become directly isolated within the bound."
        )

    def remove_single_brick_isolation(self, target: Node, operation_id: str) -> None:
        self.remote(
            target,
            f"""
set -euo pipefail
test "$(sudo cat /run/massar-file-drill-active 2>/dev/null || true)" = {operation_id}
sudo nft delete table inet {DRILL_TABLE}
if sudo nft list table inet {DRILL_TABLE} >/dev/null 2>&1; then
  echo 'file-drill nft table remained after recovery' >&2
  exit 42
fi
sudo rm -f /run/massar-file-drill-active
""",
        )

    def clear_recovery_marker(self, target: Node, operation_id: str) -> None:
        self.remote(
            target,
            f"""
set -euo pipefail
test "$(sudo cat /run/massar-file-drill-recovery-required 2>/dev/null || true)" = {operation_id}
sudo rm -f /run/massar-file-drill-recovery-required
""",
        )

    def wait_for_target_state(
        self,
        control: Node,
        target_node: str,
        *,
        online: bool,
        timeout_seconds: int,
    ) -> dict[str, BrickState]:
        deadline = self.monotonic() + timeout_seconds
        last: dict[str, BrickState] = {}
        while self.monotonic() <= deadline:
            try:
                last = self.brick_states(control)
                other_nodes_online = all(
                    state.online
                    for node_id, state in last.items()
                    if node_id != target_node
                )
                if last[target_node].online is online and other_nodes_online:
                    return last
            except (FileFailoverError, OSError):
                pass
            self.sleep(2)
        desired = "online" if online else "offline"
        raise FileFailoverError(f"Target brick did not become {desired} within the bound.")

    def wait_for_heal(self, control: Node) -> None:
        deadline = self.monotonic() + HEAL_TIMEOUT_SECONDS
        self.remote(control, f"sudo gluster volume heal {VOLUME}", check=False)
        while self.monotonic() <= deadline:
            summary = self.remote(
                control,
                f"sudo gluster volume heal {VOLUME} info summary",
                check=False,
            )
            split_brain = self.remote(
                control,
                f"sudo gluster volume heal {VOLUME} info split-brain",
                check=False,
            )
            summary_entries = re.findall(
                r"Total Number of entries:\s*(\d+)",
                summary,
            )
            split_entries = re.findall(
                r"Number of entries in split-brain:\s*(\d+)",
                split_brain,
            )
            if (
                len(summary_entries) >= 3
                and len(split_entries) >= 3
                and all(value == "0" for value in (*summary_entries, *split_entries))
            ):
                return
            self.sleep(2)
        raise FileFailoverError("Gluster heal or split-brain backlog did not clear.")

    def execute(self, target_node_id: str) -> DrillOutcome:
        target_node = validate_target(self.inventory, target_node_id)
        control = self.control_node(target_node_id)
        operation_id = str(uuid.uuid4())
        started_at = self.now()
        before_path = f".cluster-health/file-drills/{operation_id}/before.bin"
        during_path = f".cluster-health/file-drills/{operation_id}/during.bin"
        fault_attempted = False
        lock_acquired = False
        isolation_started: float | None = None
        isolation_observed_seconds: float | None = None
        recovery_seconds: float | None = None
        client_visible_outage_seconds: float | None = None
        acknowledged_loss_count = 0
        error: BaseException | None = None
        recovery_error: BaseException | None = None

        try:
            for node in self.inventory.nodes:
                self.inspect_node(node)
            prestate = self.brick_states(control)
            validate_healthy_prestate(prestate, target_node_id)
            self.acquire_shared_lock(control, operation_id)
            lock_acquired = True

            before_checksum = self.write_probe(control, before_path, os.urandom(64 * 1024))
            self.verify_probe(("node-1", "node-2", "node-3"), before_path, before_checksum)

            fault_attempted = True
            isolation_started = self.monotonic()
            self.apply_single_brick_isolation(
                target_node,
                operation_id,
                prestate[target_node_id].port,
            )
            self.wait_for_direct_port_isolation(
                control,
                target_node,
                target_port=prestate[target_node_id].port,
                control_port=prestate[control.id].port,
            )
            isolation_observed_seconds = self.monotonic() - isolation_started

            during_checksum = self.write_probe(control, during_path, os.urandom(64 * 1024))
            remaining = tuple(
                node.id for node in self.inventory.nodes if node.id != target_node_id
            )
            self.verify_probe(remaining, during_path, during_checksum)
            client_visible_outage_seconds = 0.0
        except BaseException as exc:  # recovery must also run on cancellation/interrupt
            error = exc
        finally:
            if fault_attempted:
                try:
                    recovery_started = self.monotonic()
                    self.remove_single_brick_isolation(target_node, operation_id)
                    self.wait_for_target_state(
                        control,
                        target_node_id,
                        online=True,
                        timeout_seconds=self.maximum_outage_seconds,
                    )
                    recovery_seconds = self.monotonic() - recovery_started
                    self.wait_for_heal(control)
                    if "before_checksum" in locals():
                        self.verify_probe(
                            ("node-1", "node-2", "node-3"),
                            before_path,
                            before_checksum,
                        )
                    if "during_checksum" in locals():
                        self.verify_probe(
                            ("node-1", "node-2", "node-3"),
                            during_path,
                            during_checksum,
                        )
                    self.clear_recovery_marker(target_node, operation_id)
                except BaseException as exc:
                    recovery_error = exc
            if lock_acquired:
                self.cleanup_probes(control, operation_id)
                self.release_shared_lock(control)

        if recovery_error is not None:
            error = FileFailoverError(
                f"Recovery failed after the bounded drill: {type(recovery_error).__name__}"
            )

        if error is None and (
            recovery_seconds is None
            or recovery_seconds > self.maximum_outage_seconds
        ):
            error = FileFailoverError("The file brick exceeded the maximum recovery bound.")

        completed_at = self.now()
        evidence: dict[str, object] = {
            "schemaVersion": 1,
            "service": "files",
            "formerNode": target_node_id,
            "newNode": None,
            "startedAt": iso8601(started_at),
            "recoveredAt": iso8601(completed_at),
            "result": "pass" if error is None else "fail",
            "acknowledgedLossCount": acknowledged_loss_count,
            "splitBrainDetected": False,
            "quorumEvidence": {
                "operationId": operation_id,
                "isolatedNodeCount": 1,
                "arbiterIsolated": False,
                "maximumOutageSeconds": self.maximum_outage_seconds,
                "observedOutageSeconds": client_visible_outage_seconds,
                "observedRecoverySeconds": recovery_seconds,
                "isolationObservedSeconds": isolation_observed_seconds,
                "resolvedBrickPort": (
                    prestate[target_node_id].port if "prestate" in locals() else None
                ),
                "precondition": "all-three-bricks-online",
                "fault": "single-data-brick-gluster-ports-only",
                "recoveryRequired": recovery_error is not None,
                "failureType": type(error).__name__ if error is not None else None,
            },
        }
        return DrillOutcome(evidence=evidence, error=error)


def dry_run_evidence(target_node: str, maximum_outage_seconds: int) -> dict[str, object]:
    now = utc_now()
    return {
        "schemaVersion": 1,
        "service": "files",
        "formerNode": target_node,
        "newNode": None,
        "startedAt": iso8601(now),
        "recoveredAt": iso8601(now),
        "result": "safe-refusal",
        "acknowledgedLossCount": 0,
        "splitBrainDetected": False,
        "quorumEvidence": {
            "dryRun": True,
            "isolatedNodeCount": 0,
            "plannedIsolatedNodeCount": 1,
            "arbiterIsolated": False,
            "maximumOutageSeconds": maximum_outage_seconds,
            "fault": "single-data-brick-gluster-ports-only",
        },
    }


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--known-hosts", type=Path)
    parser.add_argument("--identity", type=Path)
    parser.add_argument("--target-node", required=True)
    parser.add_argument("--evidence-output", required=True, type=Path)
    parser.add_argument(
        "--maximum-outage-seconds",
        type=int,
        default=120,
    )
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--dry-run", action="store_true")
    mode.add_argument("--yes", action="store_true")
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        inventory = load_drill_inventory(args.inventory)
        validate_target(inventory, args.target_node)
        if not MAXIMUM_OUTAGE_MIN_SECONDS <= args.maximum_outage_seconds <= MAXIMUM_OUTAGE_MAX_SECONDS:
            raise FileFailoverError(
                f"maximum outage must be between {MAXIMUM_OUTAGE_MIN_SECONDS} "
                f"and {MAXIMUM_OUTAGE_MAX_SECONDS} seconds"
            )
        if args.dry_run:
            evidence = dry_run_evidence(
                args.target_node,
                args.maximum_outage_seconds,
            )
            write_evidence(args.evidence_output, evidence)
            print(json.dumps({"status": "dry-run", "evidence": str(args.evidence_output)}))
            return 0

        if args.known_hosts is None or args.identity is None:
            raise FileFailoverError("--known-hosts and --identity are required with --yes")
        transport = StrictSshTransport(args.known_hosts, args.identity)
        outcome = FileFailoverDrill(
            inventory,
            transport,
            maximum_outage_seconds=args.maximum_outage_seconds,
        ).execute(args.target_node)
        write_evidence(args.evidence_output, outcome.evidence)
        if outcome.error is not None:
            print(
                f"file failover drill failed safely: {type(outcome.error).__name__}",
                file=sys.stderr,
            )
            return 130 if isinstance(outcome.error, KeyboardInterrupt) else 6
        print(json.dumps({"status": "success", "evidence": str(args.evidence_output)}))
        return 0
    except (FileFailoverError, OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"file failover drill blocked: {exc}", file=sys.stderr)
        return 6


if __name__ == "__main__":
    raise SystemExit(main())
