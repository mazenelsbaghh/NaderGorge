from __future__ import annotations

import importlib.util
import sys
from dataclasses import dataclass
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "deploy_release_reverse_tests",
    SCRIPTS / "deploy_release.py",
)
assert SPEC and SPEC.loader
deploy = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = deploy
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


def inventory() -> Inventory:
    return Inventory(
        nodes=tuple(
            Node(f"node-{index}", f"192.0.2.{index}", f"10.77.0.1{index}")
            for index in (1, 2, 3)
        ),
        cluster={"ssh_user": "massar-ops"},
    )


def configure_main_boundaries(
    monkeypatch: pytest.MonkeyPatch,
    rollout_state: dict[str, object],
) -> None:
    candidate_release = str(rollout_state["candidate_release"])
    cluster_inventory = inventory()

    class SuccessfulLock:
        def __init__(self, *_args, **_kwargs) -> None:
            pass

        def acquire(self) -> None:
            pass

        def release(self) -> None:
            pass

    def read_node_release(_transport, target, _overlay_address):
        return rollout_state["releases"][target.node_id]

    def write_traffic(*args):
        node_id, action = args[4], args[5]
        rollout_state["traffic_actions"].append((node_id, action))
        rollout_state["traffic_states"][node_id] = (
            "DRAIN" if action == "drain" else "UP"
        )

    def check_quorum(**kwargs):
        rollout_state["quorum_calls"] += 1
        if (
            rollout_state["fail_post_drain"]
            and kwargs["require_drained"]
        ):
            rollout_state["fail_post_drain"] = False
            raise deploy.DeployError("post-drain quorum failure")
        return {}

    def deploy_candidate(_transport, target, *_args):
        rollout_state["deploy_calls"].append(target.node_id)
        rollout_state["releases"][target.node_id] = candidate_release
        rollout_state["markers"].add(target.node_id)
        return "{}"

    def clear_marker(_transport, target, _release):
        rollout_state["clear_calls"].append(target.node_id)
        if (
            target.node_id == rollout_state["fail_clear_node"]
            and not rollout_state["clear_failure_consumed"]
        ):
            rollout_state["clear_failure_consumed"] = True
            raise deploy.DeployError(f"cleanup failed for {target.node_id}")
        rollout_state["markers"].discard(target.node_id)

    def prune_artifacts(
        _transport,
        target,
        current_release,
        rollback_release,
    ):
        rollout_state["prune_calls"].append(
            (target.node_id, current_release, rollback_release)
        )
        if (
            target.node_id == rollout_state["fail_prune_node"]
            and not rollout_state["prune_failure_consumed"]
        ):
            rollout_state["prune_failure_consumed"] = True
            raise deploy.DeployError(f"prune failed for {target.node_id}")
        return {
            "status": "pruned",
            "nodeId": target.node_id,
            "currentReleaseId": current_release,
            "rollbackReleaseId": rollback_release,
        }

    def preview_cleanup(_transport, target, current_release):
        rollout_state["preview_calls"].append((target.node_id, current_release))
        return {
            "status": "dry-run",
            "nodeId": target.node_id,
            "currentReleaseId": current_release,
            "rollbackReleaseId": current_release,
        }

    migration_gate = SimpleNamespace(
        current_release_id="prod-20260726-166-r1",
        post_migration_ids_sha256="b" * 64,
        post_migration_schema_sha256="c" * 64,
        database_system_identifier="7586552109940137719",
    )
    monkeypatch.setattr(deploy, "load_inventory", lambda _path: cluster_inventory)
    monkeypatch.setattr(
        deploy,
        "load_release_manifest",
        lambda _path, _release: SimpleNamespace(
            images={
                name: f"sha256:{index:064x}"
                for index, name in enumerate(
                    ("backend", "frontend", "worker", "migrator"), 1
                )
            },
            sha256="d" * 64,
        ),
    )
    monkeypatch.setattr(
        deploy,
        "load_migration_safety_gate",
        lambda *_args, **_kwargs: migration_gate,
    )
    monkeypatch.setattr(deploy, "StrictSshTransport", lambda *_args: object())
    monkeypatch.setattr(deploy, "RolloutLock", SuccessfulLock)
    monkeypatch.setattr(
        deploy,
        "reconcile_inconsistent_ingress_traffic",
        lambda **_kwargs: (),
    )
    monkeypatch.setattr(deploy, "node_ready", read_node_release)
    monkeypatch.setattr(
        deploy,
        "matching_recovery_marker",
        lambda _transport, target, _release: target.node_id
        in rollout_state["markers"],
    )
    monkeypatch.setattr(deploy, "assert_rollout_quorum", check_quorum)
    monkeypatch.setattr(deploy, "traffic", write_traffic)
    monkeypatch.setattr(deploy, "deploy_node", deploy_candidate)
    monkeypatch.setattr(deploy, "clear_recovery_marker", clear_marker)
    monkeypatch.setattr(deploy, "preview_release_artifact_cleanup", preview_cleanup)
    monkeypatch.setattr(deploy, "prune_release_artifacts", prune_artifacts)
    monkeypatch.setattr(
        sys,
        "argv",
        [
            "deploy_release.py",
            "--inventory", "/tmp/inventory.yml",
            "--known-hosts", "/tmp/known-hosts",
            "--identity", "/tmp/identity",
            "--release", candidate_release,
            "--manifest", "/tmp/manifest.json",
            "--backup-evidence", "/tmp/backup.json",
            "--yes",
        ],
    )


