from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import os
import subprocess
import sys
import tarfile
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
SCRIPT = ROOT / "deploy/production/scripts/install_immutable_release.py"
spec = importlib.util.spec_from_file_location("install_immutable_release", SCRIPT)
assert spec and spec.loader
installer = importlib.util.module_from_spec(spec)
sys.modules["install_immutable_release"] = installer
spec.loader.exec_module(installer)

RELEASE = "src-" + "a" * 40


def sha256(content: bytes) -> str:
    return hashlib.sha256(content).hexdigest()


def manifest(*, final: bool = False, schema_version: int = 1) -> bytes:
    value = {
        "schemaVersion": schema_version,
        "status": "success",
        "releaseId": RELEASE,
        "images": {
            name: f"sha256:{index:064x}"
            for index, name in enumerate(
                ("backend", "frontend", "worker", "migrator"), 1
            )
        },
        "nodeCount": 3,
        "digestParity": final,
    }
    return (json.dumps(value, sort_keys=True) + "\n").encode()


def bundle(member_name: str = "deploy/production/compose/compose.app.yml") -> bytes:
    output = io.BytesIO()
    with tarfile.open(fileobj=output, mode="w:gz") as archive:
        content = b"services: {}\n"
        item = tarfile.TarInfo(member_name)
        item.size = len(content)
        item.mode = 0o644
        archive.addfile(item, io.BytesIO(content))
    return output.getvalue()


