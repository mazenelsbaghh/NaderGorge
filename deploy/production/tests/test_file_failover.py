from __future__ import annotations

import importlib.util
import json
import subprocess
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))

SPEC = importlib.util.spec_from_file_location(
    "run_file_failover_drill",
    SCRIPTS / "run_file_failover_drill.py",
)
assert SPEC and SPEC.loader
runner = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = runner
SPEC.loader.exec_module(runner)


def inventory():
    return runner.load_drill_inventory(
        ROOT / "deploy/production/inventory/production.yml"
    )


def gluster_xml(*, offline: str | None = None) -> str:
    rows = []
    for index, node_id in enumerate(("node-1", "node-2", "node-3"), start=1):
        rows.append(
            "<node>"
            f"<hostname>{node_id}.cluster.internal</hostname>"
            "<path>/srv/gluster/massar/brick</path>"
            f"<port>{53000 + index}</port>"
            f"<status>{0 if node_id == offline else 1}</status>"
            "</node>"
        )
    return "<cliOutput><volStatus><volumes><volume>" + "".join(rows) + (
        "</volume></volumes></volStatus></cliOutput>"
    )


def test_file_failure_contract_requires_quorum_heal_checksum_and_isolated_restore() -> None:
    topology = (
        ROOT / "deploy/production/config/gluster/topology.json"
    ).read_text(encoding="utf-8")
    files = (
        ROOT / "deploy/production/scripts/manage_files.py"
    ).read_text(encoding="utf-8")
    restore = (
        ROOT / "deploy/production/scripts/restore_files_sample.sh"
    ).read_text(encoding="utf-8")
    assert '"arbiter_count": 1' in topology
    assert "cluster.quorum-type auto" in files
    assert "cluster.server-quorum-ratio 51%" in files
    assert "sha256sum" in files
    assert "gluster volume heal massar-shared info summary" in files
    assert "mktemp -d" in restore
    assert "restic check --read-data-subset=5%" in restore


def test_status_parser_requires_exact_three_approved_bricks() -> None:
    states = runner.parse_brick_status(gluster_xml(offline="node-1"))
    assert states["node-1"].online is False
    assert states["node-2"].online is True
    assert states["node-3"].online is True

    with pytest.raises(runner.FileFailoverError, match="exact three"):
        runner.parse_brick_status(
            gluster_xml().replace(
                "<node><hostname>node-3.cluster.internal</hostname>"
                "<path>/srv/gluster/massar/brick</path>"
                "<port>53003</port><status>1</status></node>",
                "",
            )
        )


def test_runner_refuses_arbiter_and_any_degraded_prestate() -> None:
    data = inventory()
    with pytest.raises(runner.FileFailoverError, match="arbiter"):
        runner.validate_target(data, "node-3")

    states = runner.parse_brick_status(gluster_xml(offline="node-2"))
    with pytest.raises(runner.FileFailoverError, match="already unavailable"):
        runner.validate_healthy_prestate(states, "node-1")


def test_runner_requires_one_explicit_mode_and_bounded_outage(tmp_path: Path) -> None:
    command = [
        sys.executable,
        str(SCRIPTS / "run_file_failover_drill.py"),
        "--inventory",
        str(ROOT / "deploy/production/inventory/production.yml"),
        "--target-node",
        "node-1",
        "--evidence-output",
        str(tmp_path / "evidence.json"),
    ]
    missing_confirmation = subprocess.run(
        command,
        text=True,
        capture_output=True,
        check=False,
    )
    assert missing_confirmation.returncode != 0

    invalid_bound = subprocess.run(
        [*command, "--maximum-outage-seconds", "600", "--dry-run"],
        text=True,
        capture_output=True,
        check=False,
    )
    assert invalid_bound.returncode == 6
    assert not (tmp_path / "evidence.json").exists()


