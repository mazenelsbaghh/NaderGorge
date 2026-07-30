from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPTS = ROOT / "deploy/production/scripts"
sys.path.insert(0, str(SCRIPTS))


def load(name: str):
    spec = importlib.util.spec_from_file_location(name, SCRIPTS / f"{name}.py")
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


deploy = load("deploy_release")
release_images = load("release_images")


def manifest(
    tmp_path: Path,
    release: str = "git-" + "a" * 40,
) -> Path:
    path = tmp_path / "manifest.json"
    path.write_text(json.dumps({
        "schemaVersion": 1,
        "releaseId": release,
        "gitCommit": release.removeprefix("git-"),
        "sourceStateSha256": "b" * 64,
        "dirtySourceSnapshot": False,
        "createdAt": "2026-07-27T11:30:00Z",
        "platform": "linux/amd64",
        "images": {
            name: f"sha256:{index:064x}"
            for index, name in enumerate(("backend", "frontend", "worker", "migrator"), 1)
        },
        "status": "success",
        "nodeCount": 3,
        "digestParity": True,
        "distribution": {
            node: {"status": "verified", "releaseFilesSha256": "c" * 64}
            for node in ("node-1", "node-2", "node-3")
        },
    }))
    return path


def test_release_manifest_requires_exact_images_and_valid_digests(tmp_path: Path) -> None:
    release = "git-" + "a" * 40
    images = deploy.load_release_manifest(manifest(tmp_path), release).images
    release_images.verify_manifest(images)
    assert set(images) == {"backend", "frontend", "worker", "migrator"}


def test_release_manifest_refuses_release_mismatch(tmp_path: Path) -> None:
    with pytest.raises(deploy.ReleaseContractError, match="identity"):
        deploy.load_release_manifest(manifest(tmp_path), "git-" + "d" * 40)


def test_backup_gate_requires_a_real_restore(tmp_path: Path) -> None:
    evidence = tmp_path / "backup.json"
    evidence.write_text(json.dumps({"status": "success", "restoreVerified": False}))
    release = "git-" + "a" * 40
    release_value = deploy.load_release_manifest(manifest(tmp_path), release)
    with pytest.raises(deploy.ReleaseContractError, match="exact contract"):
        deploy.load_migration_safety_gate(evidence, manifest=release_value)


def test_rolling_order_preserves_two_serving_nodes() -> None:
    assert deploy.ROLLING_ORDER == ("node-3", "node-2", "node-1")
    source = (SCRIPTS / "deploy_release.py").read_text(encoding="utf-8")
    loop = source.index("for node_id in ROLLING_ORDER")
    assert source.index("assert_rollout_quorum(", loop) < source.index(
        "deploy_node(", loop
    )
    assert "rollout stopped" in source
    assert "--no-build" in source
    assert "for service in" in source
    assert 'docker exec "$worker_id"' in source
    assert "api.massar-academy.net/api/health/ready" in source
    assert "massar/backend:{release_id}" in source
    assert "docker image inspect" in source


def test_rollback_is_application_only_and_requires_schema_compatibility() -> None:
    source = (SCRIPTS / "rollback_release.py").read_text(encoding="utf-8")
    assert "load_rollback_compatibility_gate" in source
    assert "--current-manifest" in source
    assert "--rollback-current-manifest" in source
    assert "--rollback-evidence" in source
    assert "--backup-evidence" not in source
    assert "down-migration" in source
    assert "database update" not in source


def test_dirty_source_uses_content_addressed_release(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    monkeypatch.setattr(
        release_images,
        "source_state",
        lambda _repo: {
            "releaseId": f"src-{'a' * 40}",
            "gitCommit": "b" * 40,
            "sourceStateSha256": "a" * 64,
            "dirtySourceSnapshot": True,
        },
    )
    resolved = release_images.resolve_release(ROOT, "auto")
    assert resolved["releaseId"] == f"src-{'a' * 40}"
    with pytest.raises(ValueError, match="exact current source"):
        release_images.resolve_release(ROOT, f"git-{'b' * 40}")


def test_release_builder_targets_production_architecture_and_distribution() -> None:
    source = (SCRIPTS / "release_images.py").read_text(encoding="utf-8")
    assert '"linux/amd64"' in source
    assert "docker load" in source
    assert "release-files.tar.gz" in source
    assert "digestParity" not in source
    clusterctl = (SCRIPTS / "clusterctl.py").read_text(encoding="utf-8")
    assert "distribute_release(" in clusterctl
    assert 'manifest["digestParity"] = len(distribution) == 3' in clusterctl


def test_migrator_has_required_framework_and_drops_root_after_secret_read() -> None:
    dockerfile = (ROOT / "backend/Dockerfile.migrator").read_text(encoding="utf-8")
    migration = (SCRIPTS / "migrate_release.py").read_text(encoding="utf-8")
    gate = (SCRIPTS / "prepare_release_migration_gate.py").read_text(
        encoding="utf-8"
    )
    assert "mcr.microsoft.com/dotnet/aspnet:9.0.6" in dockerfile
    assert "mcr.microsoft.com/dotnet/runtime:9.0.6" not in dockerfile
    for source in (migration, gate):
        assert "--user 0:0" in source
        assert "setpriv --reuid=65532 --regid=65532 --clear-groups" in source
    assert "host.docker.internal" not in migration
    assert "Host=127.0.0.1;Port=6544" in gate


def test_source_provenance_excludes_generated_runtime_artifacts() -> None:
    assert "artifacts" not in release_images.SOURCE_ROOTS
    assert ".next" in release_images.SOURCE_EXCLUDED_PARTS
    assert "dist" in release_images.SOURCE_EXCLUDED_PARTS
    assert "bin" in release_images.SOURCE_EXCLUDED_PARTS
    assert "obj" in release_images.SOURCE_EXCLUDED_PARTS