@pytest.fixture
def release_environment(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> Path:
    base = tmp_path / "opt/massar/releases"
    base.mkdir(parents=True)
    marker = tmp_path / "cluster-id"
    marker.write_text("massar-production\n")
    incoming = tmp_path / "incoming"
    incoming.mkdir()
    active = base / ("src-" + "b" * 40)
    active.mkdir()
    current = tmp_path / "opt/massar/current"
    current.symlink_to(active)
    monkeypatch.setattr(installer, "BASE", base)
    monkeypatch.setattr(installer, "INCOMING", incoming)
    monkeypatch.setattr(installer, "CURRENT", current)
    monkeypatch.setattr(installer, "CLUSTER_MARKER", marker)
    monkeypatch.setattr(installer, "BUILD_ROOT", tmp_path / "var/lib/massar/builds")
    monkeypatch.setattr(installer, "LOCK_FILE", tmp_path / "run/install.lock")
    monkeypatch.setattr(installer.os, "geteuid", lambda: 0)
    monkeypatch.setattr(installer, "operator_uid", os.getuid)
    return tmp_path


def write_incoming(tmp_path: Path, bundle_content: bytes, manifest_content: bytes) -> None:
    incoming = tmp_path / "incoming" / f"massar-{RELEASE}"
    incoming.mkdir()
    (incoming / "release-files.tar.gz").write_bytes(bundle_content)
    (incoming / "manifest.json").write_bytes(manifest_content)


def test_install_is_digest_bound_atomic_and_refuses_existing_root(
    release_environment: Path,
) -> None:
    bundle_content = bundle()
    manifest_content = manifest()
    write_incoming(release_environment, bundle_content, manifest_content)

    result = installer.install_release(
        RELEASE, sha256(bundle_content), sha256(manifest_content)
    )

    root = installer.BASE / RELEASE
    assert result["status"] == "installed"
    assert (root / "deploy/production/compose/compose.app.yml").is_file()
    assert (root / "manifest.json").read_bytes() == manifest_content
    assert (root / ".initial-manifest.sha256").read_text().strip() == sha256(
        manifest_content
    )
    assert not (installer.BASE / f".{RELEASE}.staging").exists()
    with pytest.raises(installer.ReleaseInstallError, match="already exists"):
        installer.install_release(
            RELEASE, sha256(bundle_content), sha256(manifest_content)
        )


def test_install_accepts_bounded_complete_source_manifest_v2(
    release_environment: Path,
) -> None:
    bundle_content = bundle()
    manifest_content = manifest(schema_version=2)
    write_incoming(release_environment, bundle_content, manifest_content)

    result = installer.install_release(
        RELEASE,
        sha256(bundle_content),
        sha256(manifest_content),
    )

    assert result["status"] == "installed"
    assert json.loads(
        (installer.BASE / RELEASE / "manifest.json").read_text()
    )["schemaVersion"] == 2


def test_manifest_size_limit_remains_fail_closed(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    manifest_path = tmp_path / "manifest.json"
    manifest_path.write_bytes(b"x" * 17)
    monkeypatch.setattr(installer, "MAXIMUM_MANIFEST_BYTES", 16)
    monkeypatch.setattr(installer, "operator_uid", os.getuid)

    with pytest.raises(
        installer.ReleaseInstallError,
        match="bounded operator file",
    ):
        installer.open_operator_file(
            manifest_path,
            installer.MAXIMUM_MANIFEST_BYTES,
        )


@pytest.mark.parametrize(
    "member_name",
    ("../../etc/passwd", "deploy/production/../../escape"),
)
def test_install_rejects_traversal_and_cleans_staging(
    release_environment: Path,
    member_name: str,
) -> None:
    bundle_content = bundle(member_name)
    manifest_content = manifest()
    write_incoming(release_environment, bundle_content, manifest_content)

    with pytest.raises(installer.ReleaseInstallError, match="invalid path"):
        installer.install_release(
            RELEASE, sha256(bundle_content), sha256(manifest_content)
        )

    assert not (installer.BASE / RELEASE).exists()
    assert not (installer.BASE / f".{RELEASE}.staging").exists()


def test_install_rejects_digest_mismatch_before_staging(
    release_environment: Path,
) -> None:
    bundle_content = bundle()
    manifest_content = manifest()
    write_incoming(release_environment, bundle_content, manifest_content)

    with pytest.raises(installer.ReleaseInstallError, match="bundle digest"):
        installer.install_release(RELEASE, "0" * 64, sha256(manifest_content))

    assert not (installer.BASE / f".{RELEASE}.staging").exists()


def test_final_manifest_only_transitions_from_bound_initial_manifest(
    release_environment: Path,
) -> None:
    bundle_content = bundle()
    initial = manifest()
    write_incoming(release_environment, bundle_content, initial)
    installer.install_release(RELEASE, sha256(bundle_content), sha256(initial))
    final = manifest(final=True)
    final_path = release_environment / "incoming" / f"massar-{RELEASE}-manifest.json"
    final_path.write_bytes(final)

    result = installer.publish_final_manifest(RELEASE, sha256(final))

    assert result["status"] == "published"
    assert (installer.BASE / RELEASE / "manifest.json").read_bytes() == final
    assert installer.publish_final_manifest(RELEASE, sha256(final))["status"] == "verified"

    (installer.BASE / RELEASE / "manifest.json").write_bytes(b"tampered")
    with pytest.raises(installer.ReleaseInstallError, match="neither initial"):
        installer.publish_final_manifest(RELEASE, sha256(final))


def test_remove_inactive_release_is_bounded_and_refuses_current(
    release_environment: Path,
) -> None:
    release_root = installer.BASE / RELEASE
    release_root.mkdir()

    assert installer.remove_inactive_release(RELEASE)["status"] == "removed"
    assert not release_root.exists()

    active_release = installer.CURRENT.resolve(strict=True)
    with pytest.raises(installer.ReleaseInstallError, match="active release"):
        installer.remove_inactive_release(active_release.name)


def test_prune_keeps_current_and_rollback_and_removes_only_old_artifacts(
    release_environment: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_release = installer.CURRENT.resolve(strict=True).name
    rollback_release = "src-" + "c" * 40
    old_release = RELEASE
    legacy_release = "prod-20260726-166-r1"
    for release_id in (rollback_release, old_release, legacy_release):
        (installer.BASE / release_id).mkdir()
    installer.BUILD_ROOT.mkdir(parents=True)
    for release_id in (current_release, rollback_release, old_release):
        (installer.BUILD_ROOT / release_id).mkdir()
    docker_calls: list[tuple[str, ...]] = []

    def docker(argv, **_kwargs):
        docker_calls.append(tuple(argv))
        stdout = ""
        if argv[1:3] == ["image", "ls"]:
            stdout = "\n".join(
                (
                    f"massar/backend:{current_release}",
                    f"massar/frontend:{rollback_release}",
                    f"massar/worker:{old_release}",
                    "postgres:16-alpine",
                )
            )
        return subprocess.CompletedProcess(argv, 0, stdout=stdout, stderr="")

    monkeypatch.setattr(installer.subprocess, "run", docker)

    evidence = installer.prune_release_artifacts(
        current_release,
        rollback_release,
        "node-1",
        confirmed=True,
    )

    assert evidence["status"] == "pruned"
    assert set(evidence["releaseIds"]) == {old_release, legacy_release}
    assert evidence["buildIds"] == [old_release]
    assert evidence["imageTags"] == [f"massar/worker:{old_release}"]
    assert (installer.BASE / current_release).is_dir()
    assert (installer.BASE / rollback_release).is_dir()
    assert not (installer.BASE / old_release).exists()
    assert not (installer.BASE / legacy_release).exists()
    assert (installer.BUILD_ROOT / current_release).is_dir()
    assert (installer.BUILD_ROOT / rollback_release).is_dir()
    assert not (installer.BUILD_ROOT / old_release).exists()
    assert any(call[1:3] == ("image", "rm") for call in docker_calls)
    assert any(call[1:3] == ("builder", "prune") for call in docker_calls)


def test_prune_dry_run_reports_without_deleting(
    release_environment: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_release = installer.CURRENT.resolve(strict=True).name
    rollback_release = "src-" + "c" * 40
    old_release = RELEASE
    for release_id in (rollback_release, old_release):
        (installer.BASE / release_id).mkdir()
    docker_calls: list[tuple[str, ...]] = []

    def docker(argv, **_kwargs):
        docker_calls.append(tuple(argv))
        return subprocess.CompletedProcess(
            argv,
            0,
            stdout=f"massar/backend:{old_release}\n",
            stderr="",
        )

    monkeypatch.setattr(installer.subprocess, "run", docker)

    evidence = installer.prune_release_artifacts(
        current_release,
        rollback_release,
        "node-1",
        confirmed=False,
    )

    assert evidence["status"] == "dry-run"
    assert (installer.BASE / old_release).is_dir()
    assert len(docker_calls) == 1
    assert docker_calls[0][1:3] == ("image", "ls")


def test_prune_refuses_pointer_mismatch_before_deleting(
    release_environment: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_release = "src-" + "c" * 40
    rollback_release = installer.CURRENT.resolve(strict=True).name
    (installer.BASE / current_release).mkdir()
    (installer.BASE / RELEASE).mkdir()
    monkeypatch.setattr(
        installer.subprocess,
        "run",
        lambda argv, **_kwargs: subprocess.CompletedProcess(
            argv, 0, stdout="", stderr=""
        ),
    )

    with pytest.raises(installer.ReleaseInstallError, match="pointer"):
        installer.prune_release_artifacts(
            current_release,
            rollback_release,
            "node-1",
            confirmed=True,
        )

    assert (installer.BASE / RELEASE).is_dir()


def test_prune_refuses_unsafe_recognized_release_before_deleting(
    release_environment: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    current_release = installer.CURRENT.resolve(strict=True).name
    rollback_release = "src-" + "c" * 40
    (installer.BASE / rollback_release).mkdir()
    (installer.BASE / RELEASE).symlink_to(installer.BASE / current_release)
    monkeypatch.setattr(
        installer.subprocess,
        "run",
        lambda argv, **_kwargs: subprocess.CompletedProcess(
            argv, 0, stdout="", stderr=""
        ),
    )

    with pytest.raises(installer.ReleaseInstallError, match="not a real directory"):
        installer.prune_release_artifacts(
            current_release,
            rollback_release,
            "node-1",
            confirmed=True,
        )

    assert (installer.BASE / rollback_release).is_dir()


def test_release_workflow_uses_narrow_helper_without_broad_root_file_commands() -> None:
    release_source = (
        ROOT / "deploy/production/scripts/release_images.py"
    ).read_text(encoding="utf-8")
    sync_source = (
        ROOT / "deploy/production/scripts/manage_backup_bucket.py"
    ).read_text(encoding="utf-8")
    sudoers = (
        ROOT
        / "deploy/production/config/sudoers/massar-immutable-release-install"
    ).read_text(encoding="utf-8")

    assert "massar-install-immutable-release" in release_source
    assert "publish-final-manifest" in release_source
    assert "sudo rm " not in release_source
    assert "sudo tar " not in release_source
    assert "sudo mv " not in release_source
    assert "install_immutable_release.py" in sync_source
    assert sudoers.strip() == (
        "massar-ops ALL=(root) NOPASSWD: "
        "/usr/local/sbin/massar-install-immutable-release *"
    )
