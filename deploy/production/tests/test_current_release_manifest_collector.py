from __future__ import annotations

import base64
import hashlib
import importlib.util
import json
import os
import sys
from dataclasses import dataclass
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))
SPEC = importlib.util.spec_from_file_location(
    "collect_current_release_manifest",
    SCRIPTS / "collect_current_release_manifest.py",
)
assert SPEC and SPEC.loader
collector = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = collector
SPEC.loader.exec_module(collector)
RELEASE = "prod-20260726-166-r1"


def manifest_bytes() -> bytes:
    return (json.dumps({
        "schemaVersion": 1,
        "releaseId": RELEASE,
        "gitCommit": "a" * 40,
        "sourceStateSha256": "b" * 64,
        "dirtySourceSnapshot": False,
        "createdAt": "2026-07-26T12:00:00Z",
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
            node: {
                "status": "verified",
                "releaseFilesSha256": "c" * 64,
            }
            for node in ("node-1", "node-2", "node-3")
        },
    }, sort_keys=True) + "\n").encode()


def envelope(content: bytes | None = None, **overrides: object) -> str:
    payload = content if content is not None else manifest_bytes()
    value = json.loads(payload)
    result: dict[str, object] = {
        "schemaVersion": 1,
        "resolutionMode": "docker-label-fallback",
        "nodeLabel": "node-1",
        "releaseId": RELEASE,
        "releaseRoot": f"/opt/massar/releases/{RELEASE}",
        "manifestPath": f"/opt/massar/releases/{RELEASE}/manifest.json",
        "manifestSha256": hashlib.sha256(payload).hexdigest(),
        "manifestBase64": base64.b64encode(payload).decode(),
        "images": value["images"],
        "actualImages": value["images"],
        "releaseFilesSha256": "c" * 64,
        "releaseFilesDigestVerified": True,
    }
    result.update(overrides)
    return json.dumps(result)


@dataclass
class Result:
    stdout: str
    returncode: int = 0
    stderr: str = ""


class FakeTransport:
    def __init__(self, outputs: dict[str, str] | None = None) -> None:
        self.outputs = outputs or {
            node: envelope(nodeLabel=node)
            for node in ("node-1", "node-2", "node-3")
        }
        self.commands: list[tuple[str, tuple[str, ...]]] = []

    def run(self, target, command, **_kwargs):
        self.commands.append((target.node_id, command))
        return Result(self.outputs[target.node_id])


def inventory(monkeypatch: pytest.MonkeyPatch, tmp_path: Path):
    known_hosts = tmp_path / "known-hosts"
    identity = tmp_path / "identity"
    known_hosts.write_text("pinned", encoding="utf-8")
    identity.write_text("private", encoding="utf-8")
    identity.chmod(0o600)
    monkeypatch.setenv("MASSAR_KNOWN_HOSTS_FILE", str(known_hosts))
    monkeypatch.setenv("MASSAR_SSH_IDENTITY_FILE", str(identity))
    return collector.load_inventory(
        ROOT / "deploy/production/inventory/production.yml"
    )


