from __future__ import annotations

import hashlib
import importlib.util
import json
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "normalize_current_release_pointer",
    SCRIPTS / "normalize_current_release_pointer.py",
)
assert SPEC and SPEC.loader
normalizer = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = normalizer
SPEC.loader.exec_module(normalizer)
ROOT_SPEC = importlib.util.spec_from_file_location(
    "normalize_current_release_root",
    SCRIPTS / "normalize_current_release_root.py",
)
assert ROOT_SPEC and ROOT_SPEC.loader
root_helper = importlib.util.module_from_spec(ROOT_SPEC)
ROOT_SPEC.loader.exec_module(root_helper)
RELEASE = "prod-20260727-166-normalize"
NOW = datetime(2026, 7, 27, 12, 0, tzinfo=timezone.utc)


def manifest_value() -> dict[str, object]:
    return {
        "schemaVersion": 1,
        "releaseId": RELEASE,
        "gitCommit": "a" * 40,
        "sourceStateSha256": "b" * 64,
        "dirtySourceSnapshot": False,
        "createdAt": "2026-07-27T11:00:00Z",
        "platform": "linux/amd64",
        "images": {
            name: f"sha256:{index:064x}"
            for index, name in enumerate(
                ("backend", "frontend", "worker", "migrator"),
                1,
            )
        },
        "status": "success",
        "nodeCount": 3,
        "digestParity": True,
        "distribution": {
            node: {"status": "verified", "releaseFilesSha256": "c" * 64}
            for node in normalizer.NODE_IDS
        },
    }