def rollout_state(
    *,
    fail_clear_node: str | None = None,
    fail_prune_node: str | None = None,
    fail_post_drain: bool = False,
) -> dict[str, object]:
    return {
        "candidate_release": "git-" + "a" * 40,
        "releases": {
            node_id: "prod-20260726-166-r1"
            for node_id in ("node-1", "node-2", "node-3")
        },
        "markers": set(),
        "traffic_states": {
            node_id: "UP" for node_id in ("node-1", "node-2", "node-3")
        },
        "traffic_actions": [],
        "deploy_calls": [],
        "clear_calls": [],
        "preview_calls": [],
        "prune_calls": [],
        "quorum_calls": 0,
        "fail_clear_node": fail_clear_node,
        "clear_failure_consumed": False,
        "fail_prune_node": fail_prune_node,
        "prune_failure_consumed": False,
        "fail_post_drain": fail_post_drain,
    }


@pytest.mark.parametrize(
    ("failed_node", "rollback_order"),
    [
        ("node-2", ("node-2", "node-3")),
        ("node-1", ("node-1", "node-2", "node-3")),
    ],
)
def test_failure_rolls_back_every_advanced_node_in_reverse_order(
    failed_node: str,
    rollback_order: tuple[str, ...],
) -> None:
    advanced = {
        "node-2": ["node-3"],
        "node-1": ["node-3", "node-2"],
    }[failed_node]
    assert deploy.reverse_rollback_order(
        advanced,
        failed_node,
        True,
    ) == rollback_order
    events: list[str] = []

    def traffic(*_args, **_kwargs):
        node_id = _args[4]
        action = _args[5]
        events.append(f"traffic:{action}:{node_id}")
        return None

    def quorum(**kwargs):
        events.append(f"quorum:{kwargs['rollout_node']}")
        return {}

    def recover(**kwargs):
        node_id = kwargs["target"].node_id
        events.append(f"recover:{node_id}")
        assert kwargs["retained_schema"].schema_sha256 == "c" * 64
        kwargs["traffic_writer"](
            kwargs["root"],
            kwargs["inventory_path"],
            kwargs["known_hosts"],
            kwargs["identity"],
            node_id,
            "undrain",
        )
        return "git-" + "d" * 40

    restored = deploy.rollback_nodes_in_reverse(
        node_ids=rollback_order,
        failed_node_id=failed_node,
        release_id="git-" + "a" * 40,
        retained_schema=deploy.RetainedSchema(
            "7586552109940137719",
            "b" * 64,
            "c" * 64,
        ),
        root=ROOT,
        inventory_path=ROOT / "deploy/production/inventory/production.yml",
        known_hosts=Path("/private/known-hosts"),
        identity=Path("/private/identity"),
        inventory=inventory(),
        transport=object(),
        traffic_writer=traffic,
        quorum_checker=quorum,
        recovery=recover,
    )

    assert tuple(restored) == rollback_order
    assert [
        event.removeprefix("recover:")
        for event in events
        if event.startswith("recover:")
    ] == list(rollback_order)
    assert f"traffic:drain:{failed_node}" not in events
    for node_id in rollback_order[1:]:
        assert events.index(f"traffic:drain:{node_id}") < events.index(
            f"recover:{node_id}"
        )


