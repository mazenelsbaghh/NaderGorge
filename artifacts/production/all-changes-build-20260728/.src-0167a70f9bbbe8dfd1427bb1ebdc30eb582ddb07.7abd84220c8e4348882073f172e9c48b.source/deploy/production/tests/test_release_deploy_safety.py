from __future__ import annotations

import importlib.util
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "deploy_release_safety",
    SCRIPTS / "deploy_release.py",
)
assert SPEC and SPEC.loader
deploy = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(deploy)


@dataclass(frozen=True)
class Node:
    id: str
    public_address: str
    overlay_address: str


@dataclass(frozen=True)
class Inventory:
    nodes: tuple[Node, ...]
    cluster: dict[str, str]


@dataclass
class Result:
    returncode: int = 0
    stdout: str = ""
    stderr: str = ""


class FakeTransport:
    def __init__(self, results: list[Result] | None = None) -> None:
        self.results = list(results or [])
        self.commands: list[tuple[object, ...]] = []

    def run(self, target, command, **kwargs):
        self.commands.append(command)
        return self.results.pop(0) if self.results else Result()


def inventory() -> Inventory:
    return Inventory(
        nodes=tuple(
            Node(f"node-{index}", f"192.0.2.{index}", f"10.77.0.1{index}")
            for index in (1, 2, 3)
        ),
        cluster={"ssh_user": "massar-ops"},
    )


def all_ingresses(statuses: dict[str, str]):
    def reader(root, inventory_path, known_hosts, identity, node_id, action):
        assert action == "status"
        return {
            ingress: statuses[node_id]
            for ingress in ("node-1", "node-2", "node-3")
        }
    return reader


def ready(*args) -> str:
    return "prod-20260726-166-r1"


def quorum_kwargs() -> dict[str, object]:
    return {
        "root": ROOT,
        "inventory_path": ROOT / "deploy/production/inventory/production.yml",
        "known_hosts": Path("/private/known-hosts"),
        "identity": Path("/private/identity"),
        "inventory": inventory(),
        "transport": FakeTransport(),
        "rollout_node": "node-3",
        "readiness_reader": ready,
    }


def test_quorum_blocks_before_drain_when_another_node_is_not_up() -> None:
    statuses = {"node-1": "UP", "node-2": "DRAIN", "node-3": "UP"}
    with pytest.raises(deploy.DeployError, match="node-2"):
        deploy.assert_rollout_quorum(
            **quorum_kwargs(),
            require_drained=False,
            traffic_reader=all_ingresses(statuses),
        )


def test_post_drain_requires_exact_target_maint_on_every_ingress() -> None:
    states = {
        "node-1": {"node-1": "UP", "node-2": "UP", "node-3": "UP"},
        "node-2": {"node-1": "UP", "node-2": "UP", "node-3": "UP"},
        "node-3": {"node-1": "DRAIN", "node-2": "UP", "node-3": "DRAIN"},
    }

    def reader(root, inventory_path, known_hosts, identity, node_id, action):
        return states[node_id]

    with pytest.raises(deploy.DeployError, match="node-3"):
        deploy.assert_rollout_quorum(
            **quorum_kwargs(),
            require_drained=True,
            traffic_reader=reader,
        )


def test_rollout_lock_blocks_conflict_and_releases_only_owned_lock() -> None:
    conflict = FakeTransport([Result(returncode=75)])
    lock = deploy.RolloutLock(
        conflict,
        deploy.SshTarget("node-1", "192.0.2.1", "massar-ops"),
        "00000000-0000-4000-8000-000000000001",
    )
    with pytest.raises(deploy.DeployError, match="another rollout"):
        lock.acquire()

    transport = FakeTransport([Result(), Result()])
    lock = deploy.RolloutLock(
        transport,
        deploy.SshTarget("node-1", "192.0.2.1", "massar-ops"),
        "00000000-0000-4000-8000-000000000002",
    )
    lock.acquire()
    lock.release()
    assert len(transport.commands) == 2
    assert "00000000-0000-4000-8000-000000000002" in transport.commands[0][-1]
    assert "/var/lib/massar/rollout-locks/release-rollout.lock" in transport.commands[0][-1]
    assert "sudo mkdir" not in transport.commands[0][-1]
    assert "sudo rmdir" not in transport.commands[1][-1]
    assert "rmdir /var/lib/massar/rollout-locks/release-rollout.lock" in transport.commands[1][-1]


