from __future__ import annotations

import argparse
import importlib.util
import json
import sys
from dataclasses import dataclass
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))


def load(name: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


clusterctl = load("clusterctl")
release_images = load("release_images")
RELEASE = "src-" + "a" * 40
PROVENANCE = {
    "releaseId": RELEASE,
    "gitCommit": "b" * 40,
    "sourceStateSha256": "a" * 64,
    "dirtySourceSnapshot": True,
}


def inventory_value(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
):
    known_hosts = tmp_path / "known-hosts"
    identity = tmp_path / "identity"
    known_hosts.write_text("pinned")
    identity.write_text("private")
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE", str(known_hosts))
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE", str(identity))
    return clusterctl.load_inventory(
        ROOT / "deploy/production/inventory/production.yml"
    )


@dataclass
class Result:
    returncode: int = 0
    stdout: str = ""
    stderr: str = ""


class VerifiedTransport:
    def __init__(self) -> None:
        self.copies: list[object] = []
        self.commands: list[tuple[object, ...]] = []

    def run(self, target, command, **kwargs):
        self.commands.append(command)
        return Result(stdout="verified\n")

    def copy(self, *args, **kwargs):
        self.copies.append(args)


class MissingTransport(VerifiedTransport):
    def __init__(self) -> None:
        super().__init__()
        self.run_count = 0

    def run(self, target, command, **kwargs):
        self.run_count += 1
        self.commands.append(command)
        return Result(stdout="missing\n" if self.run_count == 1 else "")


def artifacts(path: Path) -> dict[str, object]:
    path.mkdir()
    images = {
        name: f"sha256:{index:064x}"
        for index, name in enumerate(release_images.IMAGES, 1)
    }
    for filename in (
        "release-files.tar.gz",
        *(f"{name}.tar" for name in release_images.IMAGES),
    ):
        artifact = path / filename
        artifact.write_bytes(filename.encode())
        (path / f"{filename}.sha256").write_text(
            release_images.file_sha256(artifact) + "\n"
        )
    manifest = {
        "schemaVersion": 1,
        **PROVENANCE,
        "createdAt": "2026-07-27T11:00:00Z",
        "platform": "linux/amd64",
        "images": images,
        "status": "success",
        "nodeCount": 3,
        "digestParity": False,
    }
    (path / "manifest.json").write_text(json.dumps(manifest))
    return manifest


def test_existing_complete_local_build_is_reused_only_when_every_hash_matches(
    tmp_path: Path,
) -> None:
    output = tmp_path / RELEASE
    manifest = artifacts(output)
    images, loaded = release_images.verify_local_release_artifacts(
        output, RELEASE, PROVENANCE
    )
    assert images == manifest["images"]
    assert loaded["releaseId"] == RELEASE

    (output / "worker.tar").write_bytes(b"tampered")
    with pytest.raises(RuntimeError, match="worker.tar"):
        release_images.verify_local_release_artifacts(
            output, RELEASE, PROVENANCE
        )


def test_remote_verified_partial_distribution_is_resumed_without_recopy(
    tmp_path: Path,
) -> None:
    output = tmp_path / RELEASE
    manifest = artifacts(output)
    nodes = tuple(
        clusterctl.Node(
            f"node-{index}", f"node-{index}", f"192.0.2.{index}",
            f"10.77.0.1{index}", ("app",),
        )
        for index in (1, 2, 3)
    )
    transport = VerifiedTransport()
    result = release_images.distribute_release(
        output, RELEASE, manifest, nodes, "massar-ops", transport
    )
    assert set(result) == {"node-1", "node-2", "node-3"}
    assert transport.copies == []
    assert len(transport.commands) == 3


def test_remote_distribution_verifies_plain_digest_sidecars_before_load(
    tmp_path: Path,
) -> None:
    output = tmp_path / RELEASE
    manifest = artifacts(output)
    node = clusterctl.Node(
        "node-1", "node-1", "192.0.2.1", "10.77.0.11", ("app",),
    )
    transport = MissingTransport()

    release_images.distribute_release(
        output, RELEASE, manifest, (node,), "massar-ops", transport
    )

    scripts = "\n".join(
        str(command[2])
        for command in transport.commands
        if len(command) >= 3 and command[:2] == ("bash", "-lc")
    )
    for name in release_images.IMAGES:
        assert f"sha256sum {name}.tar | awk" in scripts
        assert f'cat {name}.tar.sha256' in scripts
        assert f"sha256sum -c {name}.tar.sha256" not in scripts
        assert scripts.index(f"sha256sum {name}.tar") < scripts.index(
            f"docker load --input {name}.tar"
        )


def test_source_mutation_aborts_before_distribution_and_publishes_no_output(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    evidence = tmp_path / "evidence"
    inventory = inventory_value(monkeypatch, tmp_path)
    args = argparse.Namespace(
        command="build",
        release="auto",
        evidence_dir=evidence,
    )
    monkeypatch.setattr(clusterctl, "operator_transport", lambda value: object())
    monkeypatch.setattr(clusterctl, "resolve_release", lambda repo, value: PROVENANCE)
    monkeypatch.setattr(
        clusterctl,
        "create_source_snapshot",
        lambda repo, snapshot, digest: snapshot.mkdir(parents=True),
    )
    monkeypatch.setattr(
        clusterctl,
        "build_release",
        lambda snapshot, release, output: (
            output.mkdir(parents=True),
            {
                name: f"sha256:{index:064x}"
                for index, name in enumerate(release_images.IMAGES, 1)
            },
        )[1],
    )
    monkeypatch.setattr(
        clusterctl,
        "create_release_bundle",
        lambda snapshot, output: (output / "release-files.tar.gz"),
    )
    monkeypatch.setattr(
        clusterctl,
        "source_state",
        lambda repo: {**PROVENANCE, "sourceStateSha256": "0" * 64},
    )
    distributed = False

    def distribute(*values, **kwargs):
        nonlocal distributed
        distributed = True
        return {}

    monkeypatch.setattr(clusterctl, "distribute_release", distribute)
    with pytest.raises(RuntimeError, match="source changed"):
        clusterctl.execute(args, inventory, inventory.nodes)
    assert distributed is False
    assert not (evidence / RELEASE).exists()


@pytest.mark.parametrize("command", ["build", "migrate", "deploy", "rollback"])
def test_cluster_wide_release_commands_reject_single_node_scope(
    command: str,
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    inventory = inventory_value(monkeypatch, tmp_path)
    args = argparse.Namespace(command=command)
    status, reason = clusterctl.execute(args, inventory, (inventory.nodes[0],))
    assert status == "blocked"
    assert reason == f"{command} is cluster-wide and requires --node all"


def test_remote_builder_flag_uses_injected_remote_workflow_before_any_local_image_build(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    inventory = inventory_value(monkeypatch, tmp_path)
    args = clusterctl.parser().parse_args([
        "--inventory", str(ROOT / "deploy/production/inventory/production.yml"),
        "build", "--node", "all", "--release", "auto", "--remote-builder", "--yes",
    ])
    monkeypatch.setattr(clusterctl, "operator_transport", lambda _inventory: object())
    monkeypatch.setattr(clusterctl, "resolve_release", lambda _repo, _release: PROVENANCE)
    monkeypatch.setattr(
        clusterctl,
        "build_release",
        lambda *args: pytest.fail("remote-builder must not invoke local Docker build"),
    )
    observed = {}
    monkeypatch.setattr(
        clusterctl,
        "run_remote_builder_workflow",
        lambda **kwargs: observed.update(kwargs) or {"status": "success"},
    )
    status, reason = clusterctl.execute(args, inventory, inventory.nodes)
    assert status == "success"
    assert reason is None
    assert observed["transport"] is not None
    assert observed["provenance"] == PROVENANCE


def test_remote_builder_dry_run_does_not_create_a_transport(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    known_hosts = tmp_path / "known-hosts"
    identity = tmp_path / "identity"
    known_hosts.write_text("pinned")
    identity.write_text("private")
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE", str(known_hosts))
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE", str(identity))
    monkeypatch.setattr(
        clusterctl,
        "operator_transport",
        lambda _inventory: pytest.fail("dry-run must not create SSH transport"),
    )
    result = clusterctl.main([
        "--inventory", str(ROOT / "deploy/production/inventory/production.yml"),
        "build", "--node", "all", "--release", "auto", "--remote-builder", "--dry-run",
        "--evidence-dir", str(tmp_path / "evidence"),
    ])
    assert result == clusterctl.EXIT_OK