def test_dry_run_needs_no_ssh_and_writes_non_mutating_evidence(tmp_path: Path) -> None:
    evidence_path = tmp_path / "file-failover.json"
    completed = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "run_file_failover_drill.py"),
            "--inventory",
            str(ROOT / "deploy/production/inventory/production.yml"),
            "--target-node",
            "node-1",
            "--evidence-output",
            str(evidence_path),
            "--dry-run",
        ],
        text=True,
        capture_output=True,
        check=False,
    )
    assert completed.returncode == 0, completed.stderr
    evidence = json.loads(evidence_path.read_text(encoding="utf-8"))
    assert evidence["result"] == "safe-refusal"
    assert evidence["quorumEvidence"]["dryRun"] is True
    assert evidence["quorumEvidence"]["isolatedNodeCount"] == 0
    assert evidence["quorumEvidence"]["plannedIsolatedNodeCount"] == 1
    assert evidence_path.stat().st_mode & 0o027 == 0


class FakeDrill(runner.FileFailoverDrill):
    def __init__(self, *, fail_after_isolation: bool = False) -> None:
        self.inventory = inventory()
        self.transport = None
        self.maximum_outage_seconds = 120
        self.sleep = lambda _seconds: None
        self.monotonic_value = 1_000.0
        self.monotonic = lambda: self.monotonic_value
        self.now = runner.utc_now
        self.by_id = {node.id: node for node in self.inventory.nodes}
        self.fail_after_isolation = fail_after_isolation
        self.events: list[str] = []
        self.fault_active = False

    def inspect_node(self, node):
        self.events.append(f"inspect:{node.id}")

    def brick_states(self, control):
        offline = "node-1" if self.fault_active else None
        return runner.parse_brick_status(gluster_xml(offline=offline))

    def acquire_shared_lock(self, control, operation_id):
        self.events.append("lock")

    def release_shared_lock(self, control):
        self.events.append("unlock")

    def write_probe(self, control, relative_path, content):
        self.events.append("write")
        return "a" * 64

    def verify_probe(self, node_ids, relative_path, expected_checksum):
        self.events.append("verify:" + ",".join(node_ids))
        if self.fail_after_isolation and self.fault_active:
            raise IOError("injected post-isolation failure")

    def cleanup_probes(self, control, operation_id):
        self.events.append("cleanup")

    def apply_single_brick_isolation(self, target, operation_id, brick_port):
        assert target.id == "node-1"
        assert brick_port == 53001
        self.events.append("isolate:node-1")
        self.fault_active = True

    def wait_for_direct_port_isolation(
        self,
        control,
        target,
        *,
        target_port,
        control_port,
        timeout_seconds=15,
    ):
        assert (control.id, target.id) == ("node-2", "node-1")
        assert (target_port, control_port) == (53001, 53002)
        self.events.append("direct-port-isolation")

    def remove_single_brick_isolation(self, target, operation_id):
        assert target.id == "node-1"
        self.events.append("recover:node-1")
        self.fault_active = False

    def clear_recovery_marker(self, target, operation_id):
        self.events.append("clear-recovery-marker")

    def wait_for_target_state(self, control, target_node, *, online, timeout_seconds):
        self.events.append(f"wait:{target_node}:{online}")
        if self.fault_active == online:
            raise AssertionError("fake state does not match requested state")
        self.monotonic_value += 5
        return self.brick_states(control)

    def wait_for_heal(self, control):
        self.events.append("heal")


def test_successful_drill_isolates_only_one_data_node_and_recovers() -> None:
    drill = FakeDrill()
    outcome = drill.execute("node-1")

    assert outcome.error is None
    assert outcome.evidence["result"] == "pass"
    assert outcome.evidence["acknowledgedLossCount"] == 0
    quorum = outcome.evidence["quorumEvidence"]
    assert quorum["isolatedNodeCount"] == 1
    assert quorum["arbiterIsolated"] is False
    assert drill.events.count("isolate:node-1") == 1
    assert drill.events.count("write") == 2
    assert drill.events.count("verify:node-1,node-2,node-3") == 3
    assert "verify:node-2,node-3" in drill.events
    assert drill.events.index("verify:node-2,node-3") < drill.events.index(
        "recover:node-1"
    )
    assert "recover:node-1" in drill.events
    assert drill.events.index("recover:node-1") < drill.events.index("heal")
    assert outcome.evidence["quorumEvidence"]["observedOutageSeconds"] == 0
    assert outcome.evidence["quorumEvidence"]["observedRecoverySeconds"] == 5
    assert outcome.evidence["quorumEvidence"]["resolvedBrickPort"] == 53001
    assert drill.events.index("heal") < drill.events.index(
        "verify:node-1,node-2,node-3",
        drill.events.index("heal"),
    )
    assert drill.events[-2:] == ["cleanup", "unlock"]