def test_deploy_node_requires_every_service_shared_write_and_atomic_pointer() -> None:
    transport = FakeTransport([Result(stdout='{"Name":"backend"}')])
    node = inventory().nodes[2]
    images = {
        name: f"sha256:{index:064x}"
        for index, name in enumerate(("backend", "frontend", "worker", "migrator"), 1)
    }
    deploy.deploy_node(
        transport,
        deploy.SshTarget(node.id, node.public_address, "massar-ops"),
        node,
        "git-" + "a" * 40,
        images,
        "b" * 64,
        "00000000-0000-4000-8000-000000000003",
    )
    script = transport.commands[0][-1]
    for service in (
        "backend", "worker", "landing", "student",
        "admin", "teacher", "staff", "gateway",
    ):
        assert service in script
    assert "MASSAR_SHARED_GID" in script
    assert '--env-file "$runtime_env"' in script
    assert 'runtime_env="/tmp/massar-runtime-' in script
    assert 'rm -f "$runtime_env"' in script
    assert "compose_state=\"$(compose ps --format json)\"" in script
    assert "compose rm --stop --force release-evidence" in script
    assert "compose up -d --no-build --remove-orphans backend worker" in script
    assert "/shared/public/.massar-worker-write-" in script
    assert "docker exec --user 10001:10001" in script
    assert "id -G" in script
    assert 'printf ready > "$1"' in script
    assert 'docker exec "$worker_id" rm -f "$probe"' in script
    assert 'docker exec "$worker_id" sh -ec' not in script
    assert "deploy-recovery" in script
    assert "massar-normalize-current-release switch" in script
    assert "sudo mv -Tf" not in script
    assert "sudo tee" not in script
    assert "rm -f /var/lib/massar/deploy-recovery" in script
    assert "10.77.0.13:8080" in script
    assert "127.0.0.1:8088/__node_ready" not in script


def test_failure_reports_exact_drained_node_recovery_marker() -> None:
    release = "git-" + "a" * 40
    error = deploy.node_recovery_error("node-3", release)
    assert str(error) == (
        "rollout stopped with node-3 drained; recovery marker: "
        f"/var/lib/massar/deploy-recovery/{release}-node-3.json"
    )


def test_failed_node_recovery_restores_old_release_then_undrains(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    events: list[str] = []
    node = inventory().nodes[2]
    monkeypatch.setattr(
        deploy,
        "recover_node",
        lambda transport, target, value, release: (
            events.append("restore-old") or "prod-20260726-166-r1"
        ),
    )

    def write_traffic(root, inventory_path, known_hosts, identity, node_id, action):
        events.append(f"traffic:{action}")
        return None

    def check_quorum(**kwargs):
        events.append("quorum")
        return {}

    restored = deploy.recover_failed_node(
        transport=FakeTransport(),
        target=deploy.SshTarget(node.id, node.public_address, "massar-ops"),
        node=node,
        release_id="git-" + "a" * 40,
        root=ROOT,
        inventory_path=ROOT / "deploy/production/inventory/production.yml",
        known_hosts=Path("/private/known-hosts"),
        identity=Path("/private/identity"),
        inventory=inventory(),
        traffic_writer=write_traffic,
        quorum_checker=check_quorum,
    )

    assert restored == "prod-20260726-166-r1"
    assert events == ["restore-old", "traffic:undrain", "quorum"]


def test_rollback_prestate_checks_all_manifests_and_live_database_hashes() -> None:
    transport = FakeTransport()
    gate = deploy.RollbackCompatibilityGate(
        current_release_id="git-" + "a" * 40,
        current_manifest_sha256="b" * 64,
        target_release_id="prod-20260726-166-r1",
        target_manifest_sha256="c" * 64,
        database_system_identifier="7586552109940137719",
        migration_ids_sha256="d" * 64,
        schema_sha256="e" * 64,
    )
    deploy.verify_rollback_prestate(
        inventory=inventory(),
        transport=transport,
        current_manifest=SimpleNamespace(
            release_id=gate.current_release_id,
            sha256=gate.current_manifest_sha256,
        ),
        gate=gate,
    )
    assert len(transport.commands) == 4
    for command in transport.commands[:3]:
        script = command[-1]
        assert "/opt/massar/current/manifest.json" in script
        assert gate.current_manifest_sha256 in script
        assert gate.current_release_id in script
        assert "/__node_ready" in script
        assert subprocess.run(
            ["bash", "-n"],
            input=script,
            text=True,
            capture_output=True,
            check=False,
        ).returncode == 0
    database_script = transport.commands[3][-1]
    assert "pg_control_system" in database_script
    assert "__EFMigrationsHistory" in database_script
    assert "pg_dump" in database_script
    assert "/^.(un)?restrict[[:space:]]/d" in database_script
    assert gate.database_system_identifier in database_script
    assert gate.migration_ids_sha256 in database_script
    assert gate.schema_sha256 in database_script
    assert subprocess.run(
        ["bash", "-n"],
        input=database_script,
        text=True,
        capture_output=True,
        check=False,
    ).returncode == 0
