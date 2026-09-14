from __future__ import annotations

import importlib.util
import json
import os
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest


ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "deploy/production/scripts/clusterctl.py"
SPEC = importlib.util.spec_from_file_location("clusterctl", MODULE_PATH)
assert SPEC and SPEC.loader
clusterctl = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = clusterctl
SPEC.loader.exec_module(clusterctl)
INVENTORY = ROOT / "deploy/production/inventory/production.yml"


def with_operator_refs(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    known_hosts = tmp_path / "known_hosts"
    identity = tmp_path / "id_ed25519"
    known_hosts.write_text("pinned\n")
    identity.write_text("private\n")
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE", str(known_hosts))
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE", str(identity))


def test_inventory_is_exactly_three_unique_approved_nodes(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    inventory = clusterctl.load_inventory(INVENTORY, require_operator_files=True)
    assert [node.id for node in inventory.nodes] == ["node-1", "node-2", "node-3"]
    assert len({node.public_address for node in inventory.nodes}) == 3
    assert len({node.overlay_address for node in inventory.nodes}) == 3
    assert len(inventory.hostnames) == 8
    assert [node.id for node in inventory.nodes if "builder" in node.roles] == ["node-3"]


@pytest.mark.parametrize(
    ("mutate", "expected"),
    [
        (lambda raw: raw["nodes"][2].update({"roles": raw["nodes"][2]["roles"][:-1]}), "exactly one builder"),
        (lambda raw: raw["nodes"][0]["roles"].append("builder"), "exactly one builder"),
        (
            lambda raw: (
                raw["nodes"][2].update({"roles": [role for role in raw["nodes"][2]["roles"] if role != "builder"]}),
                raw["nodes"][1]["roles"].append("builder"),
            ),
            "exactly one builder role to node-3",
        ),
    ],
)
def test_inventory_requires_node_3_as_the_only_builder(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    mutate,
    expected: str,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    raw = json.loads(INVENTORY.read_text())
    mutate(raw)
    path = tmp_path / "bad-builder-inventory.yml"
    path.write_text(json.dumps(raw))
    with pytest.raises(ValueError, match=expected):
        clusterctl.load_inventory(path)


def test_inventory_rejects_secret_like_keys(tmp_path: Path) -> None:
    bad = tmp_path / "bad.yml"
    raw = json.loads(INVENTORY.read_text())
    raw["password"] = "unsafe"
    text = json.dumps(raw)
    bad.write_text(text)
    with pytest.raises(ValueError, match="secret-like"):
        clusterctl.load_inventory(bad)


def test_state_change_requires_confirmation(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "deploy",
        "--node", "node-1", "--evidence-dir", str(tmp_path / "evidence"),
    ])
    assert result == clusterctl.EXIT_SAFETY


def test_dry_run_writes_redacted_valid_evidence(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    directory = tmp_path / "evidence"
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "backup",
        "--node", "all", "--dry-run", "--evidence-dir", str(directory),
    ])
    assert result == 0
    files = list(directory.glob("*.json"))
    assert len(files) == 1
    payload = json.loads(files[0].read_text())
    assert payload["status"] == "dry-run"
    assert payload["targets"] == ["node-1", "node-2", "node-3"]
    assert "private" not in files[0].read_text().lower()


@pytest.mark.parametrize("command", ["deploy", "accept"])
def test_dry_run_refuses_incomplete_operation(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    command: str,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), command,
        "--node", "all", "--dry-run",
        "--evidence-dir", str(tmp_path / command),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(next((tmp_path / command).glob("*.json")).read_text())
    assert payload["status"] == "blocked"
    assert payload["reason"]


def test_unknown_target_is_rejected_by_parser() -> None:
    with pytest.raises(SystemExit):
        clusterctl.parser().parse_args([
            "--inventory", str(INVENTORY), "audit", "--node", "node-9",
        ])


def test_pitr_probe_must_cover_dynamic_writer(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "prepare-pitr-probe",
        "--node", "node-2", "--dry-run",
        "--evidence-dir", str(tmp_path / "pitr"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(next((tmp_path / "pitr").glob("*.json")).read_text())
    assert "current writer" in payload["reason"]


def test_core_failover_requires_cluster_wide_target_for_dynamic_leaders(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "failover-test",
        "--node", "node-1", "--dry-run",
        "--evidence-dir", str(tmp_path / "failover"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(next((tmp_path / "failover").glob("*.json")).read_text())
    assert payload["targets"] == ["node-1"]
    assert "requires --node all" in payload["reason"]
    assert "dynamically" in payload["reason"]


def test_core_failover_cluster_wide_dry_run_names_all_possible_targets(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "failover-test",
        "--node", "all", "--dry-run",
        "--evidence-dir", str(tmp_path / "failover"),
    ])
    assert result == clusterctl.EXIT_OK
    payload = json.loads(next((tmp_path / "failover").glob("*.json")).read_text())
    assert payload["status"] == "dry-run"
    assert payload["targets"] == ["node-1", "node-2", "node-3"]


def test_file_failover_accepts_one_data_node_and_refuses_arbiter(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    accepted = clusterctl.main([
        "--inventory", str(INVENTORY), "file-failover-test",
        "--node", "node-2", "--dry-run",
        "--maximum-outage-seconds", "120",
        "--evidence-dir", str(tmp_path / "accepted"),
    ])
    refused = clusterctl.main([
        "--inventory", str(INVENTORY), "file-failover-test",
        "--node", "node-3", "--dry-run",
        "--evidence-dir", str(tmp_path / "refused"),
    ])
    assert accepted == clusterctl.EXIT_OK
    accepted_payload = json.loads(
        next((tmp_path / "accepted").glob("*.json")).read_text()
    )
    assert accepted_payload["targets"] == ["node-2"]
    assert refused == clusterctl.EXIT_PREFLIGHT
    refused_payload = json.loads(
        next((tmp_path / "refused").glob("*.json")).read_text()
    )
    assert "arbiter" in refused_payload["reason"]


def test_rollback_dispatches_exact_compatibility_inputs_without_backup_gate(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    args = clusterctl.parser().parse_args([
        "--inventory", str(INVENTORY), "rollback",
        "--node", "all",
        "--release", "prod-20260726-166-r1",
        "--manifest", str(tmp_path / "target-manifest.json"),
        "--current-manifest", str(tmp_path / "current-manifest.json"),
        "--compatibility-evidence", str(tmp_path / "compatibility.json"),
        "--yes",
    ])
    inventory = clusterctl.load_inventory(INVENTORY)
    monkeypatch.setattr(clusterctl, "operator_transport", lambda _inventory: object())
    observed: list[str] = []

    def run(command, **_kwargs):
        observed.extend(str(item) for item in command)
        return SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(clusterctl.subprocess, "run", run)
    status, reason = clusterctl.execute(args, inventory, inventory.nodes)
    assert (status, reason) == ("success", None)
    assert "rollback_release.py" in " ".join(observed)
    assert "--current-manifest" in observed
    assert "--compatibility-evidence" in observed
    assert "--backup-evidence" not in observed


def test_current_manifest_collection_dry_run_requires_both_local_outputs(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "collect-current-manifest",
        "--node", "all", "--dry-run",
        "--manifest-output", str(tmp_path / "manifest.json"),
        "--evidence-dir", str(tmp_path / "operation"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(next((tmp_path / "operation").glob("*.json")).read_text())
    assert "--manifest-output and --output" in payload["reason"]


def test_current_manifest_collection_refuses_partial_node_scope(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "collect-current-manifest",
        "--node", "node-1", "--dry-run",
        "--manifest-output", str(tmp_path / "manifest.json"),
        "--output", str(tmp_path / "evidence.json"),
        "--evidence-dir", str(tmp_path / "operation"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(next((tmp_path / "operation").glob("*.json")).read_text())
    assert "requires --node all" in payload["reason"]


def test_current_manifest_collection_dry_run_refuses_existing_or_same_output(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    existing = tmp_path / "existing.json"
    existing.write_text("preserve", encoding="utf-8")
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "collect-current-manifest",
        "--node", "all", "--dry-run",
        "--manifest-output", str(existing),
        "--output", str(tmp_path / "evidence.json"),
        "--evidence-dir", str(tmp_path / "operation-existing"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(
        next((tmp_path / "operation-existing").glob("*.json")).read_text()
    )
    assert "already exists" in payload["reason"]

    same = tmp_path / "same.json"
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "collect-current-manifest",
        "--node", "all", "--dry-run",
        "--manifest-output", str(same),
        "--output", str(same),
        "--evidence-dir", str(tmp_path / "operation-same"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(
        next((tmp_path / "operation-same").glob("*.json")).read_text()
    )
    assert "must differ" in payload["reason"]


def test_current_manifest_collection_dispatches_reviewed_read_only_collector(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    args = clusterctl.parser().parse_args([
        "--inventory", str(INVENTORY), "collect-current-manifest",
        "--node", "all",
        "--manifest-output", str(tmp_path / "manifest.json"),
        "--output", str(tmp_path / "evidence.json"),
    ])
    inventory = clusterctl.load_inventory(INVENTORY)
    monkeypatch.setattr(clusterctl, "operator_transport", lambda _inventory: object())
    observed: list[str] = []

    def run(command, **_kwargs):
        observed.extend(str(item) for item in command)
        return SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(clusterctl.subprocess, "run", run)
    status, reason = clusterctl.execute(args, inventory, inventory.nodes)
    assert (status, reason) == ("success", None)
    assert "collect_current_release_manifest.py" in " ".join(observed)
    assert "--manifest-output" in observed
    assert "--evidence-output" in observed
    assert "--yes" not in observed


def test_current_manifest_normalization_requires_all_nodes_and_all_evidence(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "normalize-current-manifest",
        "--node", "node-1", "--dry-run",
        "--manifest", str(tmp_path / "manifest.json"),
        "--collector-evidence", str(tmp_path / "collector.json"),
        "--output", str(tmp_path / "normalization.json"),
        "--evidence-dir", str(tmp_path / "operation"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(next((tmp_path / "operation").glob("*.json")).read_text())
    assert "requires --node all" in payload["reason"]


def test_current_manifest_normalization_dispatches_reviewed_mutation(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    args = clusterctl.parser().parse_args([
        "--inventory", str(INVENTORY), "normalize-current-manifest",
        "--node", "all",
        "--manifest", str(tmp_path / "manifest.json"),
        "--collector-evidence", str(tmp_path / "collector.json"),
        "--output", str(tmp_path / "normalization.json"),
        "--yes",
    ])
    inventory = clusterctl.load_inventory(INVENTORY)
    monkeypatch.setattr(clusterctl, "operator_transport", lambda _inventory: object())
    observed: list[str] = []

    def run(command, **_kwargs):
        observed.extend(str(item) for item in command)
        return SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(clusterctl.subprocess, "run", run)
    status, reason = clusterctl.execute(args, inventory, inventory.nodes)
    assert (status, reason) == ("success", None)
    assert "normalize_current_release_pointer.py" in " ".join(observed)
    assert "--collector-evidence" in observed
    assert "--yes" in observed


def test_legacy_release_seal_requires_all_nodes(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    with_operator_refs(monkeypatch, tmp_path)
    result = clusterctl.main([
        "--inventory", str(INVENTORY), "seal-legacy-release",
        "--node", "node-1", "--output", str(tmp_path / "seal.json"),
        "--dry-run", "--evidence-dir", str(tmp_path / "operation"),
    ])
    assert result == clusterctl.EXIT_PREFLIGHT
    payload = json.loads(next((tmp_path / "operation").glob("*.json")).read_text())
    assert "requires --node all" in payload["reason"]


def test_legacy_release_seal_dispatches_reviewed_mutation(monkeypatch, tmp_path):
    with_operator_refs(monkeypatch, tmp_path)
    args = clusterctl.parser().parse_args([
        "--inventory", str(INVENTORY), "seal-legacy-release",
        "--node", "all", "--output", str(tmp_path / "seal.json"), "--yes",
    ])
    inventory = clusterctl.load_inventory(INVENTORY)
    monkeypatch.setattr(clusterctl, "operator_transport", lambda _inventory: object())
    observed: list[str] = []

    def run(command, **_kwargs):
        observed.extend(str(part) for part in command)
        return SimpleNamespace(returncode=0, stdout="", stderr="")

    monkeypatch.setattr(clusterctl.subprocess, "run", run)
    assert clusterctl.execute(args, inventory, inventory.nodes) == ("success", None)
    assert "seal_legacy_release.py" in " ".join(observed)
    assert "--yes" in observed