def write_inputs(tmp_path: Path) -> tuple[Path, Path]:
    manifest = tmp_path / "manifest.json"
    manifest.write_text(
        json.dumps(manifest_value(), sort_keys=True) + "\n",
        encoding="utf-8",
    )
    manifest.chmod(0o640)
    digest = hashlib.sha256(manifest.read_bytes()).hexdigest()
    images = manifest_value()["images"]
    evidence = {
        "schemaVersion": 1,
        "status": "success",
        "clusterId": "massar-production",
        "capturedAt": "2026-07-27T11:55:00Z",
        "releaseId": RELEASE,
        "manifestSha256": digest,
        "images": images,
        "nodeCount": 3,
        "byteParity": True,
        "nodes": {
            node: {
                "releaseRoot": f"/opt/massar/releases/{RELEASE}",
                "manifestPath": f"/opt/massar/releases/{RELEASE}/manifest.json",
                "manifestSha256": digest,
                "resolutionMode": "docker-label-fallback",
                "nodeLabel": node,
                "actualImages": images,
                "releaseFilesSha256": "c" * 64,
                "releaseFilesDigestVerified": True,
            }
            for node in normalizer.NODE_IDS
        },
    }
    evidence_path = tmp_path / "collector.json"
    evidence_path.write_text(
        json.dumps(evidence, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    evidence_path.chmod(0o640)
    return manifest, evidence_path


def inventory(monkeypatch: pytest.MonkeyPatch, tmp_path: Path):
    known_hosts = tmp_path / "known-hosts"
    identity = tmp_path / "identity"
    known_hosts.write_text("pinned", encoding="utf-8")
    identity.write_text("private", encoding="utf-8")
    identity.chmod(0o600)
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE", str(known_hosts))
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE", str(identity))
    return normalizer.load_inventory(
        ROOT / "deploy/production/inventory/production.yml"
    )


@dataclass
class Result:
    stdout: str
    returncode: int = 0
    stderr: str = ""


class FakeTransport:
    def __init__(self, fail_apply_node: str | None = None) -> None:
        self.fail_apply_node = fail_apply_node
        self.commands: list[tuple[str, str]] = []
        self.identities = {
            node: {"device": index, "inode": index + 100}
            for index, node in enumerate(normalizer.NODE_IDS, 1)
        }

    def run(self, target, command, **_kwargs):
        action = command[2]
        self.commands.append((target.node_id, action))
        if action == "apply" and target.node_id == self.fail_apply_node:
            raise RuntimeError("simulated lost apply response")
        base = {
            "schemaVersion": 1,
            "releaseId": RELEASE,
        }
        if action == "preflight":
            value = {
                **base,
                "status": "ready",
                "releaseRoot": f"/opt/massar/releases/{RELEASE}",
                "manifestSha256": command[4],
                "currentAbsent": True,
            }
        elif action in {"apply", "verify"}:
            value = {
                **base,
                "status": "created" if action == "apply" else "verified",
                "target": f"/opt/massar/releases/{RELEASE}",
                **self.identities[target.node_id],
            }
        else:
            value = {**base, "status": "removed"}
        return Result(json.dumps(value))


def test_normalizes_only_after_fresh_three_node_fallback_evidence(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    cluster = inventory(monkeypatch, tmp_path)
    manifest_path, collector_path = write_inputs(tmp_path)
    manifest, collector = normalizer.validate_inputs(
        manifest_path,
        collector_path,
        now=NOW,
    )
    transport = FakeTransport()
    output = tmp_path / "normalization.json"
    result = normalizer.normalize(
        inventory=cluster,
        transport=transport,
        manifest=manifest,
        collector_evidence=collector,
        evidence_output=output,
        now=NOW,
    )
    assert result["status"] == "success"
    assert output.stat().st_mode & 0o777 == 0o640
    assert [action for _, action in transport.commands].count("preflight") == 3
    assert [action for _, action in transport.commands].count("apply") == 3
    assert [action for _, action in transport.commands].count("verify") == 3
    assert "remove" not in [action for _, action in transport.commands]


def test_refuses_existing_current_mode_stale_or_symlink_evidence(
    tmp_path: Path,
) -> None:
    manifest_path, collector_path = write_inputs(tmp_path)
    value = json.loads(collector_path.read_text(encoding="utf-8"))
    value["nodes"]["node-2"]["resolutionMode"] = "current-pointer"
    collector_path.write_text(json.dumps(value), encoding="utf-8")
    with pytest.raises(normalizer.NormalizationError, match="node-2"):
        normalizer.validate_inputs(manifest_path, collector_path, now=NOW)
    value["nodes"]["node-2"]["resolutionMode"] = "docker-label-fallback"
    value["nodes"]["node-2"]["releaseFilesDigestVerified"] = False
    value["nodes"]["node-2"]["releaseFilesSha256"] = None
    collector_path.write_text(json.dumps(value), encoding="utf-8")
    with pytest.raises(normalizer.NormalizationError, match="node-2"):
        normalizer.validate_inputs(manifest_path, collector_path, now=NOW)
    collector_path.unlink()
    collector_path.symlink_to(manifest_path)
    with pytest.raises(normalizer.NormalizationError, match="regular file"):
        normalizer.validate_inputs(manifest_path, collector_path, now=NOW)


def test_apply_failure_runs_marker_bound_compensation_on_all_nodes(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    cluster = inventory(monkeypatch, tmp_path)
    manifest_path, collector_path = write_inputs(tmp_path)
    manifest, collector = normalizer.validate_inputs(
        manifest_path,
        collector_path,
        now=NOW,
    )
    transport = FakeTransport(fail_apply_node="node-2")
    output = tmp_path / "normalization.json"
    with pytest.raises(RuntimeError, match="lost apply response"):
        normalizer.normalize(
            inventory=cluster,
            transport=transport,
            manifest=manifest,
            collector_evidence=collector,
            evidence_output=output,
            now=NOW,
        )
    assert transport.commands[-3:] == [
        ("node-3", "remove"),
        ("node-2", "remove"),
        ("node-1", "remove"),
    ]
    assert not output.exists()


def test_root_helper_uses_no_overwrite_inode_bound_marker_contract() -> None:
    source = (
        SCRIPTS / "normalize_current_release_root.py"
    ).read_text(encoding="utf-8")
    assert "os.path.lexists(CURRENT)" in source
    assert "os.link(temporary, CURRENT, follow_symlinks=False)" in source
    assert '"device": device' in source
    assert '"inode": inode' in source
    assert "actual != (device, inode)" in source
    assert "refusing to remove a pointer not created by this operation" in source
    assert 'f".current-switch-{operation_id}"' in source
    assert "os.replace(temporary, CURRENT)" in source
    assert '"status": "already-current"' in source
    assert "previousManifestSha256" in source


def test_root_helper_switches_an_existing_pointer_atomically_and_idempotently(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    base = tmp_path / "opt/massar/releases"
    base.mkdir(parents=True)
    cluster_marker = tmp_path / "cluster-id"
    cluster_marker.write_text("massar-production\n", encoding="ascii")
    current = base.parent / "current"

    def release(release_id: str) -> tuple[Path, str]:
        root = base / release_id
        root.mkdir()
        value = {
            "schemaVersion": 1,
            "status": "success",
            "releaseId": release_id,
            "images": {
                name: f"sha256:{index:064x}"
                for index, name in enumerate(
                    ("backend", "frontend", "worker", "migrator"),
                    1,
                )
            },
        }
        manifest = root / "manifest.json"
        manifest.write_text(json.dumps(value) + "\n", encoding="utf-8")
        return root, hashlib.sha256(manifest.read_bytes()).hexdigest()

    old_root, _ = release("prod-20260726-166-r1")
    new_root, new_digest = release("src-" + "a" * 40)
    current.symlink_to(old_root)
    monkeypatch.setattr(root_helper, "BASE", base)
    monkeypatch.setattr(root_helper, "CURRENT", current)
    monkeypatch.setattr(root_helper, "CLUSTER_MARKER", cluster_marker)
    monkeypatch.setattr(root_helper.os, "geteuid", lambda: 0)

    result = root_helper.switch(
        "1" * 32,
        new_root.name,
        new_digest,
    )
    assert result["status"] == "switched"
    assert result["previousReleaseId"] == old_root.name
    assert current.is_symlink()
    assert current.readlink() == new_root
    repeated = root_helper.switch("2" * 32, new_root.name, new_digest)
    assert repeated["status"] == "already-current"


def test_helper_is_installed_with_one_narrow_root_entry() -> None:
    sudoers = (
        ROOT
        / "deploy/production/config/sudoers/massar-current-release-normalization"
    ).read_text(encoding="utf-8").strip()
    assert sudoers == (
        "massar-ops ALL=(root) NOPASSWD: "
        "/usr/local/sbin/massar-normalize-current-release *"
    )
    manager = (
        SCRIPTS / "manage_backup_bucket.py"
    ).read_text(encoding="utf-8")
    assert (
        '"normalize_current_release_root.py":\n'
        '        "/usr/local/sbin/massar-normalize-current-release"'
    ) in manager
    assert "/etc/sudoers.d/massar-current-release-normalization" in manager