def test_heal_output_regex_matches_real_gluster_summary_format() -> None:
    sample = """
Brick node-1.cluster.internal:/srv/gluster/massar/brick
Status: Connected
Total Number of entries: 0
Number of entries in heal pending: 0
Number of entries in split-brain: 0
"""
    assert runner.re.findall(
        r"Total Number of entries:\s*(\d+)",
        sample,
    ) == ["0"]
    assert runner.re.findall(
        r"Number of entries in split-brain:\s*(\d+)",
        sample,
    ) == ["0"]


def test_recovery_runs_when_probe_fails_after_isolation() -> None:
    drill = FakeDrill(fail_after_isolation=True)
    outcome = drill.execute("node-1")

    assert outcome.error is not None
    assert outcome.evidence["result"] == "fail"
    assert "recover:node-1" in drill.events
    assert "heal" in drill.events
    assert drill.events[-2:] == ["cleanup", "unlock"]


def test_fault_command_cannot_stop_services_or_target_cluster_quorum() -> None:
    source = (SCRIPTS / "run_file_failover_drill.py").read_text(encoding="utf-8")
    assert "systemctl stop" not in source
    assert "file-arbiter" in source
    assert "isolatedNodeCount" in source
    assert "if sudo nft list table" in source
    assert 'test "$(sudo cat /run/massar-file-drill-active' in source
    assert 'test "$(sudo cat /run/massar-file-drill-recovery-required' in source
    assert "file-drill nft table remained after recovery" in source
    assert "24007, 24008, {brick_port}" in source
    assert "reject with tcp reset" in source
    assert "49152-49251" not in source
    assert "wait_for_direct_port_isolation" in source
    for forbidden_port in ("2379", "2380", "5432", "6379", "26379"):
        assert forbidden_port not in source
    for address in ("191.218.161.76", "191.218.161.78", "168.231.106.230"):
        assert address not in source


def test_drill_sudoers_allows_only_the_missing_exact_gluster_commands() -> None:
    sudoers = (
        ROOT
        / "deploy/production/config/sudoers/massar-file-failover-drill"
    ).read_text(encoding="utf-8")
    assert sudoers.strip() == (
        "massar-ops ALL=(root) NOPASSWD: "
        "/usr/sbin/gluster volume status massar-shared --xml, "
        "/usr/sbin/gluster volume get massar-shared network.ping-timeout, "
        "/usr/sbin/gluster volume heal massar-shared, "
        "/usr/bin/cat /run/massar-file-drill-active, "
        "/usr/bin/cat /run/massar-file-drill-recovery-required, "
        "/usr/bin/rm -f /run/massar-file-drill-active, "
        "/usr/bin/rm -f /run/massar-file-drill-recovery-required"
    )
    assert "*" not in sudoers
    assert "peer " not in sudoers
    assert "volume stop" not in sudoers
    assert "volume delete" not in sudoers


def test_hourly_backup_and_isolated_restore_are_acceptance_gates() -> None:
    backup_timer = (
        ROOT / "deploy/production/systemd/massar-files-backup.timer"
    ).read_text(encoding="utf-8")
    restore_timer = (
        ROOT / "deploy/production/systemd/massar-files-restore-test.timer"
    ).read_text(encoding="utf-8")
    restore = (
        ROOT / "deploy/production/scripts/restore_files_sample.sh"
    ).read_text(encoding="utf-8")

    assert "OnCalendar=hourly" in backup_timer
    assert "OnCalendar=*-*-01 06:00:00 Africa/Cairo" in restore_timer
    assert "restic check --read-data-subset=5%" in restore
    assert 'restic restore "$snapshot_id"' in restore
    assert '"isolated":True' in restore
    assert '"productionTarget":False' in restore
    assert '"checksumVerified":True' in restore
