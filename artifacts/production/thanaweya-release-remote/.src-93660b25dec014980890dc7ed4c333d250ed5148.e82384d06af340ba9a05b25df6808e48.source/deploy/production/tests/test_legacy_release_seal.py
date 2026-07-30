from __future__ import annotations

import importlib.util
import json
import sys
from dataclasses import dataclass
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "seal_legacy_release", SCRIPTS / "seal_legacy_release.py"
)
assert SPEC and SPEC.loader
seal = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = seal
SPEC.loader.exec_module(seal)
ROOT_SPEC = importlib.util.spec_from_file_location(
    "seal_legacy_release_root", SCRIPTS / "seal_legacy_release_root.py"
)
assert ROOT_SPEC and ROOT_SPEC.loader
root_seal = importlib.util.module_from_spec(ROOT_SPEC)
sys.modules[ROOT_SPEC.name] = root_seal
ROOT_SPEC.loader.exec_module(root_seal)
RELEASE = "prod-20260726-166-r1"
TREE = "c" * 64
IMAGES = {
    name: f"sha256:{index:064x}"
    for index, name in enumerate(("backend", "frontend", "worker"), 1)
}


@dataclass
class Result:
    stdout: str


class FakeTransport:
    def __init__(self, divergent: bool = False, fail_node: str | None = None):
        self.divergent = divergent
        self.fail_node = fail_node
        self.commands: list[tuple[str, str]] = []

    def run(self, target, command, **_kwargs):
        action = command[2]
        self.commands.append((target.node_id, action))
        if action == "apply" and target.node_id == self.fail_node:
            raise RuntimeError("lost apply response")
        status = {
            "inspect": "ready", "apply": "sealed",
            "verify": "verified", "remove": "removed",
        }[action]
        value = {
            "schemaVersion": 1, "status": status, "nodeId": target.node_id,
            "releaseId": RELEASE,
        }
        if action == "inspect":
            value.update({
                "images": IMAGES,
                "treeSha256": "d" * 64
                if self.divergent and target.node_id == "node-2" else TREE,
            })
        if action == "apply":
            value.update({
                "treeSha256": TREE, "manifestSha256": "e" * 64,
                "files": {},
            })
        return Result(json.dumps(value))


def inventory(monkeypatch, tmp_path):
    known = tmp_path / "known"
    identity = tmp_path / "identity"
    known.write_text("pinned")
    identity.write_text("key")
    identity.chmod(0o600)
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE", str(known))
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE", str(identity))
    return seal.load_inventory(ROOT / "deploy/production/inventory/production.yml")


def test_sealed_legacy_manifest_is_strictly_accepted(tmp_path):
    payload = seal.sealed_manifest(
        RELEASE, IMAGES, TREE, "2026-07-27T12:00:00Z"
    )
    path = tmp_path / "manifest.json"
    path.write_bytes(payload)
    contract = seal.load_release_manifest(path, RELEASE)
    assert contract.provenance_type == "sealed-legacy-bootstrap"
    assert contract.git_commit is None
    assert contract.release_files_sha256 == TREE


def test_seal_proves_parity_and_compensates_all_nodes(monkeypatch, tmp_path):
    cluster = inventory(monkeypatch, tmp_path)
    transport = FakeTransport(fail_node="node-2")
    with pytest.raises(RuntimeError, match="lost apply"):
        seal.seal(cluster, transport, tmp_path / "evidence.json")
    assert transport.commands[-3:] == [
        ("node-3", "remove"), ("node-2", "remove"), ("node-1", "remove"),
    ]
    assert not (tmp_path / "evidence.json").exists()


def test_seal_refuses_tree_divergence_before_mutation(monkeypatch, tmp_path):
    cluster = inventory(monkeypatch, tmp_path)
    transport = FakeTransport(divergent=True)
    with pytest.raises(seal.SealError, match="tree differ"):
        seal.seal(cluster, transport, tmp_path / "evidence.json")
    assert "apply" not in [action for _, action in transport.commands]


def test_root_helper_contract_is_no_overwrite_and_inode_bound():
    source = (SCRIPTS / "seal_legacy_release_root.py").read_text()
    assert '"ps", "-q"' in source
    assert "set(found) != set(SERVICES)" in source
    assert "for relative in RUNTIME_FILES" in source
    assert "os.O_EXCL" in source
    assert "os.link(temporary, destination, follow_symlinks=False)" in source
    assert "[info.st_dev, info.st_ino] != identity" in source


def configure_root_helper(monkeypatch, tmp_path):
    opt = tmp_path / "opt"
    massar = opt / "massar"
    releases = massar / "releases"
    release_root = releases / RELEASE
    release_root.mkdir(parents=True)
    for relative in (
        "deploy/production/compose/compose.app.yml",
        "deploy/production/config/nginx/massar-node.conf.template",
    ):
        path = release_root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("verified")
    marker = tmp_path / "cluster-id"
    marker.write_text("massar-production")
    monkeypatch.setattr(root_seal, "BASE", releases)
    monkeypatch.setattr(root_seal, "CURRENT", massar / "current")
    monkeypatch.setattr(root_seal, "MARKERS", tmp_path / "run")
    monkeypatch.setattr(root_seal, "CLUSTER_MARKER", marker)
    monkeypatch.setattr(root_seal, "FIXED_PARENTS", (opt, massar, releases))
    monkeypatch.setattr(root_seal.os, "geteuid", lambda: 0)
    monkeypatch.setattr(root_seal, "release_identity", lambda _node: (RELEASE, IMAGES))
    return release_root


def test_root_helper_rejects_marker_parent_symlink_and_oversize(
    monkeypatch, tmp_path
):
    configure_root_helper(monkeypatch, tmp_path)
    real = tmp_path / "real-run"
    real.mkdir()
    root_seal.MARKERS.symlink_to(real, target_is_directory=True)
    with pytest.raises(root_seal.SealError, match="parent"):
        root_seal.create_marker("a" * 32, RELEASE, TREE, {})
    root_seal.MARKERS.unlink()
    with pytest.raises(root_seal.SealError, match="exceeds"):
        root_seal.apply(
            "node-1", "a" * 32, TREE,
            "A" * (root_seal.MAXIMUM_MANIFEST_BYTES * 2 + 1),
        )


def test_partial_operation_cleanup_removes_only_recorded_inode(
    monkeypatch, tmp_path
):
    release_root = configure_root_helper(monkeypatch, tmp_path)
    root_seal.MARKERS.mkdir()
    operation = "b" * 32
    partial = release_root / "manifest.json"
    partial.write_text("partial")
    identity = [partial.lstat().st_dev, partial.lstat().st_ino]
    (root_seal.MARKERS / f"{operation}.json").write_text(json.dumps({
        "releaseId": RELEASE, "treeSha256": TREE,
        "payloadSha256": {
            "manifest.json": root_seal.hashlib.sha256(b"partial").hexdigest()
        },
        "files": {"manifest.json": identity}, "aliases": {},
    }))
    result = root_seal.verify_or_remove("node-1", operation, True)
    assert result["status"] == "removed"
    assert not partial.exists()