def test_success_markers_are_retained_until_all_nodes_advance() -> None:
    source = (SCRIPTS / "deploy_release.py").read_text(encoding="utf-8")
    deploy_body = source.split("def deploy_node(", 1)[1].split("def main(", 1)[0]
    marker_cleanup = source.split("def clear_recovery_marker(", 1)[1].split(
        "def deploy_node(", 1
    )[0]
    assert "stage=\"remove-recovery-marker\"" not in deploy_body
    assert "for node_id in ROLLING_ORDER" in source
    assert "clear_recovery_marker(" in source
    assert "tuple(reversed(advanced_nodes))" in source
    assert "not path.exists() and not path.is_symlink()" in marker_cleanup


def test_success_prunes_all_nodes_after_recovery_markers_are_cleared(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_rollout = rollout_state()
    configure_main_boundaries(monkeypatch, current_rollout)

    assert deploy.main() == 0

    assert current_rollout["clear_calls"] == list(deploy.ROLLING_ORDER)
    assert current_rollout["preview_calls"] == [
        (node_id, "prod-20260726-166-r1")
        for node_id in ("node-1", "node-2", "node-3")
    ]
    assert current_rollout["prune_calls"] == [
        (
            node_id,
            current_rollout["candidate_release"],
            "prod-20260726-166-r1",
        )
        for node_id in deploy.ROLLING_ORDER
    ]


@pytest.mark.parametrize("failed_cleanup_node", ["node-3", "node-2"])
def test_cleanup_failure_retry_finishes_same_release_without_redeploying(
    monkeypatch: pytest.MonkeyPatch,
    failed_cleanup_node: str,
) -> None:
    current_rollout = rollout_state(fail_clear_node=failed_cleanup_node)
    configure_main_boundaries(monkeypatch, current_rollout)

    with pytest.raises(deploy.DeployError, match="cleanup failed"):
        deploy.main()

    candidate_release = current_rollout["candidate_release"]
    assert set(current_rollout["releases"].values()) == {candidate_release}
    assert current_rollout["deploy_calls"] == ["node-3", "node-2", "node-1"]

    assert deploy.main() == 0
    assert set(current_rollout["releases"].values()) == {candidate_release}
    assert current_rollout["deploy_calls"] == ["node-3", "node-2", "node-1"]
    assert current_rollout["markers"] == set()


def test_prune_failure_retry_finishes_same_release_without_rollback(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_rollout = rollout_state(fail_prune_node="node-2")
    configure_main_boundaries(monkeypatch, current_rollout)

    with pytest.raises(deploy.DeployError, match="prune failed"):
        deploy.main()

    candidate_release = current_rollout["candidate_release"]
    assert set(current_rollout["releases"].values()) == {candidate_release}
    assert current_rollout["deploy_calls"] == ["node-3", "node-2", "node-1"]
    assert current_rollout["markers"] == set()

    assert deploy.main() == 0
    assert current_rollout["deploy_calls"] == ["node-3", "node-2", "node-1"]
    assert set(current_rollout["releases"].values()) == {candidate_release}


def test_interrupted_rollout_resumes_healthy_marked_node_without_redeploying(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_rollout = rollout_state()
    candidate_release = current_rollout["candidate_release"]
    current_rollout["releases"]["node-3"] = candidate_release
    current_rollout["markers"].add("node-3")
    configure_main_boundaries(monkeypatch, current_rollout)

    assert deploy.main() == 0
    assert current_rollout["deploy_calls"] == ["node-2", "node-1"]
    assert set(current_rollout["releases"].values()) == {candidate_release}
    assert current_rollout["markers"] == set()
    assert current_rollout["preview_calls"] == [
        ("node-1", "prod-20260726-166-r1"),
        ("node-2", "prod-20260726-166-r1"),
        ("node-3", candidate_release),
    ]


def test_post_drain_predeploy_failure_returns_unchanged_node_to_service(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_rollout = rollout_state(fail_post_drain=True)
    configure_main_boundaries(monkeypatch, current_rollout)

    with pytest.raises(deploy.DeployError, match="post-drain quorum failure"):
        deploy.main()

    assert current_rollout["deploy_calls"] == []
    assert current_rollout["traffic_actions"] == [
        ("node-3", "drain"),
        ("node-3", "undrain"),
    ]
    assert set(current_rollout["traffic_states"].values()) == {"UP"}