def test_collects_exact_three_node_parity_without_overwrite(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    cluster = inventory(monkeypatch, tmp_path)
    transport = FakeTransport()
    manifest_output = tmp_path / "collected" / "current-manifest.json"
    evidence_output = tmp_path / "collected" / "current-manifest-evidence.json"
    evidence = collector.collect(
        inventory=cluster,
        transport=transport,
        manifest_output=manifest_output,
        evidence_output=evidence_output,
    )
    assert manifest_output.read_bytes() == manifest_bytes()
    assert stat_mode(manifest_output) == 0o640
    assert stat_mode(evidence_output) == 0o640
    assert evidence["releaseId"] == RELEASE
    assert evidence["byteParity"] is True
    assert {
        details["resolutionMode"]
        for details in evidence["nodes"].values()
    } == {"docker-label-fallback"}
    assert set(evidence["nodes"]) == {"node-1", "node-2", "node-3"}
    assert [node for node, _ in transport.commands] == [
        "node-1", "node-2", "node-3",
    ]
    assert all(command[:2] == ("python3", "-c") for _, command in transport.commands)
    with pytest.raises(collector.ManifestCollectionError, match="already exists"):
        collector.collect(
            inventory=cluster,
            transport=transport,
            manifest_output=manifest_output,
            evidence_output=tmp_path / "unused.json",
        )


def stat_mode(path: Path) -> int:
    return path.stat().st_mode & 0o777


@pytest.mark.parametrize(
    "node_output,message",
    [
        (
            envelope(manifest_bytes() + b" "),
            "differ",
        ),
        (
            envelope(releaseRoot="/opt/massar/releases/../escape"),
            "path or digest",
        ),
        (
            envelope(manifestPath="/opt/massar/current/manifest.json"),
            "path or digest",
        ),
        (
            envelope(manifestBase64="not-base64%%%"),
            "base64",
        ),
        (
            envelope(manifestSha256="0" * 64),
            "bytes do not match",
        ),
    ],
)
def test_refuses_divergence_traversal_and_invalid_payloads(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    node_output: str,
    message: str,
) -> None:
    cluster = inventory(monkeypatch, tmp_path)
    outputs = {
        "node-1": envelope(nodeLabel="node-1"),
        "node-2": json.dumps({
            **json.loads(node_output),
            "nodeLabel": "node-2",
        }),
        "node-3": envelope(nodeLabel="node-3"),
    }
    with pytest.raises(collector.ManifestCollectionError, match=message):
        collector.collect(
            inventory=cluster,
            transport=FakeTransport(outputs),
            manifest_output=tmp_path / "manifest.json",
            evidence_output=tmp_path / "evidence.json",
        )
    assert not (tmp_path / "manifest.json").exists()
    assert not (tmp_path / "evidence.json").exists()


def test_refuses_symlink_output_and_remote_reader_is_fixed_and_nofollow(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    cluster = inventory(monkeypatch, tmp_path)
    target = tmp_path / "target.json"
    target.write_text("preserve", encoding="utf-8")
    link = tmp_path / "manifest.json"
    link.symlink_to(target)
    with pytest.raises(collector.ManifestCollectionError, match="already exists"):
        collector.collect(
            inventory=cluster,
            transport=FakeTransport(),
            manifest_output=link,
            evidence_output=tmp_path / "evidence.json",
        )
    assert target.read_text(encoding="utf-8") == "preserve"
    source = collector.REMOTE_READER
    assert 'current=pathlib.Path("/opt/massar/current")' in source
    assert 'base=pathlib.Path("/opt/massar/releases")' in source
    assert "os.lstat" in source
    assert "O_NOFOLLOW" in source
    assert "release_root.parent != base" in source
    assert "maximum=4*1024*1024" in source
    assert "source_v2_required={" in source
    assert "source_v1_required,source_v2_required,legacy_required" in source
    assert "if os.path.lexists(current):" in source
    assert 'resolution_mode="docker-label-fallback"' in source
    assert 'label=com.docker.compose.project=massar_production' in source
    assert "actual_images!=images" in source
    assert 'manifest_path=release_root/"manifest.json"' in source


def test_accepts_manifest_larger_than_legacy_one_megabyte_bound() -> None:
    value = json.loads(manifest_bytes())
    value["padding"] = "x" * (1024 * 1024)
    content = (json.dumps(value, sort_keys=True) + "\n").encode()
    assert 1024 * 1024 < len(content) < collector.MAXIMUM_MANIFEST_BYTES
    parsed = collector.parse_remote("node-1", envelope(content))
    assert parsed.content == content


def test_rejects_manifest_above_four_megabyte_bound() -> None:
    value = json.loads(manifest_bytes())
    value["padding"] = "x" * collector.MAXIMUM_MANIFEST_BYTES
    content = (json.dumps(value, sort_keys=True) + "\n").encode()
    assert len(content) > collector.MAXIMUM_MANIFEST_BYTES
    with pytest.raises(
        collector.ManifestCollectionError,
        match="manifest bytes do not match",
    ):
        collector.parse_remote("node-1", envelope(content))


def test_refuses_node_label_divergence_and_image_mismatch() -> None:
    payload = json.loads(envelope(nodeLabel="node-2"))
    with pytest.raises(collector.ManifestCollectionError, match="path or digest"):
        collector.parse_remote("node-1", json.dumps(payload))
    payload["nodeLabel"] = "node-1"
    payload["actualImages"] = {
        **payload["images"],
        "backend": "sha256:" + "f" * 64,
    }
    with pytest.raises(collector.ManifestCollectionError, match="path or digest"):
        collector.parse_remote("node-1", json.dumps(payload))


def test_missing_remote_manifest_never_publishes(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    cluster = inventory(monkeypatch, tmp_path)

    class MissingManifestTransport(FakeTransport):
        def run(self, target, command, **_kwargs):
            if target.node_id == "node-2":
                raise RuntimeError("release manifest is missing")
            return super().run(target, command)

    with pytest.raises(RuntimeError, match="manifest is missing"):
        collector.collect(
            inventory=cluster,
            transport=MissingManifestTransport(),
            manifest_output=tmp_path / "manifest.json",
            evidence_output=tmp_path / "evidence.json",
        )
    assert not (tmp_path / "manifest.json").exists()
    assert not (tmp_path / "evidence.json").exists()


def test_refuses_local_parent_symlink_and_traversal(tmp_path: Path) -> None:
    real = tmp_path / "real"
    real.mkdir()
    linked_parent = tmp_path / "linked"
    linked_parent.symlink_to(real, target_is_directory=True)
    with pytest.raises(collector.ManifestCollectionError, match="symlink"):
        collector.ensure_output_target(
            linked_parent / "manifest.json",
            "manifest",
        )
    with pytest.raises(collector.ManifestCollectionError, match="traversal"):
        collector.ensure_output_target(
            Path(str(tmp_path / "real") + "/../manifest.json"),
            "manifest",
        )


def test_dry_run_never_constructs_transport_or_writes_outputs(
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
    capsys: pytest.CaptureFixture[str],
) -> None:
    inventory(monkeypatch, tmp_path)
    constructed = False

    def forbidden_transport(*_args, **_kwargs):
        nonlocal constructed
        constructed = True
        raise AssertionError("dry-run must not construct SSH transport")

    monkeypatch.setattr(collector, "StrictSshTransport", forbidden_transport)
    output_root = tmp_path / "does-not-exist"
    result = collector.main([
        "--inventory",
        str(ROOT / "deploy/production/inventory/production.yml"),
        "--known-hosts",
        str(tmp_path / "missing-known-hosts"),
        "--identity",
        str(tmp_path / "missing-identity"),
        "--manifest-output",
        str(output_root / "manifest.json"),
        "--evidence-output",
        str(output_root / "evidence.json"),
        "--dry-run",
    ])
    assert result == 0
    assert json.loads(capsys.readouterr().out)["sshAttempted"] is False
    assert constructed is False
    assert not output_root.exists()


def test_atomic_publisher_refuses_concurrent_destination(tmp_path: Path) -> None:
    destination = tmp_path / "destination.json"
    temporary = collector.stage_file(destination, b"new")
    destination.write_bytes(b"existing")
    with pytest.raises(collector.ManifestCollectionError, match="concurrently"):
        collector.publish_without_overwrite(temporary, destination)
    assert destination.read_bytes() == b"existing"
    temporary.unlink(missing_ok=True)


def test_collector_accepts_honest_sealed_legacy_manifest() -> None:
    sealed = json.loads(manifest_bytes())
    for field in ("gitCommit", "sourceStateSha256", "dirtySourceSnapshot"):
        sealed.pop(field)
    sealed["sealedLegacyProvenance"] = {
        "schemaVersion": 2,
        "type": "sealed-legacy-bootstrap",
        "sealedAt": "2026-07-26T12:00:00Z",
        "runtimeBundleSha256": "c" * 64,
        "runtimeBundleDigestAlgorithm": "massar-runtime-bundle-sha256-v1",
        "sourceReleaseLabel": RELEASE,
    }
    sealed["images"].pop("migrator")
    payload = (json.dumps(sealed, sort_keys=True) + "\n").encode()
    parsed = collector.parse_remote(
        "node-1",
        envelope(payload, nodeLabel="node-1"),
    )
    contract = collector.validate_manifest_bytes(parsed.content, RELEASE)
    assert contract.provenance_type == "sealed-legacy-bootstrap"
